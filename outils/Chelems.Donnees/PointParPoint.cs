using System.Globalization;
using System.Text;

namespace Chelems.Donnees;

/// <summary>
/// Test point par point (Match Charting Project, Jeff Sackmann) : la dispersion d'un match à l'autre vient-elle
/// d'un niveau fixe pour tout le match, ou d'une dynamique à l'intérieur du match (élan, pression) ?
///
/// A. Variation de l'avantage au service d'un set à l'autre du même match. Référence : le même match rejoué
///    avec les vraies règles et, pour chaque joueur, un % au service constant sur tout le match (son % du match).
///    La référence contient donc le hasard des points ET les effets de sélection dus à la fin du match
///    (un match en 3 sets a forcément deux premiers sets partagés).
/// B. Élan. B1 point précédent, B2 jeu de service précédent, B3 juste après avoir breaké : statistiques centrées
///    sur le joueur dans le set (le niveau du set se retire), référence = le set rejoué avec des points indépendants.
///    B4 set précédent : référence = le match rejoué avec les niveaux de ses sets dans un ordre tiré au hasard.
/// C. Pression : % de points gagnés au service selon l'enjeu, moins le % du même serveur dans le même set.
///
/// Usage : dotnet run --project outils/Chelems.Donnees -- points
/// </summary>
internal static class PointParPoint
{
    private static readonly CultureInfo Ci = CultureInfo.InvariantCulture;
    private const int Repliques = 20;

    internal static readonly string[] Categories =
        ["balle de set/match contre le serveur", "balle de set/match pour le serveur", "tie-break (autres points)",
         "balle de break", "balle de jeu", "30-30 et égalité", "autres points"];

    /// <summary>Un point vu du serveur. S, R = score du jeu (0, 15, 30, 40, AV = 0..4) ou du tie-break ; jeux avant le point.</summary>
    internal readonly record struct Point(int Serveur, bool ServeurGagne, bool TieBreak, int S, int R, int JeuxS, int JeuxR);

    internal sealed record SetJoue(string Match, int Indice, string Surface, List<Point> Points, bool AvecTieBreak, int DeclencheurTb, int CibleTb)
    {
        public int Gagnant => Points[^1].ServeurGagne ? Points[^1].Serveur : 1 - Points[^1].Serveur;
        public int PremierServeur => Points[0].Serveur;
    }

    internal sealed record MatchJoue(string Id, int BestOf, string FinalTb, List<SetJoue> Sets);

    public static void Executer(string dossier, string sortie)
    {
        Directory.CreateDirectory(sortie);
        var log = new StringBuilder();
        void Dire(string s) { Console.WriteLine(s); log.AppendLine(s); }

        var (sets, matchs, stats) = Charger(dossier);
        Dire("Test point par point — Jeff Sackmann, Match Charting Project (hommes, tous les fichiers charting-m-points).");
        Dire($"{stats.Matchs} matchs lus, {stats.SetsLus} sets ; {sets.Count} sets complets et cohérents " +
             $"(dur {sets.Count(s => s.Surface == "Hard")}, terre {sets.Count(s => s.Surface == "Clay")}, gazon {sets.Count(s => s.Surface == "Grass")}), " +
             $"dont {matchs.Count} matchs entièrement notés.");
        Dire($"Écartés : {stats.SetsIncomplets} sets incomplets ou incohérents, {stats.PointsIllisibles} points illisibles.\n");

        var bilan = new StringBuilder("test,mesure,observe,reference,ecart,erreur_type,unite\n");
        var rng = new Random(1);
        TestA(matchs, rng, Dire, bilan);
        TestsBC(sets, rng, Dire, bilan);

        File.WriteAllText(Path.Combine(sortie, "resultats.csv"), bilan.ToString());
        File.WriteAllText(Path.Combine(sortie, "rapport.txt"), log.ToString());
        Console.WriteLine($"\nÉcrit dans {sortie}");
    }

    // ================================================================ A et B4 : à l'échelle du match

