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
        bool AGagne, int Sets, int TieBreaks, bool Meme12);

    public static Mesures Executer(MoteurMatch moteur, PopulationAtp population, Surface surface, int graine)
    {
        var rencontres = population.Rencontres;
        var bilans = new Bilan[rencontres.Count];
        Parallel.For(0, rencontres.Count, i =>
        {
            var r = rencontres[i];
            var rng = new Random(Graines.Deriver(graine, i + 1000));
            var res = moteur.Jouer(population.Joueur(r.Annee, r.Rang), population.Joueur(r.Annee, r.RangAdversaire),
                                   FormatMatch.DepuisBestOf(r.BestOf), surface, rng, enjeu: moteur.Modele.Config.Enjeu?.Valeur(r.Niveau) ?? 0);
            var a = res.Service[0];
            var b = res.Service[1];
            bilans[i] = new Bilan(a.PointsJoues, a.PointsGagnes, a.Jeux, a.JeuxTenus, a.BallesDeBreakSubies, a.BallesDeBreakSauvees,
                                  b.PointsJoues, b.PointsGagnes, b.Jeux, b.JeuxTenus, b.BallesDeBreakSubies, b.BallesDeBreakSauvees,
                                  res.Vainqueur == 0, res.Sets.Count, res.NombreTieBreaks,
                                  res.Sets[0].Gagnant == res.Sets[1].Gagnant);
        });
        return Agreger(rencontres, bilans);
    }

    private static Mesures Agreger(IReadOnlyList<Rencontre> rencontres, Bilan[] bilans)
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
