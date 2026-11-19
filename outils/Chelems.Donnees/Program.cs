// Outil de l'agent données ATP. Mesure des rivalités : quelle part de la dispersion d'un match à l'autre
// suit le joueur, le duel (la paire), ou change à chaque match, une fois le niveau du moment retiré par un Elo.
// Usage : dotnet run --project outils/Chelems.Donnees -- rivalites [dossier tennis_atp] [dossier de sortie]
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Chelems.Donnees;

CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
Console.OutputEncoding = Encoding.UTF8;

string racine = TrouverRacine();
string source = args.Length > 1 ? args[1] : Path.Combine(racine, "tennis_atp-master");
string sortie = args.Length > 2 ? args[2] : Path.Combine(racine, "resultats", "rivalites");

if (args.FirstOrDefault() == "enjeu")
{
    Enjeu.Executer(Path.Combine(racine, "tennis_atp-master"), Path.Combine(racine, "resultats", "enjeu"));
    return;
}
if (args.FirstOrDefault() == "objectifs")
{
    string surf = args.Length > 1 ? args[1] : "Hard";
    Objectifs.Executer(Path.Combine(racine, "tennis_atp-master"), surf, args.Length > 2 ? args[2] : Path.Combine(racine, "objectifs_equilibrage", Objectifs.Nom(surf)));
    return;
}
if (args.FirstOrDefault() == "forme")
{
    Forme.Executer(Path.Combine(racine, "tennis_atp-master"), Path.Combine(racine, "resultats", "forme"));
    return;
}
if (args.FirstOrDefault() == "points")
{
    PointParPoint.Executer(Path.Combine(racine, "tennis_MatchChartingProject-master"), Path.Combine(racine, "resultats", "point_par_point"));
    return;
}
if (args.FirstOrDefault() == "surfaces")
{
    int depuis = args.Length > 1 && int.TryParse(args[1], out int d) ? d : 2015;
    RivalitesSurfaces.Executer(Path.Combine(racine, "tennis_atp-master"), Path.Combine(racine, "resultats", $"rivalites_surfaces_{depuis}"), depuis);
    return;
}
if (args.FirstOrDefault() != "rivalites")
{
    Console.WriteLine("Usage : rivalites [dossier tennis_atp] [dossier de sortie]");
    return;
}
Rivalites.Executer(source, sortie);

static string TrouverRacine()
{
    var d = new DirectoryInfo(AppContext.BaseDirectory);
    while (d is not null && !File.Exists(Path.Combine(d.FullName, "Chelems.slnx"))) d = d.Parent;
    return d?.FullName ?? Directory.GetCurrentDirectory();
}

namespace Chelems.Donnees
{
    /// <summary>Un match du dépôt de Jeff Sackmann, réduit à ce dont la mesure a besoin.</summary>
    internal sealed record MatchAtp(
        int Date, string Tournoi, int OrdreTour, int Numero, int Gagnant, int Perdant,
        string Surface, string Niveau, int BestOf, bool Complet, bool CircuitPrincipal,
        int PtsServiceG, int GagnesServiceG, int PtsServiceP, int GagnesServiceP, string Score = "",
        int RangG = 99999, int RangP = 99999, int BbSauveesG = -1, int BbSubiesG = -1, int BbSauveesP = -1, int BbSubiesP = -1);

    internal static class Sackmann
    {
        private static readonly Regex Incomplet = new("RET|W/O|DEF|Def|unfinished|In Progress|Walkover", RegexOptions.Compiled);

        private static readonly Dictionary<string, int> Tours = new()
        {
            ["Q1"] = 0, ["Q2"] = 1, ["Q3"] = 2, ["Q4"] = 3, ["ER"] = 9, ["R128"] = 10, ["R64"] = 11, ["R32"] = 12,
            ["RR"] = 12, ["R16"] = 13, ["QF"] = 14, ["SF"] = 15, ["BR"] = 15, ["F"] = 16,
        };

        public static List<MatchAtp> Charger(string dossier, int debut, int fin)
        {
            var tous = new List<MatchAtp>();
            for (int an = debut; an <= fin; an++)
            {
                Lire(Path.Combine(dossier, $"atp_matches_{an}.csv"), true, tous);
                Lire(Path.Combine(dossier, $"atp_matches_qual_chall_{an}.csv"), false, tous);
            }
            // Ordre chronologique : semaine du tournoi, puis tour (qualifications avant le tableau), puis numéro.
            return tous.OrderBy(m => m.Date).ThenBy(m => m.OrdreTour).ThenBy(m => m.Tournoi).ThenBy(m => m.Numero).ToList();
        }

