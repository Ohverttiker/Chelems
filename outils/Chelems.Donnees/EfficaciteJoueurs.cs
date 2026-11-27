using System.Globalization;
using System.Text;

namespace Chelems.Donnees;

/// <summary>
/// Efficacité au service et au retour, joueur par joueur (roadmap, étape 1), tirée du Match Charting Project (hommes),
/// avec la taille et la main de l'ATP (atp_players.csv).
///
/// Au service, par balle (première rentrée, seconde jouée) : aces, services gagnants (au sens du créateur : hors ace, non remis
/// en jeu, c'est-à-dire non retourné, retour en faute provoquée ou raté sur une balle retournable), retours gagnants subis,
/// doubles fautes, points gagnés. Au retour : les mêmes issues, vues depuis le relanceur face à tous ses adversaires.
/// Sources : charting-m-stats-ServeBasics.csv (serveur), charting-m-stats-ReturnOutcomes.csv (relanceur : retournables,
/// remis en jeu, retours gagnants), charting-m-stats-Overview.csv (doubles fautes).
///
/// Usage : dotnet run --project outils/Chelems.Donnees -- efficacite-joueurs [Hard|Clay|Grass|Toutes] [annee_min] [annee_max] [points_min]
/// </summary>
internal static class EfficaciteJoueurs
{
    private static readonly CultureInfo Ci = CultureInfo.InvariantCulture;

    /// <summary>Compteurs d'un joueur dans un rôle (au service ou au retour), par balle [0 première rentrée, 1 seconde jouée].</summary>
    private sealed class Compteurs
    {
        public readonly double[] Points = new double[2], Aces = new double[2], Gagnants = new double[2], RetoursGagnants = new double[2], Gagnes = new double[2];
        public double DoublesFautes, PointsService;
        public readonly HashSet<string> Matchs = [];
    }

