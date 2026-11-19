using System.Globalization;
using System.Text;

namespace Chelems.Donnees;

/// <summary>
/// Effet de l'enjeu du tournoi. Performance d'un joueur = son résultat au-delà de ce que prédit l'Elo du moment
/// (global + surface, FiveThirtyEight, calculé depuis 2000 avec Challengers et qualifications), calibré par format.
/// L'Elo neutralise le niveau de l'adversaire.
/// - Tableau : performance par tranche de rang du joueur et par niveau de tournoi (G, M, A = 250 et 500, F, O).
/// - Piste 3 (adversaires) : Grand Chelem − Masters 1000 à écart d'Elo égal (stratification par tranches d'écart).
/// - Piste 1 (forme effacée) : mauvais jours (queue gauche) et poids de la forme d'avant le tournoi, Grand Chelem contre Masters.
/// - Piste 2 (résilience) : balles de break sauvées − % de points gagnés au service du même match, et l'inverse au retour.
/// Usage : dotnet run --project outils/Chelems.Donnees -- enjeu
/// </summary>
internal static class Enjeu
{
    private static readonly CultureInfo Ci = CultureInfo.InvariantCulture;
    private static readonly string[] Niveaux = ["G", "M", "A", "F", "O"];
    private static readonly Dictionary<string, string> NomNiveau = new()
    { ["G"] = "Grand Chelem", ["M"] = "Masters 1000", ["A"] = "ATP 250/500", ["F"] = "Masters final", ["O"] = "Jeux olympiques" };
    private static readonly (string Nom, int Min, int Max)[] Tranches =
        [("n°1", 1, 1), ("2-5", 2, 5), ("6-10", 6, 10), ("11-20", 11, 20), ("21-50", 21, 50), ("51-100", 51, 100), ("101+", 101, int.MaxValue)];

    private sealed record Ligne(int Joueur, int Date, string Tournoi, string Niveau, int BestOf, int Rang, int RangAdv,
                                double EcartElo, double Rv, double Ra, double V, double Clutch, double ClutchRetour);

