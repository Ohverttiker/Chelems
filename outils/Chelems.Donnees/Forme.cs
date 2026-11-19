using System.Globalization;
using System.Text;

namespace Chelems.Donnees;

/// <summary>
/// Forme du jour, confiance et constance (test du principe 6 et de la stat Constance de CLAUDE.md).
/// Performance d'un joueur dans un match = résidu de l'avantage au service (son % au service − celui de l'adversaire)
/// après le niveau du moment, orienté vers lui. Deux niveaux de référence :
/// - R1 : Elo FiveThirtyEight (global + surface) d'avant le match. Il suit les séries, donc il absorbe une partie de la forme ;
/// - R2 : le même Elo, figé au premier match de la saison. Il n'absorbe rien, mais il laisse la progression réelle
///   pendant la saison dans les résidus.
/// La vérité est entre les deux.
/// 1. Persistance : la performance d'un match ressemble-t-elle à celle des matchs suivants (selon l'écart en matchs ou en jours) ?
/// 2. Confiance : après un match serré (set décisif), le vainqueur joue-t-il mieux ensuite que le perdant, au-delà de la qualité de
///    jeu de ce match ? Contrôle placebo : la même mesure sur le tournoi d'AVANT (une forme qui dure donnerait le même écart).
/// 3. Constance : certains joueurs sont-ils durablement plus irréguliers ? Fiabilité par moitiés (matchs pairs et impairs).
/// Usage : dotnet run --project outils/Chelems.Donnees -- forme
/// </summary>
internal static class Forme
{
    private static readonly CultureInfo Ci = CultureInfo.InvariantCulture;
    private static readonly string[] Surfaces = ["Hard", "Clay", "Grass"];

    private sealed class Ligne
    {
        public int Joueur, Adversaire, Date, Annee, Ordre;
        public string Tournoi = "", Surface = "";
        public bool Gagne, Serre;
        public double R1, R2, V, Rv1, Rv2, Niveau;
    }