    private static void TestA(List<MatchJoue> matchs, Random rng, Action<string> dire, StringBuilder bilan)
    {
        // Observé.
        var (intraObs, invS) = VarianceIntra(matchs.Select(m => m.Sets.Select(AvantageSet).ToList()));
        var (intraSrvObs, _) = VarianceIntraServeur(matchs.Select(m => m.Sets));
        double b4Obs = ElanEntreSets(matchs.Select(m => m.Sets));

        // Référence A : % au service constant sur le match.
        var refA = new List<double>(); var refSrv = new List<double>();
        for (int r = 0; r < Repliques; r++)
        {
            var sims = matchs.Select(m =>
            {
                var p = TauxMatch(m.Sets.SelectMany(s => s.Points));
                return SimulerMatch(m, _ => p, rng);
            }).ToList();
            refA.Add(VarianceIntra(sims.Select(m => m.Select(AvantageSet).ToList())).Intra);
            refSrv.Add(VarianceIntraServeur(sims).Intra);
        }
        double intraRef = refA.Average(), intraEt = Et(refA);
        double excesA = intraObs - intraRef;
        double tauSrv = Math.Max(0, intraSrvObs - refSrv.Average()) / 1e4; // variance vraie d'un set à l'autre, % au service d'un joueur (proba²)

        // Référence B4 : niveaux des sets (débruités) dans un ordre au hasard.
        var refB4 = new List<double>();
        for (int r = 0; r < Repliques; r++)
        {
            var sims = matchs.Select(m =>
            {
                var pm = TauxMatch(m.Sets.SelectMany(s => s.Points));
                var niveaux = m.Sets.Select(s => Debruiter(s, pm, tauSrv)).OrderBy(_ => rng.Next()).ToList();
                return SimulerMatch(m, i => i < niveaux.Count ? niveaux[i] : niveaux[rng.Next(niveaux.Count)], rng);
            }).ToList();
            refB4.Add(ElanEntreSets(sims));
        }

        dire("=== A. L'avantage au service varie-t-il d'un set à l'autre du même match ?");
        dire(string.Create(Ci, $"  Variance d'un set à l'autre : observé {intraObs:F1} pts², référence « niveau constant sur le match » {intraRef:F1} ± {intraEt:F1}"));
        dire(string.Create(Ci, $"    → variation réelle du niveau d'un set à l'autre : {excesA:F1} pts² (écart-type {Racine(excesA)} pts d'avantage au service)"));
        dire(string.Create(Ci, $"    → par joueur : écart-type {100 * Math.Sqrt(tauSrv):F2} pts de % au service d'un set à l'autre"));
        dire(string.Create(Ci, $"  Part de la variance d'un match entier due à cette variation (moyenne sur les sets) : {excesA * invS:F1} pts²"));
        dire("    (à comparer aux ≈ 85 pts² par match, après Elo, sur les matchs ATP sur dur : le reste est un niveau fixe pour tout le match)");
        dire(string.Create(Ci, $"B4. Élan entre sets : avantage du vainqueur d'un set au set suivant − au set précédent : observé {b4Obs:+0.00;-0.00}, " +
                               $"référence (sets dans un ordre au hasard) {refB4.Average():+0.00;-0.00} ± {Et(refB4):F2} → écart {b4Obs - refB4.Average():+0.00;-0.00} pts"));
        dire("");
        L(bilan, "A", "variance_intra_match_entre_sets", intraObs, intraRef, excesA, intraEt, "pts²");
        L(bilan, "A", "contribution_a_la_variance_match", excesA * invS, double.NaN, double.NaN, double.NaN, "pts²");
        L(bilan, "A", "ecart_type_set_a_set_par_joueur", 100 * Math.Sqrt(tauSrv), double.NaN, double.NaN, double.NaN, "pts de %");
        L(bilan, "B4", "elan_entre_sets", b4Obs, refB4.Average(), b4Obs - refB4.Average(), Et(refB4), "pts");
    }

