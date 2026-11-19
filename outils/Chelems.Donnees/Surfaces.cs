using System.Globalization;
using System.Text;

namespace Chelems.Donnees;

/// <summary>
/// Les styles comptent-ils selon la surface ? Même décomposition que <see cref="Rivalites"/>, sur dur, terre et gazon,
/// avec un niveau SANS information de surface (Elo toutes surfaces) : l'affinité de surface reste dans les résidus,
/// comme elle devra émerger des styles dans le jeu (principe 3 de CLAUDE.md).
/// Usage : dotnet run --project outils/Chelems.Donnees -- surfaces
/// </summary>
internal static class RivalitesSurfaces
{
    private static readonly string[] Surfaces = ["Hard", "Clay", "Grass"];
    private static readonly Dictionary<string, string> Nom = new() { ["Hard"] = "dur", ["Clay"] = "terre", ["Grass"] = "gazon" };
    private static readonly CultureInfo Ci = CultureInfo.InvariantCulture;

    public static void Executer(string source, string sortie, int depuis = 2015)
    {
        Directory.CreateDirectory(sortie);
        var log = new StringBuilder();
        void Dire(string s) { Console.WriteLine(s); log.AppendLine(s); }
        var bilan = new StringBuilder("surface,mesure,best_of,valeur,unite,definition\n");

        var noms = Sackmann.Noms(source);
        var elo = new Elo();
        var obs = new List<Observation>();
        foreach (var m in Sackmann.Charger(source, depuis - 10, 2025))
        {
            if (!m.Complet) continue;
            if (m.CircuitPrincipal && m.Niveau != "D" && Surfaces.Contains(m.Surface) && m.Date / 10000 >= depuis
                && m.PtsServiceG > 0 && m.BestOf is 3 or 5)
            {
                bool aGagne = m.Gagnant < m.Perdant;
                int a = Math.Min(m.Gagnant, m.Perdant), b = Math.Max(m.Gagnant, m.Perdant);
                double sA = aGagne ? (double)m.GagnesServiceG / m.PtsServiceG : (double)m.GagnesServiceP / m.PtsServiceP;
                double sB = aGagne ? (double)m.GagnesServiceP / m.PtsServiceP : (double)m.GagnesServiceG / m.PtsServiceG;
                int nA = aGagne ? m.PtsServiceG : m.PtsServiceP, nB = aGagne ? m.PtsServiceP : m.PtsServiceG;
                obs.Add(new Observation(a, b, m.Date, m.BestOf, elo.Global(a) - elo.Global(b), aGagne,
                                        100 * (sA - sB), 1e4 * (sA * (1 - sA) / nA + sB * (1 - sB) / nB),
                                        sA, 1 - sB, nA, nB, m.Tournoi, m.Surface));
            }
            elo.MettreAJour(m);
        }

        Dire($"Styles et surfaces — Jeff Sackmann tennis_atp, circuit principal hors Coupe Davis, {depuis}-2025, matchs complets.");
        Dire($"Niveau du moment : Elo toutes surfaces (aucune information de surface), calculé depuis {depuis - 10} avec Challengers et qualifications.");
        Dire(string.Join(", ", Surfaces.Select(s => $"{Nom[s]} {obs.Count(o => o.Surface == s)} matchs")) + "\n");

        foreach (var s in Surfaces)
        {
            Dire($"--- Calibrage {Nom[s]}");
            Rivalites.Calibrer(obs.Where(o => o.Surface == s).ToList(), Dire);
        }
        foreach (var s in Surfaces)
        {
            var sb = new StringBuilder();
            Rivalites.Decomposer(obs.Where(o => o.Surface == s).ToList(), 0, Dire, sb, $"{Nom[s]}, tous formats");
            foreach (var l in sb.ToString().Split('\n', StringSplitOptions.RemoveEmptyEntries)) bilan.AppendLine($"{s},{l.TrimEnd('\r')}");
        }

        EntreSurfaces(obs, Dire, bilan);
        PenteDeStyle(obs, Dire, bilan);
        Bascules(obs, noms, Dire, bilan, sortie);

        File.WriteAllText(Path.Combine(sortie, "decomposition.csv"), bilan.ToString());
        File.WriteAllText(Path.Combine(sortie, "rapport.txt"), log.ToString());
        Console.WriteLine($"\nÉcrit dans {sortie}");
    }

