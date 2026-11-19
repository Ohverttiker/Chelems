namespace Chelems.Core.Fidelite;

/// <summary>Identifiant d'une mesure : famille (hold, pts, vict…), format (3, 5, ou 0 pour tous formats) et catégorie.</summary>
public readonly record struct CleMesure(string Famille, int BestOf, string Categorie)
{
    public override string ToString() => BestOf == 0 ? $"{Famille} {Categorie}" : $"{Famille} Bo{BestOf} {Categorie}";
}

/// <summary>Découpages utilisés par les fichiers de <c>objectifs_equilibrage/</c>.</summary>
public static class Categories
{
    public static readonly (string Nom, int Min, int Max)[] TranchesRang =
    [
        ("1-1", 1, 1), ("2-5", 2, 5), ("6-10", 6, 10), ("11-20", 11, 20),
        ("21-50", 21, 50), ("51-100", 51, 100), ("101-150", 101, 150),
    ];

    public static readonly (string Nom, int Min, int Max)[] EcartsPlaces =
    [
        ("0-5", 0, 5), ("6-10", 6, 10), ("11-20", 11, 20), ("21-40", 21, 40),
        ("41-80", 41, 80), ("81-160", 81, 160), ("161+", 161, int.MaxValue),
    ];

    public static readonly (string Nom, int Min, int Max)[] EcartsDispersion =
    [
        ("0-5", 0, 5), ("6-20", 6, 20), ("21-80", 21, 80), ("81+", 81, int.MaxValue),
    ];

    /// <summary>Cases de rang où l'on tire les adversaires réels (même découpage que le prototype Python).</summary>
    public static readonly (int Min, int Max)[] CasesAdversaires =
    [
        (1, 1), (2, 3), (4, 5), (6, 10), (11, 15), (16, 20), (21, 30), (31, 40), (41, 50),
        (51, 65), (66, 80), (81, 100), (101, 125), (126, 150),
    ];

    public static string? Dans((string Nom, int Min, int Max)[] decoupage, int x)
    {
        foreach (var (nom, min, max) in decoupage)
            if (x >= min && x <= max) return nom;
        return null;
    }

    public static int CaseAdversaire(int rang)
    {
        for (int i = 0; i < CasesAdversaires.Length; i++)
            if (rang >= CasesAdversaires[i].Min && rang <= CasesAdversaires[i].Max) return i;
        return -1;
    }
}