    /// <summary>Variance d'un set à l'autre dans un même match, corrigée du hasard binomial (la référence corrige le reste).</summary>
    private static (double Intra, double InverseS) VarianceIntra(IEnumerable<List<(double A, double V)>> matchs)
    {
        double num = 0; int den = 0; double inv = 0; int n = 0;
        foreach (var m in matchs)
        {
            if (m.Count < 2) continue;
            double moy = m.Average(x => x.A);
            num += m.Sum(x => (x.A - moy) * (x.A - moy)) - (1 - 1.0 / m.Count) * m.Sum(x => x.V);
            den += m.Count - 1;
            inv += 1.0 / m.Count;
            n++;
        }
        return (num / den, inv / n);
    }

    private static (double Intra, double InverseS) VarianceIntraServeur(IEnumerable<List<SetJoue>> matchs) =>
        VarianceIntra(matchs.SelectMany(m => new[] { 0, 1 }.Select(j => m.Select(s =>
        {
            var (n, w) = Compter(s.Points, j);
            double p = (double)w / n;
            return (100 * p, 1e4 * p * (1 - p) / n);
        }).ToList())));

    /// <summary>Avantage du vainqueur du set k au set k+1 moins au set k−1.</summary>
    private static double ElanEntreSets(IEnumerable<List<SetJoue>> matchs)
    {
        double s = 0; int n = 0;
        foreach (var l in matchs)
            for (int k = 1; k + 1 < l.Count; k++)
            {
                double signe = l[k].Gagnant == 0 ? 1 : -1;
                s += signe * (AvantageSet(l[k + 1]).A - AvantageSet(l[k - 1]).A);
                n++;
            }
        return s / n;
    }

    private static double[] TauxMatch(IEnumerable<Point> points)
    {
        var l = points.ToList();
        return [Taux(l, 0), Taux(l, 1)];
    }

    /// <summary>% au service du set ramené vers le % du match (rétrécissement bayésien) pour ne pas compter deux fois le hasard.</summary>
    private static double[] Debruiter(SetJoue s, double[] pm, double tau)
    {
        var r = new double[2];
        for (int j = 0; j < 2; j++)
        {
            var (n, w) = Compter(s.Points, j);
            double p = (double)w / n, v = p * (1 - p) / n;
            double k = tau + v > 0 ? Math.Sqrt(tau / (tau + v)) : 0;
            r[j] = Math.Clamp(pm[j] + k * (p - pm[j]), 0.05, 0.95);
        }
        return r;
    }

    // ================================================================ B1-B3 et C : à l'intérieur du set

    private sealed class Compteurs
    {
        public readonly double[] SommeCat = new double[Categories.Length];
        public readonly int[] NCat = new int[Categories.Length];
        // Covariances centrées sur le joueur dans le set, et variance de la variable explicative.
        public double CovPoint, VarPoint, CovJeu, VarJeu, CovBreak, VarBreak;
        public int NPoint, NJeu, NBreak;

        public double Ecart(int c) => SommeCat[c] / NCat[c];
        public double DPoint => CovPoint / VarPoint;
        public double DJeu => CovJeu / VarJeu;
        public double DBreak => CovBreak / VarBreak;
    }

