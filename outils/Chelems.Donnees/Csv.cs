using System.Text;

namespace Chelems.Donnees;

/// <summary>Lecture et écriture minimales de CSV (champs entre guillemets gérés).</summary>
internal static class Csv
{
    /// <summary>Lignes d'un CSV, en-tête sauté.</summary>
    public static IEnumerable<string[]> Lire(string chemin)
    {
        foreach (var ligne in File.ReadLines(chemin).Skip(1))
        {
            var champs = new List<string>();
            var cur = new StringBuilder();
            bool guillemets = false;
            foreach (char c in ligne)
            {
                if (c == '"') guillemets = !guillemets;
                else if (c == ',' && !guillemets) { champs.Add(cur.ToString()); cur.Clear(); }
                else cur.Append(c);
            }
            champs.Add(cur.ToString());
            yield return [.. champs];
        }
    }

    /// <summary>Champ prêt à écrire : entre guillemets s'il contient une virgule ou un guillemet.</summary>
    public static string Champ(string s) => s.Contains(',') || s.Contains('"') ? $"\"{s.Replace("\"", "\"\"")}\"" : s;
}