    public static void Executer(string source, string sortie)
    {
        Directory.CreateDirectory(sortie);
        var log = new StringBuilder();
        void Dire(string s) { Console.WriteLine(s); log.AppendLine(s); }
        var bilan = new StringBuilder("test,mesure,valeur,erreur_type,unite,definition\n");
        void L(string t, string m, double v, double se, string u, string d) =>
            bilan.AppendLine(string.Create(Ci, $"{t},\"{m}\",{v:F4},{se:F4},{u},\"{d}\""));

        var noms = Sackmann.Noms(source);
        var lignes = Construire(source, Dire);
        var parJoueur = lignes.GroupBy(l => l.Joueur).ToDictionary(g => g.Key, g => g.OrderBy(l => l.Ordre).ToList());

        double var1 = Variance(lignes.Select(l => l.R1)) - lignes.Average(l => l.V);
        double var2 = Variance(lignes.Select(l => l.R2)) - lignes.Average(l => l.V);
        double sf1 = var1 / 2, sf2 = var2 / 2; // part d'un joueur (le résidu porte la forme des deux joueurs)
        Dire(string.Create(Ci, $"Performance au-delà du hasard des points, par match (les deux joueurs) : R1 {var1:F1} pts², R2 {var2:F1} pts² ; part d'un joueur ≈ {sf1:F1} / {sf2:F1} pts²\n"));

        // ------------------------------------------------------------ 1. Persistance
        Dire("=== 1. Persistance de la forme : corrélation de la performance d'un joueur entre deux de ses matchs (adversaires différents)");
        Dire("    ρ = covariance / variance de la forme d'un joueur. R1 = Elo du moment, R2 = Elo figé en début de saison.");
        int[] lags = [1, 2, 3, 5, 10, 20, 50, 100];
        foreach (int k in lags)
        {
            double s1 = 0, s2 = 0; long n = 0;
            foreach (var l in parJoueur.Values)
                for (int i = 0; i + k < l.Count; i++)
                {
                    if (l[i].Adversaire == l[i + k].Adversaire) continue;
                    s1 += l[i].R1 * l[i + k].R1; s2 += l[i].R2 * l[i + k].R2; n++;
                }
            Dire(string.Create(Ci, $"  {k,3} match(s) d'écart : ρ R1 = {s1 / n / sf1,6:F3}   ρ R2 = {s2 / n / sf2,6:F3}   ({n} couples)"));
            L("1", $"rho_ecart_{k}_matchs_R1", s1 / n / sf1, double.NaN, "", "corrélation de la forme");
            L("1", $"rho_ecart_{k}_matchs_R2", s2 / n / sf2, double.NaN, "", "corrélation de la forme");
        }
        (string Nom, double Min, double Max, bool Meme)[] periodes =
            [("même tournoi", 0, 1e9, true), ("autre tournoi, < 15 j", 0, 15, false), ("15 à 45 j", 15, 45, false),
             ("45 à 120 j", 45, 120, false), ("120 à 365 j", 120, 365, false), ("1 à 2 ans", 365, 730, false)];
        foreach (var (nom, min, max, meme) in periodes)
        {
            double s1 = 0, s2 = 0; long n = 0;
            foreach (var l in parJoueur.Values)
                for (int i = 0; i < l.Count; i++)
                    for (int j = i + 1; j < l.Count; j++)
                    {
                        double dj = Jours(l[j].Date) - Jours(l[i].Date);
                        if (dj >= 730) break;
                        bool memeT = l[i].Tournoi == l[j].Tournoi;
                        if (memeT != meme || l[i].Adversaire == l[j].Adversaire) continue;
                        if (!meme && (dj < min || dj >= max)) continue;
                        s1 += l[i].R1 * l[j].R1; s2 += l[i].R2 * l[j].R2; n++;
                    }
            Dire(string.Create(Ci, $"  {nom,-22}  : ρ R1 = {s1 / n / sf1,6:F3}   ρ R2 = {s2 / n / sf2,6:F3}   ({n} couples)"));
            L("1", $"rho_{nom}_R1", s1 / n / sf1, double.NaN, "", "corrélation de la forme");
            L("1", $"rho_{nom}_R2", s2 / n / sf2, double.NaN, "", "corrélation de la forme");
        }
        Dire("");

        // ------------------------------------------------------------ 2. Confiance
        Confiance(parJoueur, Dire, L);

        // ------------------------------------------------------------ 3. Constance
        Constance(parJoueur, noms, sf1, Dire, L, sortie);

        // ------------------------------------------------------------ exemples
        Dire("=== Exemples : derniers matchs (circuit principal et Challengers), performance R1 / R2 en pts d'avantage au service");
        foreach (var nom in new[] { "Daniil Medvedev", "Frances Tiafoe" })
        {
            int id = noms.First(kv => kv.Value == nom).Key;
            Dire($"  {nom} :");
            foreach (var l in parJoueur[id].TakeLast(14))
                Dire(string.Create(Ci, $"    {l.Date}  {l.Tournoi,-10} {(l.Gagne ? "V" : "D")} contre {noms.GetValueOrDefault(l.Adversaire),-26} R1 {l.R1,6:+0.0;-0.0}  R2 {l.R2,6:+0.0;-0.0}"));
        }

        File.WriteAllText(Path.Combine(sortie, "resultats.csv"), bilan.ToString());
        File.WriteAllText(Path.Combine(sortie, "rapport.txt"), log.ToString());
        Console.WriteLine($"\nÉcrit dans {sortie}");
    }