        private static void Lire(string chemin, bool principal, List<MatchAtp> dest)
        {
            if (!File.Exists(chemin)) return;
            using var r = new StreamReader(chemin, Encoding.UTF8);
            var entete = (r.ReadLine() ?? "").Split(',');
            int I(string c) => Array.IndexOf(entete, c);
            int iT = I("tourney_id"), iS = I("surface"), iN = I("tourney_level"), iD = I("tourney_date"), iM = I("match_num"),
                iW = I("winner_id"), iL = I("loser_id"), iSc = I("score"), iB = I("best_of"), iR = I("round"),
                iWs = I("w_svpt"), iW1 = I("w_1stWon"), iW2 = I("w_2ndWon"), iLs = I("l_svpt"), iL1 = I("l_1stWon"), iL2 = I("l_2ndWon"),
                iRw = I("winner_rank"), iRl = I("loser_rank"), iWbs = I("w_bpSaved"), iWbf = I("w_bpFaced"), iLbs = I("l_bpSaved"), iLbf = I("l_bpFaced");
            string? ligne;
            while ((ligne = r.ReadLine()) is not null)
            {
                var f = Decouper(ligne);
                if (f.Count < entete.Length) continue;
                if (!int.TryParse(f[iW], out int w) || !int.TryParse(f[iL], out int l)) continue;
                int Ent(int i) => int.TryParse(f[i], NumberStyles.Float, CultureInfo.InvariantCulture, out int v) ? v : -1;
                int ws = Ent(iWs), wg = Ent(iW1) + Ent(iW2), ls = Ent(iLs), lg = Ent(iL1) + Ent(iL2);
                bool stats = ws > 0 && ls > 0 && Ent(iW1) >= 0 && Ent(iW2) >= 0 && Ent(iL1) >= 0 && Ent(iL2) >= 0;
                dest.Add(new MatchAtp(Ent(iD), f[iT], Tours.GetValueOrDefault(f[iR], 12), Ent(iM), w, l, f[iS], f[iN],
                                      Ent(iB), !Incomplet.IsMatch(f[iSc]) && f[iSc].Length > 0, principal,
                                      stats ? ws : 0, stats ? wg : 0, stats ? ls : 0, stats ? lg : 0, f[iSc],
                                      Ent(iRw) > 0 ? Ent(iRw) : 99999, Ent(iRl) > 0 ? Ent(iRl) : 99999, Ent(iWbs), Ent(iWbf), Ent(iLbs), Ent(iLbf)));
            }
        }

        internal static List<string> Decouper(string ligne)
        {
            var champs = new List<string>(50);
            var sb = new StringBuilder();
            bool g = false;
            foreach (char c in ligne)
            {
                if (c == '"') g = !g;
                else if (c == ',' && !g) { champs.Add(sb.ToString()); sb.Clear(); }
                else sb.Append(c);
            }
            champs.Add(sb.ToString().TrimEnd('\r'));
            return champs;
        }

        public static Dictionary<int, string> Noms(string dossier)
        {
            var d = new Dictionary<int, string>();
            foreach (var ligne in File.ReadLines(Path.Combine(dossier, "atp_players.csv")).Skip(1))
            {
                var f = Decouper(ligne);
                if (f.Count >= 3 && int.TryParse(f[0], out int id)) d[id] = $"{f[1]} {f[2]}".Trim();
            }
            return d;
        }
    }

    /// <summary>Elo match par match (méthode FiveThirtyEight : K = 250 / (matchs joués + 5)^0,4), global et dur.</summary>
    internal sealed class Elo
    {
        private readonly Dictionary<int, (double R, int N)> _global = new();
        private readonly Dictionary<string, Dictionary<int, (double R, int N)>> _surface = new();

        /// <summary>Elo toutes surfaces : un niveau sans aucune information de surface.</summary>
        public double Global(int joueur) => _global.GetValueOrDefault(joueur, (1500, 0)).R;

