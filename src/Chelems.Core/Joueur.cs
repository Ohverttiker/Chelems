namespace Chelems.Core;

/// <summary>Surfaces modélisées. Indoor et court pace index par tournoi : pas encore (pas de données).</summary>
public enum Surface { Dur, Terre, Gazon }

/// <summary>
/// Joueur de la V0.3 : trois stats (échelle du simulateur de circuit, 50-99 pour les pros).
/// Service : duel au service, côté serveur. Retour : même duel, côté relanceur. Échange : tout le reste du point (non découpé).
/// </summary>
public sealed record Joueur(string Nom, double Service, double Retour, double Echange)
{
    /// <summary>Joueur dont les trois stats valent le talent du simulateur de circuit.</summary>
    public Joueur(string nom, double talent) : this(nom, talent, talent, talent) { }

    /// <summary>Niveau global provisoire (CLAUDE.md §4.5) : moyenne des trois stats, à poids égaux.</summary>
    public double Talent => (Service + Retour + Echange) / 3;
}

public static class Aleatoire
{
    /// <summary>Loi normale centrée réduite (Box-Muller).</summary>
    public static double Gauss(Random rng)
    {
        double u1 = 1.0 - rng.NextDouble();
        double u2 = rng.NextDouble();
        return Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Cos(2.0 * Math.PI * u2);
    }
}
