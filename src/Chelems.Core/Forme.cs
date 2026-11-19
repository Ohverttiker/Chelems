using Chelems.Core.Configuration;

namespace Chelems.Core;

/// <summary>
/// Phase de forme d'un joueur qui dure d'un match à l'autre (seule variable dynamique stockée, justifiée par la mesure
/// du principe 6). Bornée par construction : processus autorégressif qui revient vers 0.
/// Le simulateur de circuit garde un état par joueur et appelle <see cref="ModeleForme.Avancer"/> après chaque match.
/// </summary>
public sealed class EtatForme
{
    public double Lente { get; internal set; }
}

/// <summary>Tirages de forme (en probabilité de gagner un point au service), modulés par la Constance.</summary>
public sealed class ModeleForme(ConfigMoteur config)
{
    private readonly ConfigForme _f = config.Forme;
    private readonly ConfigConstance _c = config.Constance;
    private readonly double _reductionEnjeu = config.Enjeu?.Reduction ?? 0;

    /// <summary>Multiplicateur de variance : 1 + amplitude × (50 − Constance) / 49 (de 1 − a à 1 + a sur l'échelle 1-99).</summary>
    public double Multiplicateur(Joueur j) => 1 + _c.Amplitude * (50 - Math.Clamp(j.Constance, 1, 99)) / 49;

    private double EcartTypeLente(Joueur j) => _f.EcartTypeMatch * Math.Sqrt(_f.PartLente * Multiplicateur(j));
    private double EcartTypeJour(Joueur j) => _f.EcartTypeMatch * Math.Sqrt((1 - _f.PartLente) * Multiplicateur(j));
    public double EcartTypeSet(Joueur j) => _f.EcartTypeSet * Math.Sqrt(Multiplicateur(j));

    /// <summary>Phase lente tirée dans sa loi stationnaire (joueur sans historique, ou matchs indépendants).</summary>
    public double TirerLente(Joueur j, Random rng) => EcartTypeLente(j) * Aleatoire.Gauss(rng);

    /// <summary>Facteur de variance de l'aléa du jour selon l'enjeu (0 à 1) : 1 − réduction × enjeu × (Constance − 1) / 98.</summary>
    public double FacteurEnjeu(Joueur j, double enjeu) =>
        1 - _reductionEnjeu * Math.Clamp(enjeu, 0, 1) * (Math.Clamp(j.Constance, 1, 99) - 1) / 98;

    /// <summary>Forme fixe sur tout le match = phase lente + aléa du jour (réduit par l'enjeu selon la Constance).</summary>
    public double FormeDuMatch(Joueur j, double lente, Random rng, double enjeu = 0) =>
        lente + EcartTypeJour(j) * Math.Sqrt(FacteurEnjeu(j, enjeu)) * Aleatoire.Gauss(rng);

    public double FormeDuSet(Joueur j, Random rng) => EcartTypeSet(j) * Aleatoire.Gauss(rng);

    /// <summary>Fait évoluer la phase lente d'un match (AR(1) de persistance ρ, variance stationnaire conservée).</summary>
    public void Avancer(EtatForme etat, Joueur j, Random rng)
    {
        double rho = _f.PersistanceLente;
        etat.Lente = rho * etat.Lente + Math.Sqrt(1 - rho * rho) * EcartTypeLente(j) * Aleatoire.Gauss(rng);
    }
}