        /// <summary>Moyenne de l'Elo global et de l'Elo de la surface (méthode FiveThirtyEight).</summary>
        public double Melange(int joueur, string surface) =>
            0.5 * (Global(joueur) + (_surface.TryGetValue(surface, out var t) ? t.GetValueOrDefault(joueur, (1500, 0)).R : 1500));

        public double Dur(int joueur) => Melange(joueur, "Hard");

        public void MettreAJour(MatchAtp m)
        {
            Maj(_global, m.Gagnant, m.Perdant);
            if (!_surface.TryGetValue(m.Surface, out var t)) _surface[m.Surface] = t = new();
            Maj(t, m.Gagnant, m.Perdant);
        }

        private static void Maj(Dictionary<int, (double R, int N)> t, int g, int p)
        {
            var (rg, ng) = t.GetValueOrDefault(g, (1500, 0));
            var (rp, np) = t.GetValueOrDefault(p, (1500, 0));
            double attendu = 1 / (1 + Math.Pow(10, (rp - rg) / 400));
            t[g] = (rg + 250 / Math.Pow(ng + 5, 0.4) * (1 - attendu), ng + 1);
            t[p] = (rp - 250 / Math.Pow(np + 5, 0.4) * (1 - attendu), np + 1);
        }
    }

    /// <summary>Un match mesuré, orienté du joueur A (plus petit identifiant) vers B.</summary>
    internal sealed record Observation(
        int A, int B, int Date, int BestOf, double EcartElo, bool AGagne,
        double Avantage, double VarianceBinomiale, double ServiceA, double RetourA, int PtsA, int PtsB, string Tournoi,
        string Surface = "Hard")
    {
        public double PrbVictoire;   // P(A gagne) selon l'Elo calibré
        public double ResVictoire;   // AGagne − PrbVictoire
        public double ResAvantage;   // Avantage − prédiction par l'écart d'Elo, en points de %
    }

    internal static class Rivalites
    {
        public static void Executer(string source, string sortie)
        {
            Directory.CreateDirectory(sortie);
            var log = new StringBuilder();
            void Dire(string s = "") { Console.WriteLine(s); log.AppendLine(s); }

            var matchs = Sackmann.Charger(source, 2005, 2025);
            var noms = Sackmann.Noms(source);
            var elo = new Elo();
            var obs = new List<Observation>();
            foreach (var m in matchs)
            {
                if (!m.Complet) continue;
                int an = m.Date / 10000;
                if (m.CircuitPrincipal && m.Niveau != "D" && m.Surface == "Hard" && an >= 2015 && m.PtsServiceG > 0
                    && m.BestOf is 3 or 5)
                {
                    bool aGagne = m.Gagnant < m.Perdant;
                    int a = Math.Min(m.Gagnant, m.Perdant), b = Math.Max(m.Gagnant, m.Perdant);
                    double sA = aGagne ? (double)m.GagnesServiceG / m.PtsServiceG : (double)m.GagnesServiceP / m.PtsServiceP;
                    double sB = aGagne ? (double)m.GagnesServiceP / m.PtsServiceP : (double)m.GagnesServiceG / m.PtsServiceG;
                    int nA = aGagne ? m.PtsServiceG : m.PtsServiceP, nB = aGagne ? m.PtsServiceP : m.PtsServiceG;
                    obs.Add(new Observation(a, b, m.Date, m.BestOf, elo.Dur(a) - elo.Dur(b), aGagne,
                                            100 * (sA - sB), 1e4 * (sA * (1 - sA) / nA + sB * (1 - sB) / nB),
                                            sA, 1 - sB, nA, nB, m.Tournoi));
                }
                elo.MettreAJour(m); // après la mesure : l'Elo utilisé est celui d'avant le match
            }

            Dire($"Mesure des rivalités — Jeff Sackmann tennis_atp, dur, circuit principal hors Coupe Davis, 2015-2025, matchs complets.");
            Dire($"Niveau du moment : Elo FiveThirtyEight (moyenne global/dur) calculé depuis 2005, Challengers et qualifications compris.");
            Dire($"{obs.Count} matchs mesurés.\n");

            Calibrer(obs, Dire);
            var bilan = new StringBuilder("mesure,best_of,valeur,unite,definition\n");
            foreach (int bo in new[] { 3, 5, 0 })
                Decomposer(obs.Where(o => bo == 0 || o.BestOf == bo).ToList(), bo, Dire, bilan);
            DecroissanceTemporelle(obs, Dire, bilan);
            MemeSemaine(obs, Dire, bilan);
            Equilibre(obs, noms, Dire, bilan, sortie);
            StylesOpposes(obs, Dire, bilan);
            Exemples(obs, noms, Dire, sortie);

            File.WriteAllText(Path.Combine(sortie, "decomposition.csv"), bilan.ToString());
            File.WriteAllText(Path.Combine(sortie, "rapport.txt"), log.ToString());
            Console.WriteLine($"\nÉcrit dans {sortie}");
        }

