namespace Chelems.Core;

/// <summary>
/// Règles du match. Ce ne sont pas des constantes de calibrage : ce sont les règles du tennis.
/// V0.1 : tie-break en 7 points à 6-6 dans tous les sets, y compris le dernier
/// (simplification : l'Open d'Australie et l'US Open ont changé de règle pour le set décisif au cours de 2015-2025).
/// </summary>
public sealed record FormatMatch(int SetsGagnants, int PointsTieBreak = 7, int PointsTieBreakDernierSet = 7)
{
    public static readonly FormatMatch Bo3 = new(2);
    public static readonly FormatMatch Bo5 = new(3);

    public int BestOf => 2 * SetsGagnants - 1;

    public static FormatMatch DepuisBestOf(int bestOf) => bestOf switch
    {
        3 => Bo3,
        5 => Bo5,
        _ => throw new ArgumentOutOfRangeException(nameof(bestOf), bestOf, "Format attendu : 3 ou 5 sets"),
    };
}
