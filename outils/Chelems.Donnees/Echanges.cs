using System.Globalization;
using System.Text;

namespace Chelems.Donnees;

/// <summary>
/// Cibles de l'échange (moteur V0.3) tirées du Match Charting Project (Jeff Sackmann, hommes) :
/// longueur des échanges, % de points gagnés par le serveur selon la longueur, part des coups gagnants,
/// fautes provoquées et fautes directes, et contrôles du service (aces, doubles fautes, premières balles).
///
/// Sources : charting-m-matches.csv (surface, date), charting-m-stats-Rally.csv (lignes « 1-3 », « 4-6 », « 7-9 », « 10 »,
/// et leurs variantes « -1 » / « -2 » selon le joueur au service, où pl1 désigne alors le serveur), charting-m-stats-Overview.csv (lignes « Total »).
/// Définitions MCP : la longueur compte le service et exclut la faute finale ; les coups gagnants comprennent les aces ;
/// les doubles fautes ne sont comptées ni en faute directe ni en faute provoquée ; un service non retourné
/// est en général une faute provoquée du relanceur.
///
/// Usage : dotnet run --project outils/Chelems.Donnees -- echanges [Hard|Clay|Grass] [annee_min] [annee_max]
/// </summary>
internal static class Echanges
{
    private static readonly CultureInfo Ci = CultureInfo.InvariantCulture;
    private static readonly string[] Tranches = ["1-3", "4-6", "7-9", "10"];
    private static string Nom(string t) => t == "10" ? "10+" : t;

