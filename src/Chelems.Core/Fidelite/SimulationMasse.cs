namespace Chelems.Core.Fidelite;

/// <summary>Valeurs simulées, avec les mêmes clés et unités que <see cref="CiblesAtp"/>.</summary>
public sealed class Mesures
{
    public required IReadOnlyDictionary<CleMesure, double> Valeurs { get; init; }
    public required int Matchs { get; init; }
    public required long Points { get; init; }
}

/// <summary>
/// Simulation de masse point par point, comptée exactement comme les cibles ATP (README des objectifs, règle 4) :
/// - hold et points au service par tranche : du point de vue du joueur du top 150 qui a tiré la rencontre ;
/// - résultats, sets, tie-breaks, dépendance des sets et dispersion : une ligne par match, quand le joueur
///   qui a tiré la rencontre est le mieux classé.
/// Reproductible : chaque rencontre a son propre flux aléatoire dérivé de la graine et de son indice,
/// ce qui permet aussi de paralléliser sans changer les résultats.
/// </summary>
public static class SimulationMasse
{
    private readonly record struct Bilan(
        int PtsJoA, int PtsGaA, int JeuxA, int TenusA, int BbA, int BbSauvA,
        int PtsJoB, int PtsGaB, int JeuxB, int TenusB, int BbB, int BbSauvB,
        bool AGagne, int Sets, int TieBreaks, bool Meme12,
        int PremA, int Gagne1A, int Gagne2A, int AcesA, int DfA);

    /// <summary>Compteurs sur tous les points de tous les matchs (deux joueurs) : longueur des échanges, issues du service, conversion.</summary>
    private sealed class Global
    {
        public readonly long[] Points = new long[4], ServeurGagne = new long[4];
        public readonly long[][] Fins = [new long[3], new long[3], new long[3], new long[3]];
        public readonly long[][] Issues = [new long[7], new long[7]];
        public long FautesRetour, Svpt, Aces, Df, PremIn, Gagne1, Gagne2, Attaques, AttaquesGagnees;

        public void Ajouter(ResultatMatch r)
        {
            lock (this)
            {
                for (int t = 0; t < 4; t++)
                {
                    Points[t] += r.Longueurs.Points[t];
                    ServeurGagne[t] += r.Longueurs.ServeurGagne[t];
                    for (int k = 0; k < 3; k++) Fins[t][k] += r.Longueurs.Fins[t][k];
                }
                for (int i = 0; i < 2; i++)
                {
                    var s = r.Service[i];
                    for (int b = 0; b < 2; b++) for (int k = 0; k < 7; k++) Issues[b][k] += s.Issues[b][k];
                    Svpt += s.PointsJoues; Aces += s.Aces; Df += s.DoublesFautes; PremIn += s.PremieresRentrees;
                    Gagne1 += s.GagnesPremiere; Gagne2 += s.GagnesSeconde;
                    Attaques += r.Echange[i].Attaques; AttaquesGagnees += r.Echange[i].AttaquesGagnees;
                    FautesRetour += r.Echange[i].FautesDirectesRetour;
                }
            }
        }
    }

    public static Mesures Executer(MoteurMatch moteur, PopulationAtp population, Surface surface, int graine)
    {
        var rencontres = population.Rencontres;
        var bilans = new Bilan[rencontres.Count];
        var global = new Global();
        Parallel.For(0, rencontres.Count, i =>
        {
            var r = rencontres[i];
            var rng = new Random(Graines.Deriver(graine, i + 1000));
            var res = moteur.Jouer(population.Joueur(r.Annee, r.Rang), population.Joueur(r.Annee, r.RangAdversaire),
                                   FormatMatch.DepuisBestOf(r.BestOf), surface, rng);
            global.Ajouter(res);
            var a = res.Service[0];
            var b = res.Service[1];
            bilans[i] = new Bilan(a.PointsJoues, a.PointsGagnes, a.Jeux, a.JeuxTenus, a.BallesDeBreakSubies, a.BallesDeBreakSauvees,
                                  b.PointsJoues, b.PointsGagnes, b.Jeux, b.JeuxTenus, b.BallesDeBreakSubies, b.BallesDeBreakSauvees,
                                  res.Vainqueur == 0, res.Sets.Count, res.NombreTieBreaks,
                                  res.Sets[0].Gagnant == res.Sets[1].Gagnant,
                                  a.PremieresRentrees, a.GagnesPremiere, a.GagnesSeconde, a.Aces, a.DoublesFautes);
        });
        return Agreger(rencontres, bilans, global);
    }

