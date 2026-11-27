namespace Chelems.Core;

/// <summary>Surfaces modélisées. Indoor et court pace index par tournoi : pas encore (pas de données).</summary>
public enum Surface { Dur, Terre, Gazon }

/// <summary>
/// Joueur (échelle 1-99, 50-99 pour les pros ; taille en cm).
/// Service : qualité du service (placement, variété) ; Retour : qualité du retour, son pendant côté relanceur.
/// Puissance : vitesse du service, points gratuits ; Explosivité : réaction du relanceur, son pendant.
/// Taille : attribut fixe, aide le serveur et gêne le relanceur (CLAUDE.md §5.1). Agressivité : prise de risque au service (§6.3).
/// Régularité : réussir plusieurs fois le même geste (§4.3) ; au service, plus de premières et surtout moins de doubles fautes.
/// Échange : tout le reste du point (non découpé en V0.3).
/// </summary>
public sealed record Joueur(string Nom, double Service, double Retour, double Echange,
                            double Puissance = 75, double Explosivite = 75, double TailleCm = 188, double Agressivite = 50, double Regularite = 75)
{
    /// <summary>
    /// Joueur dont Service, Retour et Échange valent le talent du simulateur de circuit. Puissance, Explosivité, Taille et Agressivité
    /// sont des stats de style, indépendantes du niveau : elles restent à leur valeur de référence (un n°1 ne fait pas forcément d'aces).
    /// </summary>
    public Joueur(string nom, double talent) : this(nom, talent, talent, talent) { }

    /// <summary>Niveau global provisoire (CLAUDE.md §4.5) : moyenne de Service, Retour et Échange, à poids égaux.</summary>
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