    internal static void Executer(string dossierMcp, string surface, int anMin, int anMax, string fichierSortie)
    {
        var matchs = new HashSet<string>();
        foreach (var l in Lire(Path.Combine(dossierMcp, "charting-m-matches.csv")))
        {
            if (l.Length < 11 || l[10] != surface) continue;
            if (!int.TryParse(l[0].AsSpan(0, 4), NumberStyles.Integer, Ci, out int an) || an < anMin || an > anMax) continue;
            matchs.Add(l[0]);
        }

        // Rally : points, gagnés, gagnants, provoquées, directes par tranche ; et points gagnés par le serveur.
        var pts = new Dictionary<string, double>(); var srv = new Dictionary<string, double>();
        var gag = new Dictionary<string, double>(); var prov = new Dictionary<string, double>(); var dir = new Dictionary<string, double>();
        var avecRally = new HashSet<string>();
        foreach (var l in Lire(Path.Combine(dossierMcp, "charting-m-stats-Rally.csv")))
        {
            if (!matchs.Contains(l[0])) continue;
            string row = l[3];
            double N(int i) => double.Parse(l[i], Ci);
            if (Tranches.Contains(row))
            {
                avecRally.Add(l[0]);
                Ajouter(pts, row, N(4));
                Ajouter(gag, row, N(6) + N(10));
                Ajouter(prov, row, N(7) + N(11));
                Ajouter(dir, row, N(8) + N(12));
            }
            else
            {
                int k = row.LastIndexOf('-');
                if (k <= 0 || !Tranches.Contains(row[..k])) continue;
                Ajouter(srv, row[..k], N(5)); // lignes « -1 » / « -2 » : pl1 = le serveur (vérifié avec Overview)
            }
        }

        // Overview : contrôles du service (mêmes définitions que les colonnes ATP).
        double svpt = 0, aces = 0, dfs = 0, firstIn = 0, firstWon = 0, secondWon = 0;
        foreach (var l in Lire(Path.Combine(dossierMcp, "charting-m-stats-Overview.csv")))
        {
            if (!avecRally.Contains(l[0]) || l[2] != "Total") continue;
            double N(int i) => double.Parse(l[i], Ci);
            svpt += N(3); aces += N(4); dfs += N(5); firstIn += N(6); firstWon += N(7); secondWon += N(9);
        }

        // ServeBasics : issues du service par balle (ligne « 1 » = premières rentrées, « 2 » = points joués sur seconde, doubles fautes comprises).
        // Service gagnant = service non retourné (unret) ou retour en faute provoquée (forced_err), aces exclus.
        double[] sbPts = new double[2], sbAces = new double[2], sbGag = new double[2];
        foreach (var l in Lire(Path.Combine(dossierMcp, "charting-m-stats-ServeBasics.csv")))
        {
            if (!avecRally.Contains(l[0]) || l[2] is not ("1" or "2")) continue;
            int b = l[2] == "1" ? 0 : 1;
            double N(int i) => double.Parse(l[i], Ci);
            sbPts[b] += N(3); sbAces[b] += N(5); sbGag[b] += N(6) + N(7);
        }
        // ReturnOutcomes (côté relanceur) : retours ratés sur une balle retournable = fautes directes de retour, que le MCP
        // compte en fautes directes et pas en services gagnants. « Service gagnant » au sens du créateur = tout service hors ace
        // qui n'est pas remis en jeu, ces retours ratés compris.
        double[] fautesRetour = new double[2];
        foreach (var l in Lire(Path.Combine(dossierMcp, "charting-m-stats-ReturnOutcomes.csv")))
        {
            if (!avecRally.Contains(l[0]) || l[2] is not ("v1st" or "v2nd")) continue;
            fautesRetour[l[2] == "v1st" ? 0 : 1] += double.Parse(l[5], Ci) - double.Parse(l[7], Ci);
        }


        double total = pts.Values.Sum();
        var sb = new StringBuilder("mesure,valeur_pct,effectif,definition\n");
        void Ligne(string mesure, double num, double den, string def) =>
            sb.AppendLine(string.Format(Ci, "{0},{1:F2},{2:F0},\"{3}\"", mesure, 100 * num / den, den, def));

        foreach (var t in Tranches)
            Ligne($"part_{Nom(t)}", pts[t], total, $"points dont l'échange compte {Nom(t)} coups (service compris, faute finale exclue) / tous les points");
        foreach (var t in Tranches)
            Ligne($"serveur_{Nom(t)}", srv[t], pts[t], $"points gagnés par le serveur / points de {Nom(t)} coups");
        foreach (var t in Tranches.Append("total"))
        {
            double P(Dictionary<string, double> d) => t == "total" ? d.Values.Sum() : d[t];
            double den = t == "total" ? total : pts[t];
            string q = t == "total" ? "tous les points" : $"points de {Nom(t)} coups";
            Ligne($"gagnants_{Nom(t)}", P(gag), den, $"coups gagnants (aces compris) / {q}");
            Ligne($"provoquees_{Nom(t)}", P(prov), den, $"fautes provoquées (services non retournés compris) / {q}");
            Ligne($"directes_{Nom(t)}", P(dir), den, $"fautes directes (doubles fautes exclues) / {q}");
        }
        // Fins de point hors service (définition du créateur) : aces, services gagnants et doubles fautes exclus ; retours gagnants compris.
        double servis = aces + sbGag[0] + sbGag[1] + fautesRetour[0] + fautesRetour[1] + dfs, horsService = total - servis;
        Ligne("gagnants_hors_service", gag.Values.Sum() - aces, horsService, "coups gagnants hors aces / points non finis par le service (aces, services gagnants, doubles fautes exclus)");
        Ligne("provoquees_hors_service", prov.Values.Sum() - sbGag[0] - sbGag[1], horsService, "fautes provoquées hors services gagnants / points non finis par le service");
        Ligne("directes_hors_service", dir.Values.Sum() - fautesRetour[0] - fautesRetour[1], horsService, "fautes directes hors retours ratés / points non finis par le service");
        Ligne("premiere_ace", sbAces[0], sbPts[0], "aces / premières rentrées");
        Ligne("premiere_gagnant", sbGag[0] + fautesRetour[0], sbPts[0], "services gagnants (hors aces, non remis en jeu : non retournés, retour en faute provoquée ou directe) / premières rentrées");
        Ligne("premiere_faute_retour", fautesRetour[0], sbGag[0] + fautesRetour[0], "retours en faute directe / services gagnants sur première");
        Ligne("seconde_ace", sbAces[1], sbPts[1], "aces / points joués sur seconde");
        Ligne("seconde_gagnant", sbGag[1] + fautesRetour[1], sbPts[1], "services gagnants / points joués sur seconde");
        Ligne("seconde_faute_retour", fautesRetour[1], sbGag[1] + fautesRetour[1], "retours en faute directe / services gagnants sur seconde");
        Ligne("seconde_double_faute", dfs, svpt - firstIn, "doubles fautes / points joués sur seconde");
        Ligne("aces", aces, svpt, "aces / points au service");
        Ligne("doubles_fautes", dfs, svpt, "doubles fautes / points au service");
        Ligne("premieres_balles", firstIn, svpt, "premières rentrées / points au service");
        Ligne("points_gagnes_1re", firstWon, firstIn, "points gagnés sur 1re / premières rentrées");
        Ligne("points_gagnes_2e", secondWon, svpt - firstIn, "points gagnés sur 2e / points joués sur 2e (doubles fautes comprises)");
        Ligne("points_gagnes_service", firstWon + secondWon, svpt, "points gagnés au service / points au service");

        Directory.CreateDirectory(Path.GetDirectoryName(fichierSortie)!);
        File.WriteAllText(fichierSortie, sb.ToString());
        Console.WriteLine($"Match Charting Project, surface {surface}, {anMin}-{anMax} : {avecRally.Count} matchs, {total:F0} points.");
        Console.Write(sb);
        Console.WriteLine($"Écrit : {fichierSortie}");
    }

    private static void Ajouter(Dictionary<string, double> d, string cle, double v) => d[cle] = d.GetValueOrDefault(cle) + v;

    /// <summary>Lignes d'un CSV (en-tête sauté), champs entre guillemets gérés.</summary>
    private static IEnumerable<string[]> Lire(string chemin)
    {
        foreach (var ligne in File.ReadLines(chemin).Skip(1))
        {
            var champs = new List<string>();
            var cur = new StringBuilder();
            bool guillemets = false;
            foreach (char c in ligne)
            {
                if (c == '"') guillemets = !guillemets;
                else if (c == ',' && !guillemets) { champs.Add(cur.ToString()); cur.Clear(); }
                else cur.Append(c);
            }
            champs.Add(cur.ToString());
            yield return [.. champs];
        }
    }
}
