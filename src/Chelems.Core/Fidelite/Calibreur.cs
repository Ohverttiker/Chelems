using Chelems.Core.Configuration;

namespace Chelems.Core.Fidelite;

/// <summary>Une surface prête à être mesurée : sa population de test et ses cibles ATP.</summary>
public sealed record Banc(Surface Surface, PopulationAtp Population, CiblesAtp Cibles)
{
    public static Banc Construire(CheminsDepot chemins, Surface s, int tiragesParJoueur, int graine) =>
        new(s, PopulationAtp.Construire(chemins.Classements, chemins.Adversaires(s), tiragesParJoueur, graine,
                                        File.Exists(chemins.ConfigPopulation) ? ConfigPopulation.Charger(chemins.ConfigPopulation) : null),
            CiblesAtp.Charger(chemins.Objectifs(s)));

    public Mesures Mesurer(ConfigMoteur cfg, int graine) =>
        SimulationMasse.Executer(new MoteurMatch(new ModelePoint(cfg)), Population, Surface, graine);
}

/// <summary>Un paramètre de calibrage : lecture, écriture (copie de la config) et pas de départ.</summary>
public sealed record Parametre(string Nom, Func<ConfigMoteur, double> Lire, Func<ConfigMoteur, double, ConfigMoteur> Ecrire, double Pas,
                               bool Negatif = false)
{
    public static Parametre Vitesse(Surface s) => new($"vitesse {ConfigMoteur.SurfaceCle(s)}",
        c => c.Surface(s).Vitesse,
        (c, v) => c with { Surfaces = new(c.Surfaces) { [ConfigMoteur.SurfaceCle(s)] = c.Surface(s) with { Vitesse = v } } }, 0.05, Negatif: true);

    public static readonly Parametre PenteService = new("service.pente",
        c => c.Service.Pente, (c, v) => c with { Service = c.Service with { Pente = v } }, 0.004);

    public static readonly Parametre PenteEchange = new("echange.pente",
        c => c.Echange.Pente, (c, v) => c with { Echange = c.Echange with { Pente = v } }, 0.004);

    /// <summary>Les deux pentes multipliées par un même facteur (garde le partage entre service et échange).</summary>
    public static readonly Parametre Pentes = new("pentes (service, échange)",
        c => c.Service.Pente,
        (c, v) => c with
        {
            Service = c.Service with { Pente = v },
            Echange = c.Echange with { Pente = c.Service.Pente == 0 ? c.Echange.Pente : Math.Round(c.Echange.Pente * v / c.Service.Pente, 6) },
        }, 0.004);
}

/// <summary>
/// Outil de l'agent d'équilibrage : descente par coordonnées sur une liste de paramètres, en minimisant la somme des chi2
/// des familles choisies sur toutes les surfaces données. Les mêmes graines servent à chaque essai (nombres aléatoires communs).
/// </summary>
public static class Calibreur
{
    public sealed record Resultat(ConfigMoteur Config, double Chi2, int Evaluations);

    public static double Chi2(ConfigMoteur cfg, IReadOnlyList<Banc> bancs, IEnumerable<string> familles, int graine, Func<CleMesure, bool>? exclure = null) =>
        bancs.Sum(b => Comparaison.Chi2(b.Cibles, b.Mesurer(cfg, graine), familles, exclure));

    public static Resultat Ajuster(ConfigMoteur depart, IReadOnlyList<Banc> bancs, IReadOnlyList<Parametre> parametres,
                                   IReadOnlyList<string> familles, int graine, int tours = 4, Action<string>? journal = null,
                                   Func<CleMesure, bool>? exclure = null)
    {
        int evaluations = 0;
        double Evaluer(ConfigMoteur c) { evaluations++; return Chi2(c, bancs, familles, graine, exclure); }

        var cfg = depart;
        double meilleur = Evaluer(cfg);
        journal?.Invoke($"départ : chi2 = {meilleur:F0}");
        var pas = parametres.Select(p => p.Pas).ToArray();
        for (int tour = 0; tour < tours; tour++)
        {
            for (int k = 0; k < parametres.Count; k++)
                foreach (int signe in (int[])[-1, 1])
                    while (true)
                    {
                        var essai = Essai(cfg, parametres[k], signe * pas[k]);
                        if (essai is null) break;
                        double x = Evaluer(essai);
                        if (x >= meilleur) break;
                        meilleur = x;
                        cfg = essai;
                    }
            journal?.Invoke($"tour {tour + 1} : chi2 = {meilleur:F0}  " +
                            string.Join("  ", parametres.Select(p => $"{p.Nom} = {p.Lire(cfg):F4}")));
            for (int k = 0; k < pas.Length; k++) pas[k] /= 2;
        }
        return new Resultat(cfg, meilleur, evaluations);
    }

    private static ConfigMoteur? Essai(ConfigMoteur cfg, Parametre p, double delta)
    {
        double v = Math.Round(p.Lire(cfg) + delta, 6); // pas de bruit d'arrondi binaire dans le JSON
        if (v < 0 && !p.Negatif) return null;
        var c = p.Ecrire(cfg, v);
        try { c.Valider(); } catch (InvalidDataException) { return null; }
        return c;
    }
}