    /// <summary>Familles « mcp » (Match Charting Project, tous les points) et « efficacite » (chiffres du créateur), mêmes définitions que les fichiers cibles.</summary>
    private static void AjouterGlobal(Global g, Action<CleMesure, double, double> ajouter)
    {
        long total = g.Points.Sum();
        string[] noms = AnalysePoint.NomsTranches;
        for (int t = 0; t < 4; t++)
        {
            ajouter(new("mcp", 0, $"part_{noms[t]}"), g.Points[t], total);
            ajouter(new("mcp", 0, $"serveur_{noms[t]}"), g.ServeurGagne[t], g.Points[t]);
        }
        string[] fins = ["gagnants", "provoquees", "directes"];
        for (int k = 0; k < 3; k++)
        {
            for (int t = 0; t < 4; t++) ajouter(new("mcp", 0, $"{fins[k]}_{noms[t]}"), g.Fins[t][k], g.Points[t]);
            ajouter(new("mcp", 0, $"{fins[k]}_total"), g.Fins.Sum(f => f[k]), total);
        }
        ajouter(new("mcp", 0, "aces"), g.Aces, g.Svpt);
        ajouter(new("mcp", 0, "doubles_fautes"), g.Df, g.Svpt);
        ajouter(new("mcp", 0, "premieres_balles"), g.PremIn, g.Svpt);
        ajouter(new("mcp", 0, "points_gagnes_1re"), g.Gagne1, g.PremIn);
        ajouter(new("mcp", 0, "points_gagnes_2e"), g.Gagne2, g.Svpt - g.PremIn);
        ajouter(new("mcp", 0, "points_gagnes_service"), g.Gagne1 + g.Gagne2, g.Svpt);

        // Issues du service mesurées par le MCP, et fins de point hors service (aces, services gagnants, doubles fautes exclus).
        long n1 = g.Issues[0].Sum(), n2 = g.Issues[1].Sum();
        ajouter(new("mcp", 0, "premiere_ace"), g.Issues[0][0], n1);
        ajouter(new("mcp", 0, "premiere_gagnant"), g.Issues[0][1], n1);
        ajouter(new("mcp", 0, "seconde_ace"), g.Issues[1][0], n2);
        ajouter(new("mcp", 0, "seconde_gagnant"), g.Issues[1][1], n2);
        ajouter(new("mcp", 0, "seconde_double_faute"), g.Issues[1][6], n2);
        long servicesGagnants = g.Issues[0][1] + g.Issues[1][1];
        double horsService = total - g.Aces - servicesGagnants - g.Df;
        ajouter(new("mcp", 0, "gagnants_hors_service"), g.Fins.Sum(f => f[0]) - g.Aces, horsService);
        ajouter(new("mcp", 0, "provoquees_hors_service"), g.Fins.Sum(f => f[1]) - (servicesGagnants - g.FautesRetour), horsService);
        ajouter(new("mcp", 0, "directes_hors_service"), g.Fins.Sum(f => f[2]) - g.FautesRetour, horsService);

        string[] issues = ["ace", "gagnant", "attaque", "neutre", "defense", "retour_gagnant", "double_faute"];
        for (int b = 0; b < 2; b++)
        {
            long n = g.Issues[b].Sum();
            for (int k = 0; k < (b == 0 ? 6 : 7); k++)
                ajouter(new("efficacite", 0, $"{(b == 0 ? "premiere" : "seconde")}_{issues[k]}"), g.Issues[b][k], n);
        }
        ajouter(new("efficacite", 0, "premieres_balles"), g.PremIn, g.Svpt);
        ajouter(new("efficacite", 0, "conversion"), g.AttaquesGagnees, g.Attaques);
        ajouter(new("efficacite", 0, "steal"), g.Attaques - g.AttaquesGagnees, g.Attaques);
    }