    private static List<Ligne> Construire(string source, Action<string> dire)
    {
        var elo = new Elo();
        var fige = new Dictionary<(int, int), Dictionary<string, double>>();
        Dictionary<string, double> Fige(int j, int an)
        {
            if (!fige.TryGetValue((j, an), out var d))
                fige[(j, an)] = d = Surfaces.ToDictionary(s => s, s => elo.Melange(j, s));
            return d;
        }

        var obs1 = new List<Observation>();
        var obs2 = new List<Observation>();
        var serres = new List<bool>();
        var niveaux = new List<(double A, double B)>();
        int ordre = 0;
        foreach (var m in Sackmann.Charger(source, 2000, 2026))
        {
            if (!m.Complet) continue;
            int an = m.Date / 10000;
            if (Surfaces.Contains(m.Surface)) { Fige(m.Gagnant, an); Fige(m.Perdant, an); }
            if (m.Niveau != "D" && Surfaces.Contains(m.Surface) && an >= 2010 && m.PtsServiceG > 0 && m.PtsServiceP > 0 && m.BestOf is 3 or 5)
            {
                bool aGagne = m.Gagnant < m.Perdant;
                int a = Math.Min(m.Gagnant, m.Perdant), b = Math.Max(m.Gagnant, m.Perdant);
                double sA = aGagne ? (double)m.GagnesServiceG / m.PtsServiceG : (double)m.GagnesServiceP / m.PtsServiceP;
                double sB = aGagne ? (double)m.GagnesServiceP / m.PtsServiceP : (double)m.GagnesServiceG / m.PtsServiceG;
                int nA = aGagne ? m.PtsServiceG : m.PtsServiceP, nB = aGagne ? m.PtsServiceP : m.PtsServiceG;
                double adv = 100 * (sA - sB), v = 1e4 * (sA * (1 - sA) / nA + sB * (1 - sB) / nB);
                string tid = $"{m.Tournoi}";
                obs1.Add(new Observation(a, b, m.Date * 100 + m.OrdreTour, m.BestOf, elo.Melange(a, m.Surface) - elo.Melange(b, m.Surface),
                                         aGagne, adv, v, sA, 1 - sB, nA, nB, tid, m.Surface));
                obs2.Add(new Observation(a, b, m.Date * 100 + m.OrdreTour, m.BestOf, Fige(a, an)[m.Surface] - Fige(b, an)[m.Surface],
                                         aGagne, adv, v, sA, 1 - sB, nA, nB, tid, m.Surface));
                int nSets = m.Score.Split(' ', StringSplitOptions.RemoveEmptyEntries).Count(t => t.Contains('-'));
                serres.Add(nSets == m.BestOf);
                niveaux.Add((elo.Global(a), elo.Global(b)));
            }
            elo.MettreAJour(m);
        }
        dire("Forme, confiance et constance — Jeff Sackmann tennis_atp, circuit principal + Challengers + qualifications, hors Coupe Davis,");
        dire($"dur/terre/gazon, 2010 à mai 2026, matchs complets avec statistiques de service : {obs1.Count} matchs. Elo calculé depuis 2000.");
        Rivalites.Calibrer(obs1, _ => { });
        Rivalites.Calibrer(obs2, _ => { });

        var lignes = new List<Ligne>(2 * obs1.Count);
        for (int i = 0; i < obs1.Count; i++)
        {
            var o1 = obs1[i]; var o2 = obs2[i];
            int date = o1.Date / 100;
            foreach (bool cote in new[] { true, false })
            {
                double s = cote ? 1 : -1;
                lignes.Add(new Ligne
                {
                    Joueur = cote ? o1.A : o1.B, Adversaire = cote ? o1.B : o1.A, Date = date, Annee = date / 10000, Ordre = i,
                    Tournoi = $"{date / 10000}-{o1.Tournoi}", Surface = o1.Surface, Gagne = cote == o1.AGagne, Serre = serres[i],
                    R1 = s * o1.ResAvantage, R2 = s * o2.ResAvantage, V = o1.VarianceBinomiale,
                    Rv1 = s * o1.ResVictoire, Rv2 = s * o2.ResVictoire, Niveau = cote ? niveaux[i].A : niveaux[i].B,
                });
            }
        }
        return lignes;
    }

