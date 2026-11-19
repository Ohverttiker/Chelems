namespace Chelems.Core;

/// <summary>Résultats exacts (points indépendants), utilisés pour contrôler le moteur aléatoire.</summary>
public static class FormulesExactes
{
    /// <summary>Probabilité que le serveur tienne son jeu quand il gagne chaque point avec la probabilité p.</summary>
    public static double ProbaTenirJeu(double p)
    {
        double q = 1 - p;
        double avantEgalite = Math.Pow(p, 4) * (1 + 4 * q + 10 * q * q);
        double egalite = 20 * Math.Pow(p, 3) * Math.Pow(q, 3);
        return avantEgalite + egalite * p * p / (1 - 2 * p * q);
    }
}