        /// <summary>P(victoire) = logistique(k × écart d'Elo) et avantage = c × écart d'Elo, par format.</summary>
        internal static void Calibrer(List<Observation> obs, Action<string> dire)
        {
            foreach (int bo in new[] { 3, 5 })
            {
                var o = obs.Where(x => x.BestOf == bo).ToList();
                double k = Math.Log(10) / 400;
                for (int it = 0; it < 50; it++)
                {
                    double g = 0, h = 0;
                    foreach (var x in o)
                    {
                        double p = 1 / (1 + Math.Exp(-k * x.EcartElo));
                        g += x.EcartElo * ((x.AGagne ? 1 : 0) - p);
                        h += x.EcartElo * x.EcartElo * p * (1 - p);
                    }
                    k += g / h;
                }
                double c = o.Sum(x => x.Avantage * x.EcartElo) / o.Sum(x => x.EcartElo * x.EcartElo);
                foreach (var x in o)
                {
                    x.PrbVictoire = 1 / (1 + Math.Exp(-k * x.EcartElo));
                    x.ResVictoire = (x.AGagne ? 1 : 0) - x.PrbVictoire;
                    x.ResAvantage = x.Avantage - c * x.EcartElo;
                }
                double brier = o.Average(x => x.ResVictoire * x.ResVictoire);
                dire($"Bo{bo} : {o.Count} matchs ; P(victoire) = logistique({k * 400 / Math.Log(10):F2} × écart Elo standard) ; " +
                     $"avantage au service = {100 * c:F2} pts par 100 points d'Elo ; Brier = {brier:F4}");
            }
            dire("");
        }