    /// <summary>
    /// Matchs serrés (set décisif joué). Pour chaque joueur : son premier match du tournoi suivant (moins de 60 jours après)
    /// et, en placebo, son dernier match du tournoi précédent (moins de 60 jours avant).
    /// Régression : performance = a + b × (a gagné le match serré) + c × (performance dans le match serré).
    /// Confiance (portée par le résultat) = b(suivant) − b(précédent). Une forme qui dure sans lien avec le résultat donne b(suivant) ≈ b(précédent).
    /// </summary>
    private static void Confiance(Dictionary<int, List<Ligne>> parJoueur, Action<string> dire, Action<string, string, double, double, string, string> L)
    {
        var suivant = new List<(double Y, double W, double X, double Yv)>();
        var precedent = new List<(double Y, double W, double X, double Yv)>();
        foreach (var l in parJoueur.Values)
            for (int i = 0; i < l.Count; i++)
            {
                if (!l[i].Serre) continue;
                var x = l[i];
                var apres = l.Skip(i + 1).FirstOrDefault(n => n.Tournoi != x.Tournoi);
                var avant = l.Take(i).LastOrDefault(n => n.Tournoi != x.Tournoi);
                if (apres is not null && Jours(apres.Date) - Jours(x.Date) <= 60)
                    suivant.Add((apres.R2, x.Gagne ? 1 : 0, x.R2, apres.Rv2));
                if (avant is not null && Jours(x.Date) - Jours(avant.Date) <= 60)
                    precedent.Add((avant.R2, x.Gagne ? 1 : 0, x.R2, avant.Rv2));
            }
        var (bS, seS) = Coefficient(suivant.Select(t => (t.Y, t.W, t.X)).ToList());
        var (bP, seP) = Coefficient(precedent.Select(t => (t.Y, t.W, t.X)).ToList());
        var (bSv, seSv) = Coefficient(suivant.Select(t => (t.Yv, t.W, t.X)).ToList());
        var (bPv, sePv) = Coefficient(precedent.Select(t => (t.Yv, t.W, t.X)).ToList());
        dire("=== 2. Confiance : après un match serré (set décisif), effet d'avoir gagné, à qualité de jeu égale dans ce match (référence R2)");
        dire(string.Create(Ci, $"  Tournoi suivant  : performance {bS,6:+0.00;-0.00} ± {seS:F2} pts d'avantage, victoire {100 * bSv,5:+0.0;-0.0} ± {100 * seSv:F1} pts de %  ({suivant.Count} cas)"));
        dire(string.Create(Ci, $"  Tournoi précédent (placebo) : performance {bP,6:+0.00;-0.00} ± {seP:F2}, victoire {100 * bPv,5:+0.0;-0.0} ± {100 * sePv:F1}  ({precedent.Count} cas)"));
        double d = bS - bP, se = Math.Sqrt(seS * seS + seP * seP);
        double dv = bSv - bPv, sev = Math.Sqrt(seSv * seSv + sePv * sePv);
        dire(string.Create(Ci, $"  → effet propre du résultat (confiance) : performance {d:+0.00;-0.00} ± {se:F2} pts, victoire {100 * dv:+0.0;-0.0} ± {100 * sev:F1} pts de %"));
        dire("");
        L("2", "confiance_performance", d, se, "pts", "b(tournoi suivant) − b(tournoi précédent)");
        L("2", "confiance_victoire", 100 * dv, 100 * sev, "pts de %", "idem sur la victoire");
    }

    /// <summary>MCO à deux variables : coefficient de W (et son erreur-type) dans Y = a + b W + c X.</summary>
    private static (double B, double Se) Coefficient(List<(double Y, double W, double X)> d)
    {
        int n = d.Count;
        double my = d.Average(t => t.Y), mw = d.Average(t => t.W), mx = d.Average(t => t.X);
        double sww = 0, sxx = 0, swx = 0, swy = 0, sxy = 0;
        foreach (var (y, w, x) in d)
        {
            double a = w - mw, b = x - mx, c = y - my;
            sww += a * a; sxx += b * b; swx += a * b; swy += a * c; sxy += b * c;
        }
        double det = sww * sxx - swx * swx;
        double bw = (swy * sxx - sxy * swx) / det, bx = (sxy * sww - swy * swx) / det;
        double rss = d.Sum(t => Math.Pow(t.Y - my - bw * (t.W - mw) - bx * (t.X - mx), 2));
        double s2 = rss / (n - 3);
        return (bw, Math.Sqrt(s2 * sxx / det));
    }