    private static void TestsBC(List<SetJoue> sets, Random rng, Action<string> dire, StringBuilder bilan)
    {
        var obs = new Compteurs();
        foreach (var s in sets) Analyser(s, obs);
        var nulles = new List<Compteurs>();
        for (int r = 0; r < Repliques; r++)
        {
            var c = new Compteurs();
            foreach (var s in sets)
            {
                var p = TauxMatch(s.Points);
                var (sim, _) = SimulerSet(s.Match, s.Indice, s.Surface, s.PremierServeur, s.AvecTieBreak, s.DeclencheurTb, s.CibleTb, p, rng);
                Analyser(sim, c);
            }
            nulles.Add(c);
        }

        void Ligne(string test, string nom, Func<Compteurs, double> f, int n)
        {
            double o = f(obs);
            var v = nulles.Select(f).ToList();
            double refv = v.Average(), et = Et(v);
            dire(string.Create(Ci, $"  {nom,-56} observé {100 * o,6:+0.00;-0.00}  indépendance {100 * refv,6:+0.00;-0.00}  écart {100 * (o - refv),6:+0.00;-0.00} ± {100 * et:F2} pts de %  ({n} cas)"));
            L(bilan, test, nom, 100 * o, 100 * refv, 100 * (o - refv), 100 * et, "pts de %");
        }

        dire("=== B. Élan dans le set (à niveau du joueur égal dans ce set)");
        Ligne("B1", "point gagné après point gagné − après point perdu", c => c.DPoint, obs.NPoint);
        Ligne("B2", "jeu tenu après jeu de service tenu − après break subi", c => c.DJeu, obs.NJeu);
        Ligne("B3", "jeu tenu juste après avoir breaké − après jeu tenu adverse", c => c.DBreak, obs.NBreak);
        dire("");
        dire("=== C. Pression : % de points gagnés au service selon l'enjeu, moins le % du même serveur dans le même set");
        for (int k = 0; k < Categories.Length; k++)
        {
            int kk = k;
            Ligne("C", Categories[k], c => c.Ecart(kk), obs.NCat[k]);
        }
        dire("");
    }

    private static void Analyser(SetJoue s, Compteurs c)
    {
        int[] n = new int[2], w = new int[2];
        foreach (var p in s.Points) { n[p.Serveur]++; if (p.ServeurGagne) w[p.Serveur]++; }
        if (n[0] < 10 || n[1] < 10) return;
        double[] taux = [(double)w[0] / n[0], (double)w[1] / n[1]];

        // C : écart au % du serveur dans le set, sans le point lui-même.
        foreach (var p in s.Points)
        {
            int y = p.ServeurGagne ? 1 : 0;
            double baseLoo = (double)(w[p.Serveur] - y) / (n[p.Serveur] - 1);
            int k = Categorie(p, s);
            c.SommeCat[k] += y - baseLoo;
            c.NCat[k]++;
        }

        // B1 : deux points consécutifs du même jeu de service, centrés sur le % du serveur dans le set.
        for (int i = 1; i < s.Points.Count; i++)
        {
            var a = s.Points[i - 1]; var b = s.Points[i];
            if (a.TieBreak || b.TieBreak || a.JeuxS != b.JeuxS || a.JeuxR != b.JeuxR || a.Serveur != b.Serveur) continue;
            double t = taux[a.Serveur];
            c.CovPoint += ((a.ServeurGagne ? 1 : 0) - t) * ((b.ServeurGagne ? 1 : 0) - t);
            c.VarPoint += t * (1 - t);
            c.NPoint++;
        }

        // Jeux hors tie-break.
        var jeux = new List<(int Serveur, bool Tenu)>();
        for (int i = 0; i < s.Points.Count; i++)
        {
            var p = s.Points[i];
            if (p.TieBreak) continue;
            bool dernier = i + 1 == s.Points.Count || s.Points[i + 1].TieBreak
                           || s.Points[i + 1].Serveur != p.Serveur || s.Points[i + 1].JeuxS != p.JeuxS || s.Points[i + 1].JeuxR != p.JeuxR;
            if (dernier) jeux.Add((p.Serveur, p.ServeurGagne));
        }
        double[] tenus = new double[2]; int[] nj = new int[2];
        foreach (var j in jeux) { nj[j.Serveur]++; if (j.Tenu) tenus[j.Serveur]++; }
        if (nj[0] < 2 || nj[1] < 2) return;
        double[] h = [tenus[0] / nj[0], tenus[1] / nj[1]];
        for (int i = 1; i < jeux.Count; i++)
        {
            int sv = jeux[i].Serveur;
            double y = (jeux[i].Tenu ? 1 : 0) - h[sv];
            if (jeux[i - 1].Serveur != sv)
            {
                // B3 : x = l'adversaire vient d'être breaké.
                int adv = 1 - sv;
                double xb = 1 - h[adv];
                c.CovBreak += ((jeux[i - 1].Tenu ? 0 : 1) - xb) * y;
                c.VarBreak += xb * (1 - xb);
                c.NBreak++;
            }
            if (i >= 2 && jeux[i - 2].Serveur == sv)
            {
                // B2 : x = son jeu de service précédent tenu.
                c.CovJeu += ((jeux[i - 2].Tenu ? 1 : 0) - h[sv]) * y;
                c.VarJeu += h[sv] * (1 - h[sv]);
                c.NJeu++;
            }
        }
    }