    /// <summary>
    /// Covariances du résidu entre deux matchs, sur la même surface ou sur deux surfaces différentes :
    /// - joueur (deux adversaires différents) : l'excès « même surface » mesure l'affinité de surface du joueur ;
    /// - paire : l'excès « même surface », moins la part des deux joueurs, mesure l'interaction des styles propre à une surface.
    /// </summary>
    private static void EntreSurfaces(List<Observation> obs, Action<string> dire, StringBuilder bilan)
    {
        // Joueur : résidus orientés vers lui.
        var parJoueur = new Dictionary<int, List<(int Adv, double R, double Rv, string S)>>();
        foreach (var o in obs)
        {
            Ajouter(o.A, (o.B, o.ResAvantage, o.ResVictoire, o.Surface));
            Ajouter(o.B, (o.A, -o.ResAvantage, -o.ResVictoire, o.Surface));
        }
        void Ajouter(int j, (int, double, double, string) x)
        {
            if (!parJoueur.TryGetValue(j, out var l)) parJoueur[j] = l = [];
            l.Add(x);
        }
        var jm = new Acc(); var jd = new Acc();
        foreach (var l in parJoueur.Values)
            for (int i = 0; i < l.Count; i++)
                for (int k = i + 1; k < l.Count; k++)
                {
                    if (l[i].Adv == l[k].Adv) continue;
                    (l[i].S == l[k].S ? jm : jd).Ajouter(l[i].R * l[k].R, l[i].Rv * l[k].Rv);
                }

        var pm = new Acc(); var pd = new Acc();
        foreach (var g in obs.GroupBy(o => (o.A, o.B)))
        {
            var l = g.ToList();
            for (int i = 0; i < l.Count; i++)
                for (int k = i + 1; k < l.Count; k++)
                    (l[i].Surface == l[k].Surface ? pm : pd).Ajouter(l[i].ResAvantage * l[k].ResAvantage, l[i].ResVictoire * l[k].ResVictoire);
        }

        double affinite = jm.Cov - jd.Cov;
        double interactionSurface = (pm.Cov - pd.Cov) - 2 * affinite;
        dire("=== Même surface ou surfaces différentes (covariance du résidu entre deux matchs)");
        dire($"  joueur, même surface        : {jm.N,8} couples, avantage {jm.Cov,5:F1} pts², victoires {jm.CovV:F4}");
        dire($"  joueur, surfaces différentes : {jd.N,8} couples, avantage {jd.Cov,5:F1} pts², victoires {jd.CovV:F4}");
        dire($"    → affinité de surface d'un joueur : variance {affinite:F1} pts² (écart-type {Racine(affinite)} pts)");
        dire($"  paire, même surface         : {pm.N,8} couples, avantage {pm.Cov,5:F1} pts², victoires {pm.CovV:F4}");
        dire($"  paire, surfaces différentes  : {pd.N,8} couples, avantage {pd.Cov,5:F1} pts², victoires {pd.CovV:F4}");
        dire($"    → interaction des styles propre à la surface (paire − 2 × affinités) : variance {interactionSurface:F1} pts²");
        dire("");
        void L(string m, double v, string def) => bilan.AppendLine(string.Create(Ci, $"toutes,{m},0,{v:F3},pts²,\"{def}\""));
        L("cov_joueur_meme_surface", jm.Cov, $"{jm.N} couples");
        L("cov_joueur_autre_surface", jd.Cov, $"{jd.N} couples");
        L("variance_affinite_surface", affinite, "cov joueur même surface − surfaces différentes");
        L("cov_paire_meme_surface", pm.Cov, $"{pm.N} couples");
        L("cov_paire_autre_surface", pd.Cov, $"{pd.N} couples");
        L("variance_interaction_styles_surface", interactionSurface, "(paire même − autre surface) − 2 × affinité");
    }

