using Chelems.Core.Configuration;

namespace Chelems.Core.Fidelite;

/// <summary>
/// Spécialisation de la population de test (roadmap, étape 1) : écart-type et corrélation des écarts de Service et de Retour au talent
/// (<see cref="ConfigPopulation.Profil(string, double, int, int, double, double)"/>), choisis pour que les joueurs classés de 1 à
/// <c>rangMax</c> retrouvent la dispersion de leurs points gagnés au service et au retour, et leur corrélation, mesurées à l'ATP.
/// Calcul exact (<see cref="AnalysePoint"/>), face à un adversaire de référence, sur toutes les saisons du simulateur de circuit.
/// </summary>
public static class CaleurSpecialisation
{
    /// <summary>[Créateur, 2026-11-26] Corrélation négative modérée entre les profils Service et Retour, pour la part que la taille n'explique pas.</summary>
    public const double BorneCorrelation = 0.6;

    public sealed record Dispersion(double EcartTypeService, double EcartTypeRetour, double Correlation);

    /// <summary>Cibles mesurées : classement ATP sur 52 semaines (top 81, toutes surfaces), service = 1re et 2e pondérées, retour ≈ 0,63 × sur 1re + 0,37 × sur 2e.</summary>
    public static Dispersion MesurerAtp(string fichierClassementsAtp)
    {
        var s = new List<double>();
        var r = new List<double>();
        foreach (var l in Csv.Lire(fichierClassementsAtp))
        {
            if (l["surface"] != "all") continue;
            double p1 = l.Reel("serve_FirstServePct");
            s.Add((p1 * l.Reel("serve_FirstServePointsWonPct") + (100 - p1) * l.Reel("serve_SecondServePointsWonPct")) / 100);
            r.Add(0.63 * l.Reel("return_FirstServeReturnPointsWonPct") + 0.37 * l.Reel("return_SecondServeReturnPointsWonPct"));
        }
        return Statistiques(s, r);
    }

    /// <summary>Joueurs du simulateur de circuit (toutes saisons) classés de 1 à rangMax, avec leur talent.</summary>
    public static List<(int Annee, int Rang, double Talent)> JoueursDuSimulateur(string cheminClassements, int rangMax) =>
        [.. Csv.Lire(cheminClassements).Where(l => l.Entier("rang") <= rangMax).Select(l => (l.Entier("annee"), l.Entier("rang"), l.Reel("talent")))];

    public static Dispersion Simuler(ConfigMoteur cfg, Surface surface, IReadOnlyList<(int Annee, int Rang, double Talent)> joueurs,
                                     double ecartType, double correlation, double partTalent = 1, double reference = 72,
                                     ConfigPopulation? physique = null, double niveauAdversaire = 72.8)
    {
        var modele = new ModelePoint(cfg);
        var adversaire = new Joueur("Référence", niveauAdversaire);
        var res = joueurs.AsParallel().AsOrdered().Select(j =>
        {
            var p = ConfigPopulation.Profil("", j.Talent, j.Annee, j.Rang, ecartType, correlation, partTalent, reference, physique);
            double service = AnalysePoint.Calculer(modele.Preparer(p, adversaire, surface)).Serveur;
            double retour = 1 - AnalysePoint.Calculer(modele.Preparer(adversaire, p, surface)).Serveur;
            return (100 * service, 100 * retour);
        }).ToList();
        return Statistiques([.. res.Select(x => x.Item1)], [.. res.Select(x => x.Item2)]);
    }

    public static (double EcartType, double Correlation, Dispersion Obtenue) Caler(ConfigMoteur cfg, Surface surface,
        IReadOnlyList<(int Annee, int Rang, double Talent)> joueurs, Dispersion cible, double partTalent = 1, double reference = 72, ConfigPopulation? physique = null, Action<string>? journal = null)
    {
        double F(double[] x)
        {
            double et = Math.Abs(x[0]), rho = Math.Clamp(x[1], -BorneCorrelation, BorneCorrelation);
            var d = Simuler(cfg, surface, joueurs, et, rho, partTalent, reference, physique);
            return Math.Pow((d.EcartTypeService - cible.EcartTypeService) / 0.2, 2) + Math.Pow((d.EcartTypeRetour - cible.EcartTypeRetour) / 0.2, 2)
                   + Math.Pow((d.Correlation - cible.Correlation) / 0.05, 2) + 10 * Math.Pow(Math.Max(0, Math.Abs(x[1]) - BorneCorrelation), 2);
        }
        var x = CaleurEchange.NelderMead(F, [5, -0.3], 2, 60);
        double e = Math.Round(Math.Abs(x[0]), 2), c = Math.Round(Math.Clamp(x[1], -BorneCorrelation, BorneCorrelation), 3);
        var obtenue = Simuler(cfg, surface, joueurs, e, c, partTalent, reference, physique);
        journal?.Invoke($"écart-type {e}, corrélation {c} : service {obtenue.EcartTypeService:F2}, retour {obtenue.EcartTypeRetour:F2}, corrélation {obtenue.Correlation:F2}");
        return (e, c, obtenue);
    }

    private static Dispersion Statistiques(List<double> s, List<double> r)
    {
        double ms = s.Average(), mr = r.Average();
        double vs = s.Sum(v => (v - ms) * (v - ms)) / s.Count, vr = r.Sum(v => (v - mr) * (v - mr)) / r.Count;
        double cov = s.Zip(r).Sum(p => (p.First - ms) * (p.Second - mr)) / s.Count;
        return new(Math.Sqrt(vs), Math.Sqrt(vr), cov / Math.Sqrt(vs * vr));
    }
}