    internal static int Categorie(Point p, SetJoue s)
    {
        if (p.TieBreak)
        {
            bool pourS = p.S + 1 >= s.CibleTb && p.S + 1 - p.R >= 2;
            bool pourR = p.R + 1 >= s.CibleTb && p.R + 1 - p.S >= 2;
            return pourR ? 0 : pourS ? 1 : 2;
        }
        bool balleDeJeu = p.S >= 3 && p.S > p.R, balleDeBreak = p.R >= 3 && p.R > p.S;
        if (balleDeBreak && GagneSet(p.JeuxR, p.JeuxS)) return 0;
        if (balleDeJeu && GagneSet(p.JeuxS, p.JeuxR)) return 1;
        if (balleDeBreak) return 3;
        if (balleDeJeu) return 4;
        if (p.S == p.R && p.S >= 2) return 5;
        return 6;
    }

    private static bool GagneSet(int jeux, int adverses) => jeux + 1 >= 6 && jeux + 1 - adverses >= 2;

    // ================================================================ simulation (points indépendants, vraies règles)

    private static (SetJoue Set, int ProchainServeur) SimulerSet(string match, int indice, string surface, int premier,
                                                                 bool avecTb, int declencheur, int cible, double[] prob, Random rng)
    {
        var pts = new List<Point>(80);
        int[] jeux = [0, 0];
        int serveur = premier;
        while (true)
        {
            if (avecTb && jeux[0] == declencheur && jeux[1] == declencheur)
            {
                int[] tb = [0, 0];
                for (int k = 0; ; k++)
                {
                    int sv = k % 4 is 0 or 3 ? serveur : 1 - serveur;
                    bool g = rng.NextDouble() < prob[sv];
                    pts.Add(new Point(sv, g, true, tb[sv], tb[1 - sv], jeux[sv], jeux[1 - sv]));
                    tb[g ? sv : 1 - sv]++;
                    if (Math.Max(tb[0], tb[1]) >= cible && Math.Abs(tb[0] - tb[1]) >= 2) break;
                }
                var set = new SetJoue(match, indice, surface, pts, avecTb, declencheur, cible);
                return (set, 1 - serveur);
            }
            int ps = 0, pr = 0;
            while (true)
            {
                bool g = rng.NextDouble() < prob[serveur];
                pts.Add(new Point(serveur, g, false, ps, pr, jeux[serveur], jeux[1 - serveur]));
                if (g) ps++; else pr++;
                if (ps >= 3 && pr >= 3 && ps == pr) { ps = pr = 3; }      // égalité
                if (ps >= 4 && ps - pr >= 2) { jeux[serveur]++; break; }
                if (pr >= 4 && pr - ps >= 2) { jeux[1 - serveur]++; break; }
            }
            serveur = 1 - serveur;
            if (Math.Max(jeux[0], jeux[1]) >= 6 && Math.Abs(jeux[0] - jeux[1]) >= 2)
                return (new SetJoue(match, indice, surface, pts, avecTb, declencheur, cible), serveur);
        }
    }

