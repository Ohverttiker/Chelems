namespace Chelems.Core;

/// <summary>Surfaces modélisées. Indoor et court pace index par tournoi : pas encore (pas de données).</summary>
public enum Surface { Dur, Terre, Gazon }

/// <summary>
/// Style de jeu caché d'un joueur (V0.x, en attendant les statistiques détaillées) : un point du plan, fixe.
/// Il sert deux fois, sans aucune règle par joueur (principe 1) :
/// - affinité de surface = projection du style sur la direction propre à la surface (principe 3) ;
/// - contre entre deux styles = produit vectoriel (structure de roue, aucun style ne domine en moyenne).
/// </summary>
public readonly record struct Style(double X, double Y)
{
    public static readonly Style Neutre = new(0, 0);

    /// <summary>Tire un style au hasard : X et Y suivent une loi normale centrée réduite.</summary>
    public static Style Tirer(Random rng) => new(Aleatoire.Gauss(rng), Aleatoire.Gauss(rng));

    /// <summary>Avantage du style a sur le style b. Antisymétrique ; variance 2 pour deux styles au hasard.</summary>
    public static double Opposition(Style a, Style b) => a.X * b.Y - a.Y * b.X;

    /// <summary>Projection sur une direction (affinité de surface). Variance |direction|² pour un style au hasard.</summary>
    public double Projection(double[] direction) => X * direction[0] + Y * direction[1];
}

/// <summary>
/// Joueur de la V0.2 : un talent (échelle du simulateur de circuit, 50-99), un style caché et la stat Constance (1-99).
/// Constance haute = forme qui varie peu d'un match à l'autre.
/// </summary>
public sealed record Joueur(string Nom, double Talent, Style Style, double Constance = 50)
{
    public Joueur(string nom, double talent) : this(nom, talent, Style.Neutre) { }
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