    private static Mesures Agreger(IReadOnlyList<Rencontre> rencontres, Bilan[] bilans, Global g)
    {
        var ratios = new Dictionary<CleMesure, (double Num, double Den)>();
        var disp = new Dictionary<CleMesure, (double S, double S2, int N)>();
        long points = 0;

        void Ajouter(CleMesure cle, double num, double den)
        {
            ratios.TryGetValue(cle, out var v);
            ratios[cle] = (v.Num + num, v.Den + den);
        }

        for (int i = 0; i < bilans.Length; i++)
        {
            var r = rencontres[i];
            var b = bilans[i];
            int bo = r.BestOf;
            points += b.PtsJoA + b.PtsJoB;

            // Point de vue du joueur du top 150.
            string tranche = Categories.Dans(Categories.TranchesRang, r.Rang)!;
            Ajouter(new("hold", bo, tranche), b.TenusA, b.JeuxA);
            Ajouter(new("pts", bo, tranche), b.PtsGaA, b.PtsJoA);
            Ajouter(new("moyen", 0, "hold"), b.TenusA, b.JeuxA);
            Ajouter(new("moyen", 0, "points_gagnes_service"), b.PtsGaA, b.PtsJoA);
            Ajouter(new("moyen", 0, "balles_de_break_sauvees"), b.BbSauvA, b.BbA);
            Ajouter(new("moyen", 0, "premieres_balles"), b.PremA, b.PtsJoA);
            Ajouter(new("moyen", 0, "points_gagnes_1re"), b.Gagne1A, b.PremA);
            Ajouter(new("moyen", 0, "points_gagnes_2e"), b.Gagne2A, b.PtsJoA - b.PremA);
            Ajouter(new("moyen", 0, "aces_par_point_service"), b.AcesA, b.PtsJoA);
            Ajouter(new("moyen", 0, "doubles_fautes_par_point_service"), b.DfA, b.PtsJoA);
            Ajouter(new("gagne", bo, tranche), b.AGagne ? 1 : 0, 1);
            Ajouter(new("retour", bo, tranche), b.JeuxB - b.TenusB, b.JeuxB);
            Ajouter(new("pts_retour", bo, tranche), b.PtsJoB - b.PtsGaB, b.PtsJoB);
            // Balles de break (au moins 3) : % sauvées − % de points gagnés au service du match, et l'inverse au retour (sans cible ATP figée).
            if (b.BbA >= 3) Ajouter(new("clutch_service", 0, tranche), (double)b.BbSauvA / b.BbA - (double)b.PtsGaA / b.PtsJoA, 1);
            if (b.BbB >= 3) Ajouter(new("clutch_retour", 0, tranche), (1 - (double)b.BbSauvB / b.BbB) - (1 - (double)b.PtsGaB / b.PtsJoB), 1);

            // Niveau match : le joueur qui a tiré la rencontre est le mieux classé.
            if (r.Rang >= r.RangAdversaire) continue;
            int ecart = r.RangAdversaire - r.Rang;
            string e = Categories.Dans(Categories.EcartsPlaces, ecart)!;
            Ajouter(new("vict", bo, e), b.AGagne ? 1 : 0, 1);
            Ajouter(new("hold_mieux", bo, e), b.TenusA, b.JeuxA);
            Ajouter(new("hold_moins", bo, e), b.TenusB, b.JeuxB);
            Ajouter(new("score", bo, "sets_au_tiebreak"), b.TieBreaks, b.Sets);
            for (int n = (bo + 1) / 2; n <= bo; n++)
                Ajouter(new("score", bo, $"matchs_en_{n}_sets"), b.Sets == n ? 1 : 0, 1);
            if (bo == 3)
            {
                Ajouter(new("deux_sets", 3, e), b.Sets == 2 ? 1 : 0, 1);
                Ajouter(new("meme12", 3, e), b.Meme12 ? 1 : 0, 1);
            }

            double avantage = 100.0 * ((double)b.PtsGaA / b.PtsJoA - (double)b.PtsGaB / b.PtsJoB);
            var cle = new CleMesure("disp", bo, Categories.Dans(Categories.EcartsDispersion, ecart)!);
            disp.TryGetValue(cle, out var d);
            disp[cle] = (d.S + avantage, d.S2 + avantage * avantage, d.N + 1);
        }

        AjouterGlobal(g, Ajouter);

        var valeurs = ratios.Where(kv => kv.Value.Den > 0).ToDictionary(kv => kv.Key, kv => kv.Value.Num / kv.Value.Den);
        foreach (var (cle, (s, s2, n)) in disp)
        {
            double moyenne = s / n;
            valeurs[cle with { Famille = "disp_moy" }] = moyenne;
            valeurs[cle with { Famille = "disp_et" }] = n > 1 ? Math.Sqrt((s2 - n * moyenne * moyenne) / (n - 1)) : double.NaN;
        }
        return new Mesures { Valeurs = valeurs, Matchs = bilans.Length, Points = points };
    }
}