    internal static void Executer(string dossierMcp, string dossierAtp, string surface, int anMin, int anMax, int pointsMin, string fichierSortie)
    {
        // Matchs retenus, et les deux joueurs de chacun.
        var joueurs = new Dictionary<string, (string A, string B)>();
        var mains = new Dictionary<string, string>();
        foreach (var l in Csv.Lire(Path.Combine(dossierMcp, "charting-m-matches.csv")))
        {
            if (l.Length < 11 || (surface != "Toutes" && l[10] != surface)) continue;
            if (!int.TryParse(l[0].AsSpan(0, 4), NumberStyles.Integer, Ci, out int an) || an < anMin || an > anMax) continue;
            joueurs[l[0]] = (l[1], l[2]);
            mains.TryAdd(l[1], l[3]);
            mains.TryAdd(l[2], l[4]);
        }
        string Adversaire(string match, string joueur) => joueurs[match].A == joueur ? joueurs[match].B : joueurs[match].A;

        // Retours (côté relanceur) : retours ratés sur balle retournable et retours gagnants, par match, relanceur et balle.
        var retours = new Dictionary<(string, string, int), (double Rates, double Gagnants)>();
        foreach (var l in Csv.Lire(Path.Combine(dossierMcp, "charting-m-stats-ReturnOutcomes.csv")))
        {
            if (!joueurs.ContainsKey(l[0]) || l[2] is not ("v1st" or "v2nd")) continue;
            double N(int i) => double.Parse(l[i], Ci);
            retours[(l[0], l[1], l[2] == "v1st" ? 0 : 1)] = (N(5) - N(7), N(9));
        }

        var service = new Dictionary<string, Compteurs>();
        var retour = new Dictionary<string, Compteurs>();
        Compteurs De(Dictionary<string, Compteurs> d, string j) => d.TryGetValue(j, out var c) ? c : d[j] = new Compteurs();
        foreach (var l in Csv.Lire(Path.Combine(dossierMcp, "charting-m-stats-ServeBasics.csv")))
        {
            if (!joueurs.ContainsKey(l[0]) || l[2] is not ("1" or "2")) continue;
            string adv = Adversaire(l[0], l[1]);
            int b = l[2] == "1" ? 0 : 1;
            if (!retours.TryGetValue((l[0], adv, b), out var r)) continue;
            double N(int i) => double.Parse(l[i], Ci);
            foreach (var c in new[] { De(service, l[1]), De(retour, adv) })
            {
                c.Points[b] += N(3); c.Gagnes[b] += N(4); c.Aces[b] += N(5);
                c.Gagnants[b] += N(6) + N(7) + r.Rates; c.RetoursGagnants[b] += r.Gagnants;
                c.Matchs.Add(l[0]);
            }
        }
        foreach (var l in Csv.Lire(Path.Combine(dossierMcp, "charting-m-stats-Overview.csv")))
        {
            if (!joueurs.ContainsKey(l[0]) || l[2] != "Total" || !service.TryGetValue(l[1], out var c) || !c.Matchs.Contains(l[0])) continue;
            c.DoublesFautes += double.Parse(l[5], Ci);
            c.PointsService += double.Parse(l[3], Ci);
            De(retour, Adversaire(l[0], l[1])).DoublesFautes += double.Parse(l[5], Ci);
        }

        // Taille (cm) de l'ATP, par nom complet ; plusieurs homonymes : la plus récente fiche renseignée.
        var tailles = new Dictionary<string, string>();
        foreach (var l in Csv.Lire(Path.Combine(dossierAtp, "atp_players.csv")))
            if (l.Length > 6 && l[6].Length > 0) tailles[$"{l[1]} {l[2]}"] = l[6];

        var sb = new StringBuilder();
        sb.AppendLine("joueur,taille_cm,main,matchs,points_service,premieres," +
                      "p1_ace,p1_gagnant,p1_retour_gagnant,p1_gagnes,p2_ace,p2_gagnant,p2_retour_gagnant,p2_double_faute,p2_gagnes," +
                      "matchs_retour,points_retour,r1_ace,r1_gagnant,r1_retour_gagnant,r1_gagnes,r2_ace,r2_gagnant,r2_retour_gagnant,r2_double_faute,r2_gagnes");
        int n = 0;
        foreach (var (j, s) in service.OrderByDescending(kv => kv.Value.Points.Sum()))
        {
            if (s.Points.Sum() < pointsMin || !retour.TryGetValue(j, out var r) || r.Points.Sum() < pointsMin) continue;
            static string P(double num, double den) => den > 0 ? (100 * num / den).ToString("F2", Ci) : "";
            string Role(Compteurs c) => string.Join(",",
                c.Matchs.Count, c.Points.Sum(),
                P(c.Aces[0], c.Points[0]), P(c.Gagnants[0], c.Points[0]), P(c.RetoursGagnants[0], c.Points[0]), P(c.Gagnes[0], c.Points[0]),
                P(c.Aces[1], c.Points[1]), P(c.Gagnants[1], c.Points[1]), P(c.RetoursGagnants[1], c.Points[1]), P(c.DoublesFautes, c.Points[1]),
                P(c.Gagnes[1], c.Points[1]));
            // Au retour, « gagnés » = points gagnés par le serveur adverse : on garde la vue du serveur pour comparer les deux rôles.
            var roleService = Role(s).Split(',');
            sb.AppendLine(string.Join(",", Csv.Champ(j), tailles.GetValueOrDefault(j, ""), mains.GetValueOrDefault(j, ""),
                                      roleService[0], roleService[1], P(s.Points[0], s.Points.Sum()),
                                      string.Join(",", roleService[2..]), Role(r)));
            n++;
        }
        Directory.CreateDirectory(Path.GetDirectoryName(fichierSortie)!);
        File.WriteAllText(fichierSortie, sb.ToString());
        Console.WriteLine($"Match Charting Project, surface {surface}, {anMin}-{anMax} : {n} joueurs avec au moins {pointsMin} points au service et au retour.");
        Console.WriteLine($"Écrit : {fichierSortie}");
    }
}