    /// <summary>
    /// Le style « serveur ou relanceur » explique-t-il le résidu selon la surface ?
    /// Style d'un joueur = (% points gagnés au service − moyenne de la surface) − (% au retour − moyenne de la surface),
    /// moyenné sur tous ses matchs de toutes surfaces, en retirant le match mesuré (pas de circularité).
    /// Pente du résidu de l'avantage (et de la victoire) sur l'écart de style A − B, par surface.
    /// </summary>
    private static void PenteDeStyle(List<Observation> obs, Action<string> dire, StringBuilder bilan)
    {
        var moyService = Surfaces.ToDictionary(s => s, s => obs.Where(o => o.Surface == s).Average(o => (o.ServiceA + (1 - o.RetourA)) / 2));
        double Ecart(Observation o, bool pourA) =>
            pourA ? (o.ServiceA - moyService[o.Surface]) - (o.RetourA - (1 - moyService[o.Surface]))
                  : ((1 - o.RetourA) - moyService[o.Surface]) - ((1 - o.ServiceA) - (1 - moyService[o.Surface]));
        var somme = new Dictionary<int, (double S, int N)>();
        foreach (var o in obs)
        {
            Ajout(o.A, Ecart(o, true));
            Ajout(o.B, Ecart(o, false));
        }
        void Ajout(int j, double v) { var x = somme.GetValueOrDefault(j); somme[j] = (x.S + v, x.N + 1); }
        double? Style(int j, double ceMatch)
        {
            var (s, n) = somme[j];
            return n >= 21 ? 100 * (s - ceMatch) / (n - 1) : null;
        }

        dire("=== Style serveur/relanceur et surface (pente du résidu sur l'écart de style, joueurs ≥ 20 autres matchs)");
        double etStyle = 0;
        foreach (var s in Surfaces)
        {
            var points = new List<(double D, double R, double Rv)>();
            foreach (var o in obs.Where(o => o.Surface == s))
            {
                var sa = Style(o.A, Ecart(o, true));
                var sb = Style(o.B, Ecart(o, false));
                if (sa is null || sb is null) continue;
                points.Add((sa.Value - sb.Value, o.ResAvantage, o.ResVictoire));
            }
            double sxx = points.Sum(p => p.D * p.D);
            double b = points.Sum(p => p.D * p.R) / sxx, bv = points.Sum(p => p.D * p.Rv) / sxx;
            double se = Math.Sqrt(points.Sum(p => (p.R - b * p.D) * (p.R - b * p.D)) / (points.Count - 1) / sxx);
            double sev = Math.Sqrt(points.Sum(p => (p.Rv - bv * p.D) * (p.Rv - bv * p.D)) / (points.Count - 1) / sxx);
            etStyle = Math.Sqrt(sxx / points.Count);
            dire(string.Create(Ci, $"  {Nom[s],-6} ({points.Count,5} matchs) : avantage {b,6:+0.00;-0.00} ± {se:F2} pt par point d'écart de style ; " +
                                   $"victoire {100 * bv,6:+0.0;-0.0} ± {100 * sev:F1} points de % par point d'écart de style"));
            bilan.AppendLine(string.Create(Ci, $"{s},pente_style_avantage,0,{b:F4},pts/pt,\"± {se:F4}\""));
            bilan.AppendLine(string.Create(Ci, $"{s},pente_style_victoire,0,{bv:F5},proba/pt,\"± {sev:F5}\""));
        }
        dire(string.Create(Ci, $"  (écart-type de l'écart de style entre deux adversaires : {etStyle:F1} pts)"));
        dire("");
    }