    /// <summary>Rejoue un match entier : mêmes règles par set (celles des sets notés, sinon celles du format), niveaux par set donnés.</summary>
    private static List<SetJoue> SimulerMatch(MatchJoue m, Func<int, double[]> niveau, Random rng)
    {
        int gagnants = (m.BestOf + 1) / 2;
        int[] setsGagnes = [0, 0];
        var res = new List<SetJoue>();
        int serveur = m.Sets[0].PremierServeur;
        for (int i = 0; setsGagnes[0] < gagnants && setsGagnes[1] < gagnants; i++)
        {
            bool avecTb; int declencheur, cible;
            if (i < m.Sets.Count) (avecTb, declencheur, cible) = (m.Sets[i].AvecTieBreak, m.Sets[i].DeclencheurTb, m.Sets[i].CibleTb);
            else if (i == m.BestOf - 1) (avecTb, declencheur, cible) = m.FinalTb == "A" ? (false, 6, 7) : m.FinalTb is "T" or "S" ? (true, 6, 10) : (true, 6, 7);
            else (avecTb, declencheur, cible) = (true, 6, 7);
            var (set, prochain) = SimulerSet(m.Id, i, m.Sets[0].Surface, serveur, avecTb, declencheur, cible, niveau(i), rng);
            res.Add(set);
            setsGagnes[set.Gagnant]++;
            serveur = prochain;
        }
        return res;
    }

    // ================================================================ lecture

    private sealed record Bilan(int Matchs, int SetsLus, int SetsIncomplets, int PointsIllisibles);

    private static (List<SetJoue>, List<MatchJoue>, Bilan) Charger(string dossier)
    {
        var meta = new Dictionary<string, (string Surface, int BestOf, string FinalTb)>();
        foreach (var ligne in File.ReadLines(Path.Combine(dossier, "charting-m-matches.csv")).Skip(1))
        {
            var f = Sackmann.Decouper(ligne);
            if (f.Count < 14) continue;
            int.TryParse(f[12], out int bo);
            meta[f[0]] = (f[10], bo, f[13].Trim());
        }

        var brut = new Dictionary<string, List<(int Pt, int Set, int G1, int G2, string Pts, bool TbSet, int Svr, int Gagnant)>>();
        int illisibles = 0;
        foreach (var fichier in Directory.GetFiles(dossier, "charting-m-points-*.csv"))
        {
            using var r = new StreamReader(fichier, Encoding.UTF8);
            var entete = Sackmann.Decouper(r.ReadLine() ?? "");
            int I(string c) => entete.IndexOf(c);
            int iM = I("match_id"), iPt = I("Pt"), iS1 = I("Set1"), iS2 = I("Set2"), iG1 = I("Gm1"), iG2 = I("Gm2"),
                iPts = I("Pts"), iTb = I("TbSet"), iSvr = I("Svr"), iW = I("PtWinner");
            string? ligne;
            while ((ligne = r.ReadLine()) is not null)
            {
                var f = Sackmann.Decouper(ligne);
                if (f.Count <= iW || !int.TryParse(f[iPt], out int pt) || !int.TryParse(f[iS1], out int s1) || !int.TryParse(f[iS2], out int s2)
                    || !int.TryParse(f[iG1], out int g1) || !int.TryParse(f[iG2], out int g2)
                    || !int.TryParse(f[iSvr], out int svr) || !int.TryParse(f[iW], out int gagnant) || svr is not (1 or 2) || gagnant is not (1 or 2))
                { illisibles++; continue; }
                if (!brut.TryGetValue(f[iM], out var l)) brut[f[iM]] = l = [];
                l.Add((pt, s1 + s2, g1, g2, f[iPts], f[iTb] is "True" or "1" or "TRUE", svr - 1, gagnant - 1));
            }
        }

        var sets = new List<SetJoue>();
        var matchs = new List<MatchJoue>();
        int setsLus = 0, incomplets = 0;
        foreach (var (id, l) in brut)
        {
            if (!meta.TryGetValue(id, out var m) || m.BestOf is not (3 or 5)) continue;
            var setsMatch = new List<SetJoue>();
            bool matchComplet = true;
            foreach (var g in l.OrderBy(x => x.Pt).GroupBy(x => x.Set).OrderBy(g => g.Key))
            {
                setsLus++;
                var lp = g.ToList();
                var points = new List<Point>();
                bool ok = true;
                int declencheur = 6;
                foreach (var x in lp)
                {
                    bool tb = x.TbSet && x.G1 == x.G2 && x.G1 >= 6;
                    if (tb) declencheur = x.G1;
                    if (!LireScore(x.Pts, tb, out int sS, out int sR)) { ok = false; break; }
                    int js = x.Svr == 0 ? x.G1 : x.G2, jr = x.Svr == 0 ? x.G2 : x.G1;
                    points.Add(new Point(x.Svr, x.Gagnant == x.Svr, tb, sS, sR, js, jr));
                }
                if (!ok || points.Count < 24) { incomplets++; matchComplet = false; continue; }
                int cible = g.Key == m.BestOf - 1 && m.FinalTb is "T" or "S" ? 10 : 7;
                var set = new SetJoue(id, g.Key, m.Surface, points, lp[0].TbSet, declencheur, cible);
                var last = points[^1];
                int cat = Categorie(last, set);
                bool converti = (cat == 1 && last.ServeurGagne) || (cat == 0 && !last.ServeurGagne);
                if (!converti) { incomplets++; matchComplet = false; continue; }
                sets.Add(set);
                setsMatch.Add(set);
            }
            // Match entièrement noté : sets consécutifs depuis le premier, fin au bon moment.
            if (matchComplet && setsMatch.Count > 0 && setsMatch.Select((s, i) => s.Indice == i).All(x => x))
            {
                int gagnants = (m.BestOf + 1) / 2;
                int[] c = [0, 0];
                bool coherent = true;
                for (int i = 0; i < setsMatch.Count; i++)
                {
                    if (Math.Max(c[0], c[1]) == gagnants) { coherent = false; break; }
                    c[setsMatch[i].Gagnant]++;
                }
                if (coherent && Math.Max(c[0], c[1]) == gagnants) matchs.Add(new MatchJoue(id, m.BestOf, m.FinalTb, setsMatch));
            }
        }
        return (sets, matchs, new Bilan(brut.Count, setsLus, incomplets, illisibles));
    }