        /// <summary>
        /// Résidu de l'avantage au service r = u(joueurs) + w(match) + bruit binomial.
        /// - Total au-delà du hasard des points : Var(r) − moyenne de la variance binomiale.
        /// - Part propre au duel (tout ce qui suit la paire) : covariance moyenne de r entre deux matchs de la même paire.
        /// - Dont part propre à chaque joueur : covariance moyenne entre deux matchs d'un même joueur contre deux adversaires
        ///   différents (r orienté vers ce joueur). La paire en porte deux (A et B) : σ²(duel) = 2 σ²(joueur) + σ²(interaction).
        /// - Part qui change à chaque match : total − part du duel.
        /// </summary>
        internal static void Decomposer(List<Observation> obs, int bo, Action<string> dire, StringBuilder bilan, string? etiquette = null)
        {
            string fmt = etiquette ?? (bo == 0 ? "tous formats" : $"Bo{bo}");
            double totalBrut = Variance(obs.Select(o => o.ResAvantage));
            double bruit = obs.Average(o => o.VarianceBinomiale);
            double total = totalBrut - bruit;

            var (duel, nPaires, nCouples) = CovarianceIntraGroupe(
                obs.GroupBy(o => (o.A, o.B)).Select(g => g.Select(o => o.ResAvantage).ToList()));
            var (duelV, _, _) = CovarianceIntraGroupe(
                obs.GroupBy(o => (o.A, o.B)).Select(g => g.Select(o => o.ResVictoire).ToList()));
            double joueur = CovarianceJoueur(obs, o => o.ResAvantage);
            double joueurV = CovarianceJoueur(obs, o => o.ResVictoire);
            double interaction = duel - 2 * joueur;
            double match = total - duel;
            double bernoulli = obs.Average(o => o.PrbVictoire * (1 - o.PrbVictoire));

            dire($"=== Décomposition de l'avantage au service, {fmt} ({obs.Count} matchs, {nPaires} paires vues au moins deux fois, {nCouples} couples de matchs)");
            dire($"  Résidu brut après Elo : écart-type {Math.Sqrt(totalBrut):F2} pts, dont hasard des points {Math.Sqrt(bruit):F2} pts");
            dire($"  Au-delà du hasard des points : écart-type {Et(total)} pts (variance {total:F1})");
            dire($"    propre au duel (fixe pour la paire)  : variance {duel,6:F1}  ({Pct(duel, total)} du total)  écart-type {Et(duel)}");
            dire($"      dont joueurs (×2)                  : variance {2 * joueur,6:F1}  ({Pct(2 * joueur, total)})");
            dire($"      dont interaction des styles        : variance {interaction,6:F1}  ({Pct(interaction, total)})");
            dire($"    change à chaque match                : variance {match,6:F1}  ({Pct(match, total)})  écart-type {Et(match)}");
            dire($"  Victoires : covariance dans la paire {duelV:F4} (dont joueurs ×2 : {2 * joueurV:F4}), " +
                 $"à comparer à la variance de Bernoulli {bernoulli:F4} (part du duel ≈ {Pct(duelV, bernoulli)} de la variance d'un match)");
            dire("");

            void L(string mesure, double v, string u, string def) =>
                bilan.AppendLine(string.Create(CultureInfo.InvariantCulture, $"{mesure},{bo},{v:F3},{u},\"{def}\""));
            L("variance_au_dela_du_hasard", total, "pts²", "Var(résidu de l'avantage au service après Elo) − variance binomiale moyenne");
            L("variance_duel", duel, "pts²", "covariance du résidu entre deux matchs de la même paire");
            L("variance_joueurs_x2", 2 * joueur, "pts²", "2 × covariance entre deux matchs d'un joueur contre des adversaires différents");
            L("variance_interaction", interaction, "pts²", "variance_duel − variance_joueurs_x2");
            L("variance_par_match", match, "pts²", "variance_au_dela_du_hasard − variance_duel");
            L("part_duel", duel / total, "fraction", "variance_duel / variance_au_dela_du_hasard");
            L("covariance_victoires_duel", duelV, "", "covariance de (victoire − P Elo) entre deux matchs de la même paire");
            L("variance_bernoulli_moyenne", bernoulli, "", "moyenne de p(1 − p) selon l'Elo");
        }

        /// <summary>L'effet attaché à la paire baisse-t-il quand les deux matchs sont éloignés dans le temps ?</summary>
        internal static void DecroissanceTemporelle(List<Observation> obs, Action<string> dire, StringBuilder bilan)
        {
            (string Nom, double Min, double Max)[] classes = [("moins d'un an", 0, 1), ("1 à 3 ans", 1, 3), ("plus de 3 ans", 3, 99)];
            dire("=== Covariance du résidu de l'avantage entre deux matchs de la même paire, selon le temps qui les sépare");
            foreach (var (nom, min, max) in classes)
            {
                double s = 0, sv = 0; long n = 0;
                foreach (var g in obs.GroupBy(o => (o.A, o.B)))
                {
                    var l = g.ToList();
                    for (int i = 0; i < l.Count; i++)
                        for (int j = i + 1; j < l.Count; j++)
                        {
                            double dt = Math.Abs(Annees(l[i].Date) - Annees(l[j].Date));
                            if (dt < min || dt >= max) continue;
                            s += l[i].ResAvantage * l[j].ResAvantage;
                            sv += l[i].ResVictoire * l[j].ResVictoire;
                            n++;
                        }
                }
                dire($"  {nom,-14} : {n,6} couples, covariance avantage {s / n,5:F1} pts², covariance victoires {sv / n:F4}");
                bilan.AppendLine(string.Create(CultureInfo.InvariantCulture,
                    $"covariance_duel_{nom.Replace(' ', '_')},0,{s / n:F3},pts²,\"{n} couples de matchs de la même paire\""));
            }
            dire("");
        }