    public static void Executer(string source, string sortie)
    {
        Directory.CreateDirectory(sortie);
        var log = new StringBuilder();
        void Dire(string s) { Console.WriteLine(s); log.AppendLine(s); }
        var csv = new StringBuilder("analyse,tranche,niveau,valeur,erreur_type,effectif,unite\n");
        void L(string a, string t, string n, double v, double se, int eff, string u) =>
            csv.AppendLine(string.Create(Ci, $"{a},{t},{n},{v:F4},{se:F4},{eff},{u}"));

        var lignes = Construire(source, Dire);
        string Tranche(int r) => Tranches.First(t => r >= t.Min && r <= t.Max).Nom;

        // ------------------------------------------------------------ tableau principal
        Dire("=== Performance au-delà de l'Elo, par tranche de rang du joueur et niveau de tournoi");
        Dire("    victoire : points de % de victoire en plus de ce que prédit l'Elo ; avantage : points d'avantage au service.");
        Dire($"{"tranche",-8}" + string.Concat(Niveaux.Select(n => $"{NomNiveau[n],27}")));
        foreach (var t in Tranches)
        {
            var sb = new StringBuilder($"{t.Nom,-8}");
            foreach (var n in Niveaux)
            {
                var l = lignes.Where(x => x.Niveau == n && Tranche(x.Rang) == t.Nom).ToList();
                if (l.Count < 20) { sb.Append($"{"—",27}"); continue; }
                var (mv, sv) = Moyenne(l.Select(x => 100 * x.Rv));
                var (ma, sa) = Moyenne(l.Select(x => x.Ra));
                sb.Append(string.Create(Ci, $"{$"{mv:+0.0;-0.0}±{sv:F1} / {ma:+0.0;-0.0}±{sa:F1} ({l.Count})",27}"));
                L("victoire", t.Nom, n, mv, sv, l.Count, "pts de %");
                L("avantage", t.Nom, n, ma, sa, l.Count, "pts");
            }
            Dire(sb.ToString());
        }
        Dire("    (lecture : « victoire ± erreur-type / avantage ± erreur-type (matchs) »)\n");

        // ------------------------------------------------------------ piste 3 : adversaires
        Dire("=== Piste 3 : les adversaires. Grand Chelem − Masters 1000, à écart d'Elo égal (tranches d'écart pondérées comme en Grand Chelem)");
        (string Nom, double Min, double Max)[] ecarts = [("< 0", -9999, 0), ("0-100", 0, 100), ("100-200", 100, 200), ("200-300", 200, 300), ("300+", 300, 9999)];
        foreach (var t in Tranches.Take(5))
        {
            var g = lignes.Where(x => x.Niveau == "G" && Tranche(x.Rang) == t.Nom).ToList();
            var m = lignes.Where(x => x.Niveau == "M" && Tranche(x.Rang) == t.Nom).ToList();
            double dv = 0, da = 0, var_ = 0, vara = 0; int poids = 0;
            foreach (var (_, min, max) in ecarts)
            {
                var gb = g.Where(x => x.EcartElo >= min && x.EcartElo < max).ToList();
                var mb = m.Where(x => x.EcartElo >= min && x.EcartElo < max).ToList();
                if (gb.Count < 5 || mb.Count < 5) continue;
                var (gv, gsv) = Moyenne(gb.Select(x => 100 * x.Rv)); var (mv, msv) = Moyenne(mb.Select(x => 100 * x.Rv));
                var (ga, gsa) = Moyenne(gb.Select(x => x.Ra)); var (ma, msa) = Moyenne(mb.Select(x => x.Ra));
                dv += gb.Count * (gv - mv); da += gb.Count * (ga - ma);
                var_ += gb.Count * gb.Count * (gsv * gsv + msv * msv); vara += gb.Count * gb.Count * (gsa * gsa + msa * msa);
                poids += gb.Count;
            }
            double brutV = Moyenne(g.Select(x => 100 * x.Rv)).M - Moyenne(m.Select(x => 100 * x.Rv)).M;
            double medG = Mediane(g.Select(x => (double)x.RangAdv)), medM = Mediane(m.Select(x => (double)x.RangAdv));
            Dire(string.Create(Ci, $"  {t.Nom,-7} brut {brutV,5:+0.0;-0.0} pts de victoire ; à écart d'Elo égal : victoire {dv / poids,5:+0.0;-0.0} ± {Math.Sqrt(var_) / poids:F1}, " +
                                   $"avantage {da / poids,5:+0.0;-0.0} ± {Math.Sqrt(vara) / poids:F1} pts ; rang médian des adversaires : GC {medG:F0}, M1000 {medM:F0} ; " +
                                   $"écart d'Elo moyen : GC {g.Average(x => x.EcartElo):F0}, M1000 {m.Average(x => x.EcartElo):F0}"));
            L("gc_moins_m1000_ecart_elo_egal_victoire", t.Nom, "G-M", dv / poids, Math.Sqrt(var_) / poids, g.Count, "pts de %");
            L("gc_moins_m1000_ecart_elo_egal_avantage", t.Nom, "G-M", da / poids, Math.Sqrt(vara) / poids, g.Count, "pts");
        }
        Dire("");

        // ------------------------------------------------------------ piste 1 : effacer une mauvaise forme
        Dire("=== Piste 1 : forme effacée ? Joueurs du top 10 (rang du jour), Grand Chelem contre Masters 1000");
        double s2 = lignes.Select(x => x.Ra).Let(v => Variance(v));
        double seuil = Math.Sqrt(s2);
        foreach (var n in new[] { "G", "M", "A" })
        {
            var l = lignes.Where(x => x.Niveau == n && x.Rang <= 10).ToList();
            double gauche = 100.0 * l.Count(x => x.Ra < -seuil) / l.Count, droite = 100.0 * l.Count(x => x.Ra > seuil) / l.Count;
            double var1 = Variance(l.Select(x => x.Ra)) - l.Average(x => x.V);
            Dire(string.Create(Ci, $"  {NomNiveau[n],-14} : très mauvais jours (avantage < −{seuil:F0} pts) {gauche,4:F1} % ; très bons jours (> +{seuil:F0}) {droite,4:F1} % ; " +
                                   $"variance au-delà du hasard {var1,5:F1} pts² ({l.Count} matchs)"));
            L("mauvais_jours_top10", "1-10", n, gauche, double.NaN, l.Count, "%");
            L("bons_jours_top10", "1-10", n, droite, double.NaN, l.Count, "%");
            L("variance_top10", "1-10", n, var1, double.NaN, l.Count, "pts²");
        }
        // Contrôle : la baisse de variance en Grand Chelem touche-t-elle toutes les tranches (effet du Bo5) ou seulement le haut ?
        Dire("  Contrôle par tranche — variance au-delà du hasard (pts²) et très mauvais jours (%), GC / M1000 / 250-500 :");
        foreach (var t in Tranches)
        {
            var parts = new[] { "G", "M", "A" }.Select(n =>
            {
                var l = lignes.Where(x => x.Niveau == n && Tranche(x.Rang) == t.Nom).ToList();
                double v = Variance(l.Select(x => x.Ra)) - l.Average(x => x.V);
                L("variance_par_tranche", t.Nom, n, v, double.NaN, l.Count, "pts²");
                return string.Create(Ci, $"{v,5:F1} / {100.0 * l.Count(x => x.Ra < -seuil) / l.Count,4:F1} %");
            });
            Dire($"    {t.Nom,-7} " + string.Join("    ", parts));
        }
        // Forme d'avant : moyenne de l'avantage résiduel des matchs des 45 jours précédents dans d'autres tournois.
        var parJoueur = lignes.GroupBy(x => x.Joueur).ToDictionary(g => g.Key, g => g.OrderBy(x => x.Date).ToList());
        foreach (var n in new[] { "G", "M" })
        {
            var pts = new List<(double X, double Y)>();
            foreach (var l in parJoueur.Values)
                foreach (var x in l.Where(x => x.Niveau == n && x.Rang <= 10))
                {
                    var avant = l.Where(p => p.Tournoi != x.Tournoi && p.Date < x.Date && Jours(x.Date) - Jours(p.Date) <= 45).ToList();
                    if (avant.Count >= 3) pts.Add((avant.Average(p => p.Ra), x.Ra));
                }
            double mx = pts.Average(p => p.X), my = pts.Average(p => p.Y);
            double sxx = pts.Sum(p => (p.X - mx) * (p.X - mx)), b = pts.Sum(p => (p.X - mx) * (p.Y - my)) / sxx;
            double se = Math.Sqrt(pts.Sum(p => Math.Pow(p.Y - my - b * (p.X - mx), 2)) / (pts.Count - 2) / sxx);
            Dire(string.Create(Ci, $"  Poids de la forme des 45 jours précédents sur un match de {NomNiveau[n]} : pente {b:F3} ± {se:F3} ({pts.Count} matchs du top 10)"));
            L("pente_forme_avant_top10", "1-10", n, b, se, pts.Count, "");
        }
        Dire("");

        // ------------------------------------------------------------ piste 2 : résilience à la pression
        Dire("=== Piste 2 : balles de break. Au service : % sauvées − % de points gagnés au service du match. Au retour : % converties − % de points gagnés au retour.");
        Dire($"{"tranche",-8}" + string.Concat(Niveaux.Take(3).Select(n => $"{NomNiveau[n],28}")));
        foreach (var t in Tranches)
        {
            var sb = new StringBuilder($"{t.Nom,-8}");
            foreach (var n in Niveaux.Take(3))
            {
                var l = lignes.Where(x => x.Niveau == n && Tranche(x.Rang) == t.Nom).ToList();
                var cs = l.Where(x => !double.IsNaN(x.Clutch)).Select(x => x.Clutch).ToList();
                var cr = l.Where(x => !double.IsNaN(x.ClutchRetour)).Select(x => x.ClutchRetour).ToList();
                if (cs.Count < 20 || cr.Count < 20) { sb.Append($"{"—",28}"); continue; }
                var (ms, ss) = Moyenne(cs); var (mr, sr) = Moyenne(cr);
                sb.Append(string.Create(Ci, $"{$"{ms:+0.0;-0.0}±{ss:F1} / {mr:+0.0;-0.0}±{sr:F1}",28}"));
                L("clutch_service", t.Nom, n, ms, ss, cs.Count, "pts");
                L("clutch_retour", t.Nom, n, mr, sr, cr.Count, "pts");
            }
            Dire(sb.ToString());
        }
        Dire("    (lecture : « au service ± erreur-type / au retour ± erreur-type », en points de % ; matchs avec au moins 3 balles de break)\n");

        File.WriteAllText(Path.Combine(sortie, "resultats.csv"), csv.ToString());
        File.WriteAllText(Path.Combine(sortie, "rapport.txt"), log.ToString());
        Console.WriteLine($"Écrit dans {sortie}");
    }