    /// <summary>
    /// Constance comme trait du joueur : variance de sa performance (centrée sur sa moyenne) au-delà du hasard des points.
    /// Fiabilité : corrélation, entre joueurs, de cette variance mesurée sur ses matchs pairs et sur ses matchs impairs.
    /// Même chose pour l'asymétrie (mauvais jours plus rares ou plus fréquents que les très bons).
    /// </summary>
    private static void Constance(Dictionary<int, List<Ligne>> parJoueur, Dictionary<int, string> noms, double sf,
                                  Action<string> dire, Action<string, string, double, double, string, string> L, string sortie)
    {
        const int Min = 150;
        // Variance PROPRE au joueur : r² − hasard ≈ σ²(joueur) + σ²(adversaire). Résolution itérative sur tous les joueurs,
        // les joueurs avec peu de matchs étant ramenés vers la moyenne (poids n / (n + 20)).
        var propre = parJoueur.Keys.ToDictionary(j => j, _ => sf);
        for (int it = 0; it < 30; it++)
        {
            var nouveau = parJoueur.ToDictionary(kv => kv.Key, kv => kv.Value.Average(x => x.R1 * x.R1 - x.V - propre[x.Adversaire]));
            foreach (var (j, l) in parJoueur) propre[j] = sf + l.Count / (l.Count + 20.0) * (nouveau[j] - sf);
        }
        double Propre(List<Ligne> l) => l.Average(x => x.R1 * x.R1 - x.V - propre[x.Adversaire]);

        var joueurs = parJoueur.Where(kv => kv.Value.Count >= Min).ToList();
        var stats = joueurs.Select(kv =>
        {
            var l = kv.Value;
            var pairs = l.Where((_, i) => i % 2 == 0).ToList();
            var impairs = l.Where((_, i) => i % 2 == 1).ToList();
            double seV = Math.Sqrt(l.Select(x => x.R1 * x.R1).Let(v => Variance(v) / l.Count));
            return (Id: kv.Key, N: l.Count, Var: Propre(l), Asym: Moments(l).Asym, VarP: Propre(pairs), VarI: Propre(impairs),
                    AsymP: Moments(pairs).Asym, AsymI: Moments(impairs).Asym, SeVar: seV, Niveau: l.Average(x => x.Niveau));
        }).ToList();

        // Le niveau lui-même : la constance est-elle seulement un reflet du niveau ?
        double rNiveau = Correlation(stats.Select(s => (s.Var, s.Niveau)).ToList());
        double[] Residus(Func<(int Id, int N, double Var, double Asym, double VarP, double VarI, double AsymP, double AsymI, double SeVar, double Niveau), double> f)
        {
            var xs = stats.Select(s => s.Niveau).ToList(); var ys = stats.Select(f).ToList();
            double mx = xs.Average(), my = ys.Average();
            double b = xs.Zip(ys).Sum(p => (p.First - mx) * (p.Second - my)) / xs.Sum(x => (x - mx) * (x - mx));
            return xs.Zip(ys).Select(p => p.Second - my - b * (p.First - mx)).ToArray();
        }
        var resP = Residus(s => s.VarP); var resI = Residus(s => s.VarI);
        double rPartiel = Correlation(resP.Zip(resI).Select(p => (p.First, p.Second)).ToList());

        double rVar = Correlation(stats.Select(s => (s.VarP, s.VarI)).ToList());
        double rAsym = Correlation(stats.Select(s => (s.AsymP, s.AsymI)).ToList());
        var rng = new Random(1);
        var bootV = new List<double>(); var bootA = new List<double>();
        for (int b = 0; b < 500; b++)
        {
            var e = Enumerable.Range(0, stats.Count).Select(_ => stats[rng.Next(stats.Count)]).ToList();
            bootV.Add(Correlation(e.Select(s => (s.VarP, s.VarI)).ToList()));
            bootA.Add(Correlation(e.Select(s => (s.AsymP, s.AsymI)).ToList()));
        }
        double moyVar = stats.Average(s => s.Var);
        // Dispersion vraie entre joueurs de leur variance de forme (fiabilité de Spearman-Brown appliquée à l'écart-type observé).
        double fiab = 2 * rVar / (1 + rVar);
        double etVarVrai = Math.Sqrt(Math.Max(0, fiab)) * Math.Sqrt(Variance(stats.Select(s => s.Var)));

        dire($"=== 3. Constance : certains joueurs sont-ils durablement plus irréguliers ? ({stats.Count} joueurs d'au moins {Min} matchs, référence R1)");
        dire(string.Create(Ci, $"  Variance de forme PROPRE au joueur (adversaires et hasard retirés) : moyenne {moyVar:F1} pts² (écart-type {Math.Sqrt(Math.Max(0, moyVar)):F1} pts d'avantage au service)"));
        dire(string.Create(Ci, $"  Fiabilité matchs pairs / impairs : r = {rVar:F2} ± {Et(bootV):F2}  (0 = tout le monde pareil, 1 = trait parfaitement stable)"));
        dire(string.Create(Ci, $"    → écart-type vrai entre joueurs de cette variance ≈ {etVarVrai:F1} pts²"));
        dire(string.Create(Ci, $"  Lien avec le niveau : corrélation variance / Elo moyen = {rNiveau:F2} ; fiabilité à niveau égal (partielle) r = {rPartiel:F2}"));
        L("3", "correlation_variance_niveau", rNiveau, double.NaN, "", "les meilleurs sont-ils plus réguliers ?");
        L("3", "fiabilite_a_niveau_egal", rPartiel, double.NaN, "", "corrélation pairs/impairs des résidus après niveau");
        dire(string.Create(Ci, $"  Fiabilité pairs / impairs de l'asymétrie (mauvais jours contre très bons jours) : r = {rAsym:F2} ± {Et(bootA):F2}"));
        L("3", "fiabilite_variance_forme", rVar, Et(bootV), "", "corrélation pairs/impairs entre joueurs");
        L("3", "ecart_type_vrai_variance_entre_joueurs", etVarVrai, double.NaN, "pts²", "dispersion réelle de la constance");
        L("3", "fiabilite_asymetrie", rAsym, Et(bootA), "", "corrélation pairs/impairs entre joueurs");

        // Biais de style possible : la variance suit-elle le profil serveur ?
        var profil = stats.Select(s => (s.Var, Serve: parJoueur[s.Id].Average(x => x.Surface == "Grass" ? 1.0 : 0.0))).ToList();
        string[] cites = ["Jannik Sinner", "Roger Federer", "Novak Djokovic", "Rafael Nadal", "Carlos Alcaraz", "Tommy Paul", "Quentin Halys",
                          "Alexander Zverev", "Casper Ruud", "Stefanos Tsitsipas", "Daniil Medvedev", "Frances Tiafoe", "Nick Kyrgios",
                          "Alexander Bublik", "Benoit Paire", "Andrey Rublev", "Holger Rune"];
        var ordonnes = stats.OrderBy(s => s.Var).ToList();
        string Fiche((int Id, int N, double Var, double Asym, double VarP, double VarI, double AsymP, double AsymI, double SeVar, double Niveau) s) =>
            string.Create(Ci, $"{noms.GetValueOrDefault(s.Id),-24} {s.N,4} matchs  variance {s.Var,6:F1} ± {s.SeVar,4:F1}  rang {ordonnes.IndexOf(s) + 1,3}/{stats.Count}  asymétrie {s.Asym,5:+0.00;-0.00}");
        dire("  Joueurs cités (variance basse = régulier ; asymétrie négative = plus de très mauvais jours que de très bons) :");
        foreach (var n in cites)
        {
            var s = stats.FirstOrDefault(x => noms.GetValueOrDefault(x.Id) == n);
            dire(s.Id == 0 ? $"    {n,-24} moins de {Min} matchs" : $"    {Fiche(s)}");
        }
        dire("  Les 8 plus réguliers :");
        foreach (var s in ordonnes.Take(8)) dire($"    {Fiche(s)}");
        dire("  Les 8 plus irréguliers :");
        foreach (var s in ordonnes.TakeLast(8)) dire($"    {Fiche(s)}");
        dire("");

        var csv = new StringBuilder("joueur,matchs,variance_forme_pts2,erreur_type,variance_pairs,variance_impairs,asymetrie\n");
        foreach (var s in ordonnes)
            csv.AppendLine(string.Create(Ci, $"\"{noms.GetValueOrDefault(s.Id)}\",{s.N},{s.Var:F2},{s.SeVar:F2},{s.VarP:F2},{s.VarI:F2},{s.Asym:F3}"));
        File.WriteAllText(Path.Combine(sortie, "constance_par_joueur.csv"), csv.ToString());
    }