        /// <summary>
        /// La part « par match » dure-t-elle une semaine ? Covariance du résidu (orienté vers le joueur) entre deux matchs
        /// d'un même joueur contre deux adversaires différents : dans le même tournoi, ou dans deux tournois à moins de 60 jours.
        /// Un excès dans le même tournoi signale un effet de la semaine : conditions (vitesse du court, balles, altitude) ou forme.
        /// </summary>
        internal static void MemeSemaine(List<Observation> obs, Action<string> dire, StringBuilder bilan)
        {
            var parJoueur = new Dictionary<int, List<(int Adv, double R, double Rv, string T, double Date)>>();
            foreach (var o in obs)
            {
                Ajouter(o.A, o.B, o.ResAvantage, o.ResVictoire, o);
                Ajouter(o.B, o.A, -o.ResAvantage, -o.ResVictoire, o);
            }
            void Ajouter(int j, int adv, double r, double rv, Observation o)
            {
                if (!parJoueur.TryGetValue(j, out var l)) parJoueur[j] = l = [];
                l.Add((adv, r, rv, o.Tournoi, Annees(o.Date)));
            }
            double sT = 0, svT = 0, sA = 0, svA = 0; long nT = 0, nA = 0;
            foreach (var l in parJoueur.Values)
                for (int i = 0; i < l.Count; i++)
                    for (int j = i + 1; j < l.Count; j++)
                    {
                        if (l[i].Adv == l[j].Adv) continue;
                        if (l[i].T == l[j].T) { sT += l[i].R * l[j].R; svT += l[i].Rv * l[j].Rv; nT++; }
                        else if (Math.Abs(l[i].Date - l[j].Date) < 60 / 365.0) { sA += l[i].R * l[j].R; svA += l[i].Rv * l[j].Rv; nA++; }
                    }
            dire("=== Même joueur, deux adversaires différents : même tournoi ou tournois différents à moins de 60 jours");
            dire($"  même tournoi        : {nT,7} couples, covariance avantage {sT / nT,5:F1} pts², victoires {svT / nT:F4}");
            dire($"  autre tournoi < 60 j : {nA,7} couples, covariance avantage {sA / nA,5:F1} pts², victoires {svA / nA:F4}");
            dire("");
            bilan.AppendLine(string.Create(CultureInfo.InvariantCulture, $"covariance_joueur_meme_tournoi,0,{sT / nT:F3},pts²,\"{nT} couples\""));
            bilan.AppendLine(string.Create(CultureInfo.InvariantCulture, $"covariance_joueur_autre_tournoi_60j,0,{sA / nA:F3},pts²,\"{nA} couples\""));
        }

        /// <summary>Rivalités d'au moins 6 matchs sur dur : part des rivalités équilibrées, réel contre Elo seul.</summary>
        internal static void Equilibre(List<Observation> obs, Dictionary<int, string> noms, Action<string> dire,
                                      StringBuilder bilan, string sortie)
        {
            var paires = obs.GroupBy(o => (o.A, o.B)).Where(g => g.Count() >= 6).Select(g => g.ToList()).ToList();
            double Part(Func<List<Observation>, int> victoiresA) =>
                paires.Count(p => { int v = victoiresA(p); return Math.Min(v, p.Count - v) >= 0.4 * p.Count; }) / (double)paires.Count;
            double reel = Part(p => p.Count(o => o.AGagne));

            var rng = new Random(1);
            double nul = 0;
            const int repliques = 2000;
            for (int r = 0; r < repliques; r++) nul += Part(p => p.Count(o => rng.NextDouble() < o.PrbVictoire));
            nul /= repliques;

            // Écart à l'Elo dans chaque paire : (victoires réelles − attendues), réduit par l'écart-type de Bernoulli.
            var z = paires.Select(p => p.Sum(o => o.ResVictoire) / Math.Sqrt(p.Sum(o => o.PrbVictoire * (1 - o.PrbVictoire)))).ToList();
            double varZ = z.Average(x => x * x);

            dire($"=== Rivalités d'au moins 6 matchs sur dur : {paires.Count} paires");
            dire($"  Part des rivalités équilibrées (le moins victorieux gagne au moins 40 % des matchs) : réel {100 * reel:F1} %, Elo seul {100 * nul:F1} %");
            dire($"  Dispersion des bilans autour de l'Elo : Var(z) = {varZ:F2} (1 = l'Elo explique tout, > 1 = effet propre au duel)");
            dire("");
            bilan.AppendLine(string.Create(CultureInfo.InvariantCulture, $"rivalites_equilibrees_reel,0,{reel:F4},fraction,\"paires >= 6 matchs, minoritaire >= 40 %\""));
            bilan.AppendLine(string.Create(CultureInfo.InvariantCulture, $"rivalites_equilibrees_elo_seul,0,{nul:F4},fraction,\"même mesure, victoires tirées selon P Elo\""));
            bilan.AppendLine(string.Create(CultureInfo.InvariantCulture, $"variance_z_bilans,0,{varZ:F4},,\"Var((victoires − attendues) / écart-type Bernoulli), paires >= 6 matchs\""));

            var csv = new StringBuilder("joueur_a,joueur_b,matchs,victoires_a,victoires_attendues_a,z\n");
            for (int i = 0; i < paires.Count; i++)
            {
                var p = paires[i];
                csv.AppendLine(string.Create(CultureInfo.InvariantCulture,
                    $"\"{noms.GetValueOrDefault(p[0].A)}\",\"{noms.GetValueOrDefault(p[0].B)}\",{p.Count},{p.Count(o => o.AGagne)},{p.Sum(o => o.PrbVictoire):F2},{z[i]:F2}"));
            }
            File.WriteAllText(Path.Combine(sortie, "rivalites_6_matchs.csv"), csv.ToString());
        }