    private static List<Ligne> Construire(string source, Action<string> dire)
    {
        var elo = new Elo();
        var obs = new List<Observation>();
        var meta = new List<MatchAtp>();
        foreach (var m in Sackmann.Charger(source, 2000, 2026))
        {
            if (!m.Complet) continue;
            int an = m.Date / 10000;
            if (m.CircuitPrincipal && Niveaux.Contains(m.Niveau) && an >= 2010 && m.PtsServiceG > 0 && m.PtsServiceP > 0
                && m.BestOf is 3 or 5 && m.Surface is "Hard" or "Clay" or "Grass")
            {
                bool aGagne = m.Gagnant < m.Perdant;
                int a = Math.Min(m.Gagnant, m.Perdant), b = Math.Max(m.Gagnant, m.Perdant);
                double sA = aGagne ? (double)m.GagnesServiceG / m.PtsServiceG : (double)m.GagnesServiceP / m.PtsServiceP;
                double sB = aGagne ? (double)m.GagnesServiceP / m.PtsServiceP : (double)m.GagnesServiceG / m.PtsServiceG;
                int nA = aGagne ? m.PtsServiceG : m.PtsServiceP, nB = aGagne ? m.PtsServiceP : m.PtsServiceG;
                obs.Add(new Observation(a, b, m.Date, m.BestOf, elo.Melange(a, m.Surface) - elo.Melange(b, m.Surface), aGagne,
                                        100 * (sA - sB), 1e4 * (sA * (1 - sA) / nA + sB * (1 - sB) / nB), sA, 1 - sB, nA, nB, m.Tournoi, m.Surface));
                meta.Add(m);
            }
            elo.MettreAJour(m);
        }
        Rivalites.Calibrer(obs, _ => { });
        dire($"Effet de l'enjeu — Jeff Sackmann tennis_atp, circuit principal hors Coupe Davis, 2010 à mai 2026, matchs complets avec statistiques : {obs.Count} matchs.");
        dire("Niveau du moment : Elo FiveThirtyEight (global + surface), calculé depuis 2000 avec Challengers et qualifications ; prédiction calibrée par format.\n");

        var lignes = new List<Ligne>(2 * obs.Count);
        for (int i = 0; i < obs.Count; i++)
        {
            var o = obs[i]; var m = meta[i];
            foreach (bool estA in new[] { true, false })
            {
                int j = estA ? o.A : o.B;
                bool estGagnant = j == m.Gagnant;
                double s = estA ? 1 : -1;
                // Résilience : balles de break sauvées au service (et converties au retour) comparées au % de points du match.
                int bs = estGagnant ? m.BbSauveesG : m.BbSauveesP, bf = estGagnant ? m.BbSubiesG : m.BbSubiesP;
                int obs_ = estGagnant ? m.BbSauveesP : m.BbSauveesG, obf = estGagnant ? m.BbSubiesP : m.BbSubiesG;
                double pctService = estA ? o.ServiceA : 1 - o.RetourA;
                double pctServiceAdv = estA ? 1 - o.RetourA : o.ServiceA;
                double clutch = bf >= 3 && bs >= 0 ? 100 * ((double)bs / bf - pctService) : double.NaN;
                double clutchR = obf >= 3 && obs_ >= 0 ? 100 * ((1 - (double)obs_ / obf) - (1 - pctServiceAdv)) : double.NaN;
                lignes.Add(new Ligne(j, m.Date, $"{m.Date / 10000}-{m.Tournoi}", m.Niveau, m.BestOf,
                                     estGagnant ? m.RangG : m.RangP, estGagnant ? m.RangP : m.RangG,
                                     s * o.EcartElo, s * o.ResVictoire, s * o.ResAvantage, o.VarianceBinomiale, clutch, clutchR));
            }
        }
        return lignes;
    }

    private static (double M, double Se) Moyenne(IEnumerable<double> x)
    {
        var l = x.ToList();
        double m = l.Average();
        return (m, Math.Sqrt(l.Sum(v => (v - m) * (v - m)) / (l.Count - 1) / l.Count));
    }

    private static double Variance(IEnumerable<double> x)
    {
        var l = x.ToList();
        double m = l.Average();
        return l.Sum(v => (v - m) * (v - m)) / (l.Count - 1);
    }

    private static double Mediane(IEnumerable<double> x)
    {
        var l = x.OrderBy(v => v).ToList();
        return l.Count == 0 ? double.NaN : l[l.Count / 2];
    }

    private static double Jours(int date) =>
        new DateTime(date / 10000, Math.Clamp(date / 100 % 100, 1, 12), Math.Clamp(date % 100, 1, 28)).Subtract(DateTime.MinValue).TotalDays;

    private static TR Let<T, TR>(this T x, Func<T, TR> f) => f(x);
}