    /// <summary>
    /// Rivalités qui basculent selon la surface : paires avec au moins 3 matchs sur deux surfaces.
    /// z = (victoires − attendues par l'Elo) / écart-type de Bernoulli, par surface. Sans effet de surface, (z1 − z2)²/2 vaut 1 en moyenne.
    /// </summary>
    private static void Bascules(List<Observation> obs, Dictionary<int, string> noms, Action<string> dire, StringBuilder bilan, string sortie)
    {
        var csv = new StringBuilder("joueur_a,joueur_b,surface_1,matchs_1,victoires_a_1,attendues_a_1,surface_2,matchs_2,victoires_a_2,attendues_a_2,ecart_z\n");
        var stat = new List<double>();
        foreach (var g in obs.GroupBy(o => (o.A, o.B)))
        {
            var parS = g.GroupBy(o => o.Surface).Where(x => x.Count() >= 3).ToDictionary(x => x.Key, x => x.ToList());
            var cles = Surfaces.Where(parS.ContainsKey).ToList();
            for (int i = 0; i < cles.Count; i++)
                for (int k = i + 1; k < cles.Count; k++)
                {
                    var l1 = parS[cles[i]]; var l2 = parS[cles[k]];
                    double z1 = Z(l1), z2 = Z(l2);
                    stat.Add((z1 - z2) * (z1 - z2) / 2);
                    csv.AppendLine(string.Create(Ci,
                        $"\"{noms.GetValueOrDefault(g.Key.A)}\",\"{noms.GetValueOrDefault(g.Key.B)}\",{cles[i]},{l1.Count},{l1.Count(o => o.AGagne)},{l1.Sum(o => o.PrbVictoire):F2},{cles[k]},{l2.Count},{l2.Count(o => o.AGagne)},{l2.Sum(o => o.PrbVictoire):F2},{z1 - z2:F2}"));
                }
        }
        static double Z(List<Observation> l) => l.Sum(o => o.ResVictoire) / Math.Sqrt(l.Sum(o => o.PrbVictoire * (1 - o.PrbVictoire)));
        double moyenne = stat.Average();
        dire($"=== Rivalités vues au moins 3 fois sur deux surfaces : {stat.Count} comparaisons");
        dire(string.Create(Ci, $"  (z1 − z2)²/2 moyen = {moyenne:F2} (1 = aucun effet de surface sur le bilan, au-delà de l'Elo toutes surfaces) ; " +
                               $"part des comparaisons significatives au seuil de 5 % : {100.0 * stat.Count(x => x > 3.84) / stat.Count:F0} % (5 % attendus par hasard)"));
        File.WriteAllText(Path.Combine(sortie, "bascules_par_surface.csv"), csv.ToString());
        bilan.AppendLine(string.Create(Ci, $"toutes,bascule_z_moyen,0,{moyenne:F3},,\"{stat.Count} comparaisons ; 1 = pas d'effet\""));

        dire("\n=== Exemples : bilan par surface (attendu par l'Elo toutes surfaces entre parenthèses)");
        (string, string)[] duels = [("Rafael Nadal", "Roger Federer"), ("Rafael Nadal", "Novak Djokovic"), ("Novak Djokovic", "Roger Federer"),
                                    ("Rafael Nadal", "Dominic Thiem"), ("Carlos Alcaraz", "Jannik Sinner"), ("Novak Djokovic", "Andy Murray"),
                                    ("Rafael Nadal", "Alexander Zverev"), ("Daniil Medvedev", "Rafael Nadal")];
        foreach (var (x, y) in duels)
        {
            int a = noms.FirstOrDefault(kv => kv.Value == x).Key, b = noms.FirstOrDefault(kv => kv.Value == y).Key;
            bool inverse = a > b;
            var l = obs.Where(o => o.A == Math.Min(a, b) && o.B == Math.Max(a, b)).ToList();
            var parts = Surfaces.Select(s =>
            {
                var m = l.Where(o => o.Surface == s).ToList();
                if (m.Count == 0) return null;
                int v = m.Count(o => o.AGagne ^ inverse);
                double att = m.Sum(o => inverse ? 1 - o.PrbVictoire : o.PrbVictoire);
                return string.Create(Ci, $"{Nom[s]} {v}-{m.Count - v} ({att:F1}-{m.Count - att:F1})");
            }).Where(p => p is not null);
            dire($"  {x} – {y} : {string.Join(" ; ", parts)}");
        }
    }

    private static string Racine(double v) => v > 0 ? Math.Sqrt(v).ToString("F2", Ci) : "—";

    private sealed class Acc
    {
        private double _s, _sv;
        public long N { get; private set; }
        public void Ajouter(double r, double rv) { _s += r; _sv += rv; N++; }
        public double Cov => _s / N;
        public double CovV => _sv / N;
    }
}