        /// <summary>
        /// Hypothèse du créateur : deux styles opposés donnent des duels plus mouvants.
        /// Style approché par le profil de service : % de points gagnés au service − % de points gagnés au retour
        /// (positif = serveur, négatif = relanceur), mesuré sur tous ses matchs de l'échantillon.
        /// </summary>
        internal static void StylesOpposes(List<Observation> obs, Action<string> dire, StringBuilder bilan)
        {
            var profil = new Dictionary<int, (double S, double R, int N)>();
            foreach (var o in obs)
            {
                Ajout(o.A, o.ServiceA, o.RetourA);
                Ajout(o.B, 1 - o.RetourA, 1 - o.ServiceA);
            }
            void Ajout(int j, double s, double r)
            {
                var p = profil.GetValueOrDefault(j);
                profil[j] = (p.S + s, p.R + r, p.N + 1);
            }
            double Style(int j) { var p = profil[j]; return 100 * (p.S - p.R) / p.N; }

            var paires = obs.GroupBy(o => (o.A, o.B)).Where(g => g.Count() >= 2 && profil[g.Key.A].N >= 20 && profil[g.Key.B].N >= 20)
                            .Select(g => (Distance: Math.Abs(Style(g.Key.A) - Style(g.Key.B)), Matchs: g.ToList()))
                            .OrderBy(p => p.Distance).ToList();
            int t = paires.Count / 3;
            dire("=== Styles proches ou opposés (style = % points gagnés au service − % au retour, joueurs ≥ 20 matchs)");
            string[] noms = ["styles proches", "intermédiaire", "styles opposés"];
            for (int k = 0; k < 3; k++)
            {
                var groupe = paires.Skip(k * t).Take(k == 2 ? paires.Count - 2 * t : t).ToList();
                var o = groupe.SelectMany(p => p.Matchs).ToList();
                double total = Variance(o.Select(x => x.ResAvantage)) - o.Average(x => x.VarianceBinomiale);
                var (duel, _, _) = CovarianceIntraGroupe(groupe.Select(p => p.Matchs.Select(x => x.ResAvantage).ToList()));
                var (duelV, _, _) = CovarianceIntraGroupe(groupe.Select(p => p.Matchs.Select(x => x.ResVictoire).ToList()));
                double bern = o.Average(x => x.PrbVictoire * (1 - x.PrbVictoire));
                dire($"  {noms[k],-16} (écart de style {groupe.First().Distance:F1} à {groupe.Last().Distance:F1} pts, {groupe.Count} paires, {o.Count} matchs) : " +
                     $"au-delà du hasard {total,5:F1}, duel {duel,5:F1}, par match {total - duel,5:F1} pts² ; victoires : duel {duelV:F4} / Bernoulli {bern:F4}");
                bilan.AppendLine(string.Create(CultureInfo.InvariantCulture,
                    $"style_{k + 1}_variance_duel,0,{duel:F3},pts²,\"{noms[k]}\""));
                bilan.AppendLine(string.Create(CultureInfo.InvariantCulture,
                    $"style_{k + 1}_variance_par_match,0,{total - duel:F3},pts²,\"{noms[k]}\""));
            }
            dire("");
        }