    /// <summary>Variance (hasard des points retiré) et asymétrie de la performance d'un joueur, centrée sur sa moyenne.</summary>
    private static (double Var, double Asym) Moments(List<Ligne> l)
    {
        double m = l.Average(x => x.R1);
        double v = l.Average(x => (x.R1 - m) * (x.R1 - m));
        double m3 = l.Average(x => Math.Pow(x.R1 - m, 3));
        return (v - l.Average(x => x.V), m3 / Math.Pow(v, 1.5));
    }

    private static double Correlation(List<(double X, double Y)> d)
    {
        double mx = d.Average(t => t.X), my = d.Average(t => t.Y);
        double sxy = d.Sum(t => (t.X - mx) * (t.Y - my)), sxx = d.Sum(t => (t.X - mx) * (t.X - mx)), syy = d.Sum(t => (t.Y - my) * (t.Y - my));
        return sxy / Math.Sqrt(sxx * syy);
    }

    private static double Variance(IEnumerable<double> x)
    {
        var l = x.ToList();
        double m = l.Average();
        return l.Sum(v => (v - m) * (v - m)) / (l.Count - 1);
    }

    private static double Et(List<double> v) => Math.Sqrt(Variance(v));

    private static double Jours(int date) =>
        new DateTime(date / 10000, Math.Clamp(date / 100 % 100, 1, 12), Math.Clamp(date % 100, 1, 28)).Subtract(DateTime.MinValue).TotalDays;

    private static TR Let<T, TR>(this T x, Func<T, TR> f) => f(x);
}
