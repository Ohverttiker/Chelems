using System.Globalization;
using System.Text;

namespace Chelems.Core.Fidelite;

/// <summary>Lecteur CSV minimal (séparateur virgule, champs entre guillemets possibles).</summary>
internal static class Csv
{
    public static IEnumerable<Dictionary<string, string>> Lire(string chemin)
    {
        using var lecteur = new StreamReader(chemin, Encoding.UTF8);
        var entete = Decouper(lecteur.ReadLine() ?? throw new InvalidDataException($"CSV vide : {chemin}"));
        string? ligne;
        while ((ligne = lecteur.ReadLine()) is not null)
        {
            if (ligne.Length == 0) continue;
            var champs = Decouper(ligne);
            var d = new Dictionary<string, string>(entete.Count);
            for (int i = 0; i < entete.Count && i < champs.Count; i++) d[entete[i]] = champs[i];
            yield return d;
        }
    }

    public static double Reel(this Dictionary<string, string> l, string cle) =>
        double.Parse(l[cle], CultureInfo.InvariantCulture);

    public static int Entier(this Dictionary<string, string> l, string cle) =>
        int.Parse(l[cle], CultureInfo.InvariantCulture);

    private static List<string> Decouper(string ligne)
    {
        var champs = new List<string>();
        var sb = new StringBuilder();
        bool guillemets = false;
        for (int i = 0; i < ligne.Length; i++)
        {
            char c = ligne[i];
            if (guillemets)
            {
                if (c == '"' && i + 1 < ligne.Length && ligne[i + 1] == '"') { sb.Append('"'); i++; }
                else if (c == '"') guillemets = false;
                else sb.Append(c);
            }
            else if (c == '"') guillemets = true;
            else if (c == ',') { champs.Add(sb.ToString()); sb.Clear(); }
            else sb.Append(c);
        }
        champs.Add(sb.ToString().TrimEnd('\r'));
        return champs;
    }
}
