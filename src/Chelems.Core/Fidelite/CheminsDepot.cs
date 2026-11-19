using Chelems.Core.Configuration;

namespace Chelems.Core.Fidelite;

/// <summary>Emplacements des fichiers de référence dans le dépôt (trouvé en remontant jusqu'à Chelems.slnx).</summary>
public sealed record CheminsDepot(string Racine)
{
    public string Config => Path.Combine(Racine, "config", "moteur.json");
    public string ConfigPopulation => Path.Combine(Racine, "config", "population.json");
    public string Classements => Path.Combine(Racine, "donnees", "sim_classements.csv");
    public string Resultats => Path.Combine(Racine, "resultats");

    /// <summary>Cibles ATP figées : le dur à la racine de objectifs_equilibrage/ (historique), terre et gazon dans leurs sous-dossiers.</summary>
    public string Objectifs(Surface s) => s == Surface.Dur
        ? Path.Combine(Racine, "objectifs_equilibrage")
        : Path.Combine(Racine, "objectifs_equilibrage", ConfigMoteur.SurfaceCle(s));

    public string Adversaires(Surface s) => Path.Combine(Objectifs(s), $"adversaires_{ConfigMoteur.SurfaceCle(s)}.csv");

    public static CheminsDepot Trouver(string? depart = null)
    {
        foreach (var debut in new[] { depart ?? AppContext.BaseDirectory, Directory.GetCurrentDirectory() })
        {
            var dir = new DirectoryInfo(debut);
            while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Chelems.slnx"))) dir = dir.Parent;
            if (dir is not null) return new CheminsDepot(dir.FullName);
        }
        throw new DirectoryNotFoundException("Racine du dépôt (Chelems.slnx) introuvable");
    }
}
