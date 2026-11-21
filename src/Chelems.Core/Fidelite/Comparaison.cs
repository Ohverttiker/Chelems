using System.Globalization;
using System.Text;

namespace Chelems.Core.Fidelite;

public enum Statut { Ok, Limite, Ecart }

public readonly record struct LigneComparaison(CleMesure Cle, double Atp, double Simule, double Tolerance, Statut Statut)
{
    /// <summary>Écart simulé − ATP, en points de %.</summary>
    public double Ecart => Simule - Atp;
}

/// <summary>
/// Compare une simulation aux cibles ATP.
/// Tolérances [Proposé, à valider par le créateur] : max(plancher de la famille, 2 erreurs-types binomiales de la cible ATP),
/// en points de %. « Limite » entre 1 et 1,5 tolérance, « Écart » au-delà.
/// </summary>
public static class Comparaison
{
    /// <summary>Familles ajustées par le calibrage (comme le prototype Python). Les autres sont des contrôles indépendants.</summary>
    public static readonly string[] FamillesAjustees = ["hold", "pts", "vict"];

    private static readonly Dictionary<string, double> Planchers = new()
    {
        ["hold"] = 1.0, ["pts"] = 0.5, ["gagne"] = 2.0, ["retour"] = 1.0, ["pts_retour"] = 0.5, ["moyen"] = 1.0, ["vict"] = 2.0,
        ["hold_mieux"] = 1.5, ["hold_moins"] = 1.5, ["score"] = 2.0,
        ["deux_sets"] = 2.5, ["meme12"] = 2.5, ["disp_moy"] = 1.0, ["disp_et"] = 1.0, ["mcp"] = 1.0, ["efficacite"] = 1.0,
    };

    private static readonly string[] Ordre =
        ["moyen", "efficacite", "mcp", "hold", "pts", "retour", "pts_retour", "gagne", "vict", "hold_mieux", "hold_moins", "score", "deux_sets", "meme12", "disp_moy", "disp_et"];

    public static List<LigneComparaison> Comparer(CiblesAtp cibles, Mesures mesures)
    {
        var lignes = new List<LigneComparaison>();
        foreach (var (cle, cible) in cibles.Valeurs)
        {
            if (!mesures.Valeurs.TryGetValue(cle, out double sim)) continue;
            bool enPoints = cle.Famille.StartsWith("disp", StringComparison.Ordinal);
            double atp = enPoints ? cible.Valeur : 100 * cible.Valeur;
            double s = enPoints ? sim : 100 * sim;
            double tol = Planchers.GetValueOrDefault(cle.Famille, 2.0);
            if (!enPoints && !double.IsNaN(cible.Effectif) && cible.Effectif > 0)
                tol = Math.Max(tol, 200 * Math.Sqrt(cible.Valeur * (1 - cible.Valeur) / cible.Effectif));
            double ecart = Math.Abs(s - atp);
            var statut = ecart <= tol ? Statut.Ok : ecart <= 1.5 * tol ? Statut.Limite : Statut.Ecart;
            lignes.Add(new LigneComparaison(cle, atp, s, tol, statut));
        }
        return lignes
            .OrderBy(l => Array.IndexOf(Ordre, l.Cle.Famille))
            .ThenBy(l => l.Cle.BestOf)
            .ThenBy(l => OrdreCategorie(l.Cle.Categorie))
            .ToList();
    }

    /// <summary>Chi2 binomial pondéré par les effectifs ATP (même définition que ajuster_point.py).</summary>
    public static double Chi2(CiblesAtp cibles, Mesures mesures, IEnumerable<string> familles, Func<CleMesure, bool>? exclure = null)
    {
        var f = familles.ToHashSet();
        double chi2 = 0;
        foreach (var (cle, cible) in cibles.Valeurs)
        {
            if (!f.Contains(cle.Famille) || double.IsNaN(cible.Effectif) || exclure?.Invoke(cle) == true) continue;
            if (!mesures.Valeurs.TryGetValue(cle, out double sim)) continue;
            double p = Math.Clamp(sim, 1e-6, 1 - 1e-6);
            chi2 += cible.Effectif * (cible.Valeur - p) * (cible.Valeur - p) / (p * (1 - p));
        }
        return chi2;
    }

    public static string Tableau(IEnumerable<LigneComparaison> lignes)
    {
        var sb = new StringBuilder();
        var ci = CultureInfo.InvariantCulture;
        sb.AppendLine($"{"mesure",-42}{"ATP",8}{"sim",8}{"écart",8}{"tol.",7}  statut");
        string? famille = null;
        foreach (var l in lignes)
        {
            if (famille is not null && l.Cle.Famille != famille) sb.AppendLine();
            famille = l.Cle.Famille;
            string statut = l.Statut switch { Statut.Ok => "ok", Statut.Limite => "limite", _ => "ÉCART" };
            double ecart = Math.Round(l.Ecart, 1);
            if (ecart == 0) ecart = 0; // pas de « -0.0 »
            sb.AppendLine(string.Format(ci, "{0,-42}{1,8:F1}{2,8:F1}{3,8:+0.0;-0.0;0.0}{4,7:F1}  {5}",
                                        l.Cle.ToString(), l.Atp, l.Simule, ecart, l.Tolerance, statut));
        }
        return sb.ToString();
    }

    public static string EnCsv(IEnumerable<LigneComparaison> lignes)
    {
        var ci = CultureInfo.InvariantCulture;
        var sb = new StringBuilder("famille,best_of,categorie,atp,simule,ecart,tolerance,statut\n");
        foreach (var l in lignes)
            sb.AppendLine(string.Format(ci, "{0},{1},{2},{3:F2},{4:F2},{5:F2},{6:F2},{7}",
                                        l.Cle.Famille, l.Cle.BestOf, l.Cle.Categorie, l.Atp, l.Simule, l.Ecart, l.Tolerance, l.Statut));
        return sb.ToString();
    }

    private static int OrdreCategorie(string c)
    {
        // Trie « 0-5 », « 6-10 », « 161+ », « matchs_en_2_sets »… par le premier nombre rencontré.
        int i = 0;
        while (i < c.Length && !char.IsDigit(c[i])) i++;
        int j = i;
        while (j < c.Length && char.IsDigit(c[j])) j++;
        return j > i ? int.Parse(c.AsSpan(i, j - i), CultureInfo.InvariantCulture) : int.MaxValue;
    }
}