        internal static void Exemples(List<Observation> obs, Dictionary<int, string> noms, Action<string> dire, string sortie)
        {
            int Id(string nom) => noms.First(kv => kv.Value == nom).Key;
            (string, string)[] duels = [("Daniil Medvedev", "Jannik Sinner"), ("Novak Djokovic", "Rafael Nadal"),
                                        ("Novak Djokovic", "Roger Federer"), ("Carlos Alcaraz", "Jannik Sinner")];
            dire("=== Exemples (dur, circuit principal 2015-2025) : P = probabilité de victoire du premier joueur selon l'Elo");
            foreach (var (x, y) in duels)
            {
                int a = Id(x), b = Id(y);
                var l = obs.Where(o => (o.A == Math.Min(a, b)) && (o.B == Math.Max(a, b))).OrderBy(o => o.Date).ToList();
                if (l.Count == 0) continue;
                bool inverse = a > b;
                var seq = l.Select(o =>
                {
                    bool g = o.AGagne ^ inverse;
                    double p = inverse ? 1 - o.PrbVictoire : o.PrbVictoire;
                    return string.Create(CultureInfo.InvariantCulture, $"{o.Date / 10000}:{(g ? "V" : "D")}({100 * p:F0})");
                });
                int v = l.Count(o => o.AGagne ^ inverse);
                double att = l.Sum(o => inverse ? 1 - o.PrbVictoire : o.PrbVictoire);
                dire($"  {x} – {y} : {v}-{l.Count - v} (attendu par l'Elo {att:F1}-{l.Count - att:F1})");
                dire($"    {string.Join(' ', seq)}");
            }
        }

        // ---------- outils statistiques ----------

        internal static (double Cov, int Groupes, long Couples) CovarianceIntraGroupe(IEnumerable<List<double>> groupes)
        {
            // Moyenne sur tous les couples (i ≠ j) de matchs d'un même groupe de r_i × r_j.
            double s = 0; long couples = 0; int g = 0;
            foreach (var l in groupes)
            {
                if (l.Count < 2) continue;
                double somme = l.Sum(), carres = l.Sum(x => x * x);
                s += (somme * somme - carres) / 2;
                couples += (long)l.Count * (l.Count - 1) / 2;
                g++;
            }
            return (couples == 0 ? double.NaN : s / couples, g, couples);
        }

        internal static double CovarianceJoueur(List<Observation> obs, Func<Observation, double> residu)
        {
            // Pour chaque joueur, résidus orientés vers lui ; couples de matchs contre deux adversaires différents.
            var parJoueur = new Dictionary<int, List<(int Adv, double R)>>();
            foreach (var o in obs)
            {
                double r = residu(o);
                Ajouter(o.A, o.B, r);
                Ajouter(o.B, o.A, -r);
            }
            void Ajouter(int j, int adv, double r)
            {
                if (!parJoueur.TryGetValue(j, out var l)) parJoueur[j] = l = [];
                l.Add((adv, r));
            }
            double s = 0; long n = 0;
            foreach (var l in parJoueur.Values)
            {
                double somme = l.Sum(x => x.R), carres = l.Sum(x => x.R * x.R);
                double tous = (somme * somme - carres) / 2;
                long nTous = (long)l.Count * (l.Count - 1) / 2;
                foreach (var g in l.GroupBy(x => x.Adv))
                {
                    double sg = g.Sum(x => x.R), cg = g.Sum(x => x.R * x.R);
                    tous -= (sg * sg - cg) / 2;
                    nTous -= (long)g.Count() * (g.Count() - 1) / 2;
                }
                s += tous;
                n += nTous;
            }
            return s / n;
        }

        internal static double Variance(IEnumerable<double> x)
        {
            var l = x.ToList();
            double m = l.Average();
            return l.Sum(v => (v - m) * (v - m)) / (l.Count - 1);
        }

        internal static double Annees(int date) =>
            date / 10000 + ((date / 100) % 100 - 1) / 12.0 + (date % 100) / 365.0;

        internal static string Et(double v) => v > 0 ? Math.Sqrt(v).ToString("F2", CultureInfo.InvariantCulture) : "—";
        internal static string Pct(double a, double b) => (100 * a / b).ToString("F0", CultureInfo.InvariantCulture) + " %";
    }
}
