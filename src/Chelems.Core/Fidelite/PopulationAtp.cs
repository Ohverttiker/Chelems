using System.Text.Json;

namespace Chelems.Core.Fidelite;

/// <summary>Un match à simuler : un joueur du top 150 d'une saison simulée contre un adversaire de rang réaliste.</summary>
public readonly record struct Rencontre(int Annee, int Rang, int RangAdversaire, int BestOf, string Niveau = "A");

/// <summary>
/// Hypothèses de la population de test (config/population.json), tirées des mesures de docs/mesure_rivalites.md :
/// Constance ~ N(moyenne + pente × (talent − référence), écart-type), bornée 1-99.
/// </summary>
public sealed record ConfigPopulation(double ConstanceMoyenne, double ConstancePenteTalent, double TalentReference, double ConstanceEcartType)
{
    public static ConfigPopulation Charger(string chemin) =>
        JsonSerializer.Deserialize<ConfigPopulation>(File.ReadAllText(chemin), new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower })
        ?? throw new InvalidDataException($"Configuration vide : {chemin}");
}

/// <summary>
/// Population de test (règles d'usage de objectifs_equilibrage/README.md) :
/// - joueurs et talents des 17 saisons du simulateur de circuit (<c>donnees/sim_classements.csv</c>) : classement aux points, bruité (règle 3) ;
/// - adversaires tirés dans la répartition ATP réelle de la surface, avec le format (<c>adversaires_*.csv</c>, règle 2) ;
/// - chaque joueur a un style caché et une Constance, tirés une fois pour toutes : ce sont les mêmes joueurs sur toutes les surfaces.
/// </summary>
public sealed class PopulationAtp
{
    private readonly Dictionary<(int Annee, int Rang), Joueur> _joueurs;

    public IReadOnlyList<Rencontre> Rencontres { get; }

    private PopulationAtp(Dictionary<(int, int), Joueur> joueurs, List<Rencontre> rencontres)
    {
        _joueurs = joueurs;
        Rencontres = rencontres;
    }

    public Joueur Joueur(int annee, int rang) => _joueurs[(annee, rang)];
    public IEnumerable<Joueur> Joueurs => _joueurs.Values;

    public static PopulationAtp Construire(string cheminClassements, string cheminAdversaires, ConfigPopulation cfg,
                                           int tiragesParJoueur, int graine)
    {
        var talents = new SortedDictionary<int, SortedDictionary<int, double>>();
        foreach (var l in Csv.Lire(cheminClassements))
        {
            int an = l.Entier("annee");
            if (!talents.TryGetValue(an, out var t)) talents[an] = t = new SortedDictionary<int, double>();
            t[l.Entier("rang")] = l.Reel("talent");
        }

        var pool = new List<(int RangAdversaire, int BestOf, string Niveau)>[Categories.CasesAdversaires.Length];
        for (int i = 0; i < pool.Length; i++) pool[i] = [];
        foreach (var l in Csv.Lire(cheminAdversaires))
        {
            int k = Categories.CaseAdversaire(l.Entier("rang_joueur"));
            if (k >= 0) pool[k].Add((l.Entier("rang_adversaire"), l.Entier("best_of"), l.GetValueOrDefault("niveau_tournoi") ?? "A"));
        }

        // Attributs cachés : un tirage par joueur, dans un ordre fixe (saison puis rang), indépendant de la surface.
        var rngStyles = new Random(Graines.Deriver(graine, 0));
        var joueurs = new Dictionary<(int, int), Joueur>();
        foreach (var (an, table) in talents)
            foreach (var (rang, talent) in table)
            {
                var style = Style.Tirer(rngStyles);
                double constance = Math.Clamp(cfg.ConstanceMoyenne + cfg.ConstancePenteTalent * (talent - cfg.TalentReference)
                                              + cfg.ConstanceEcartType * Aleatoire.Gauss(rngStyles), 1, 99);
                joueurs[(an, rang)] = new Joueur($"{an}#{rang}", talent, style, constance);
            }

        var rngTirage = new Random(Graines.Deriver(graine, 1));
        var rencontres = new List<Rencontre>();
        foreach (var (an, table) in talents)
        {
            int rangMax = table.Keys.Max();
            for (int r = 1; r <= 150; r++)
            {
                if (!table.ContainsKey(r)) continue;
                var adversaires = pool[Categories.CaseAdversaire(r)];
                if (adversaires.Count == 0) continue;
                for (int i = 0; i < tiragesParJoueur; i++)
                {
                    var (ro, bo, niveau) = adversaires[rngTirage.Next(adversaires.Count)];
                    ro = Math.Min(ro, rangMax); // non classé ou au-delà de la table : dernier rang simulé
                    if (ro == r || !table.ContainsKey(ro)) continue;
                    rencontres.Add(new Rencontre(an, r, ro, bo, niveau));
                }
            }
        }
        return new PopulationAtp(joueurs, rencontres);
    }
}

/// <summary>Dérivation de graines (SplitMix64) : un flux aléatoire indépendant et reproductible par indice.</summary>
public static class Graines
{
    public static int Deriver(int graine, long indice)
    {
        ulong z = unchecked((ulong)graine * 0x9E3779B97F4A7C15UL + (ulong)indice + 0x632BE59BD9B4E019UL);
        z = unchecked((z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL);
        z = unchecked((z ^ (z >> 27)) * 0x94D049BB133111EBUL);
        z ^= z >> 31;
        return (int)(z & 0x7FFFFFFF);
    }
}