    private static bool LireScore(string pts, bool tieBreak, out int s, out int r)
    {
        s = r = 0;
        var parts = pts.Split('-');
        if (parts.Length != 2) return false;
        if (tieBreak) return int.TryParse(parts[0], out s) && int.TryParse(parts[1], out r);
        static int V(string x) => x.Trim() switch { "0" => 0, "15" => 1, "30" => 2, "40" => 3, "AD" => 4, _ => -1 };
        s = V(parts[0]);
        r = V(parts[1]);
        if (s == 4) r = 3;
        if (r == 4) s = 3;
        return s >= 0 && r >= 0;
    }

    // ================================================================ outils

    private static (int N, int W) Compter(List<Point> points, int serveur)
    {
        int n = 0, w = 0;
        foreach (var p in points)
            if (p.Serveur == serveur) { n++; if (p.ServeurGagne) w++; }
        return (Math.Max(n, 1), w);
    }

    private static double Taux(List<Point> points, int serveur) { var (n, w) = Compter(points, serveur); return (double)w / n; }

    private static (double A, double V) AvantageSet(SetJoue s)
    {
        var (n0, w0) = Compter(s.Points, 0);
        var (n1, w1) = Compter(s.Points, 1);
        double p0 = (double)w0 / n0, p1 = (double)w1 / n1;
        return (100 * (p0 - p1), 1e4 * (p0 * (1 - p0) / n0 + p1 * (1 - p1) / n1));
    }

    private static double Et(List<double> v)
    {
        double m = v.Average();
        return Math.Sqrt(v.Sum(x => (x - m) * (x - m)) / (v.Count - 1));
    }

    private static string Racine(double v) => v > 0 ? Math.Sqrt(v).ToString("F2", Ci) : "—";

    private static void L(StringBuilder b, string test, string mesure, double o, double r, double e, double se, string u) =>
        b.AppendLine(string.Create(Ci, $"{test},\"{mesure}\",{o:F3},{r:F3},{e:F3},{se:F3},{u}"));
}
