using System.Text.Json;

namespace Chelems.Core.Fidelite;

/// <summary>Un match à simuler : un joueur du top 150 d'une saison simulée contre un adversaire de rang réaliste.</summary>
public readonly record struct Rencontre(int Annee, int Rang, int RangAdversaire, int BestOf, string Niveau = "A");

/// <summary>
/// Hypothèses de la population de test (config/population.json), tirées des mesures de docs/mesure_rivalites.md :
/// Constance ~ N(moyenne + pente × (talent − référence), écart-type), bornée 1-99.
/// Spécialisation (roadmap, étape 1, 2026-11-25) : Service et Retour suivent une part β du talent et s'en écartent de dS, dR ~ N(0, écart-type), de corrélation
/// donnée, et l'Échange de −(dS + dR) / 2. Calée pour reproduire, dans le top 81, la dispersion des points gagnés au service et au retour
/// et leur corrélation (ATP, 52 semaines : écarts-types 2,95 et 2,41, corrélation −0,28). Commande : caler-specialisation.
/// </summary>
public sealed record ConfigPopulation(double ConstanceMoyenne, double ConstancePenteTalent, double TalentReference, double ConstanceEcartType,
                                      double SpecialisationEcartType = 0, double SpecialisationCorrelation = 0, double SpecialisationPartTalent = 1,
                                      double TaillePuissance = 0.43, double TailleExplosivite = 0.43, double TailleMoyenne = 188.7, double TailleEcartType = 7.4)
{
    /// <summary>Profil d'un joueur de la population : écarts tirés d'une graine propre au joueur (même profil d'une simulation à l'autre).</summary>
    public Joueur Profil(string nom, double talent, int annee, int rang) =>
        Profil(nom, talent, annee, rang, SpecialisationEcartType, SpecialisationCorrelation, SpecialisationPartTalent, TalentReference, this);

    /// <summary>Écart-type des stats de style (Puissance, Explosivité), le même que l'échelle des stats de niveau.</summary>
    public const double EcartTypeStyle = 6.4;

    /// <param name="partTalent">β : part du talent que suivent Service et Retour (S = référence + β × (talent − référence) + dS). L'effet d'un point
    /// de Service est calé sur les joueurs réels ; β garde l'effet du talent sur le service calé par la simulation de masse.</param>
    /// <param name="physique">
    /// [Créateur, 2026-11-25] Taille tirée dans la répartition des joueurs du MCP ; la Puissance la suit (plus grand, plus puissant),
    /// l'Explosivité la suit en sens inverse (plus grand, moins explosif) : P = 75 + a × (taille − 188) + bruit, E = 75 − b × (taille − 188) + bruit,
    /// bruits réglés pour un écart-type total de <see cref="EcartTypeStyle"/>. Service et Retour ne dépendent pas de la taille.
    /// Absent : taille, Puissance et Explosivité de référence.
    /// </param>
    public static Joueur Profil(string nom, double talent, int annee, int rang, double ecartType, double correlation, double partTalent = 1,
                                double reference = 72, ConfigPopulation? physique = null)
    {
        if (ecartType == 0 && partTalent == 1 && physique is null) return new Joueur(nom, talent);
        var rng = new Random(Graines.Deriver(7919, annee * 1000 + rang));
        double z1 = Aleatoire.Gauss(rng), z2 = Aleatoire.Gauss(rng);
        double dS = ecartType * z1, dR = ecartType * (correlation * z1 + Math.Sqrt(1 - correlation * correlation) * z2);
        static double B(double v) => Math.Clamp(v, 1, 99);
        double niveau = reference + partTalent * (talent - reference);
        var j = new Joueur(nom, B(niveau + dS), B(niveau + dR), B(talent - (dS + dR) / 2));
        if (physique is null) return j;
        double taille = Math.Clamp(physique.TailleMoyenne + physique.TailleEcartType * Aleatoire.Gauss(rng), 165, 215);
        double Bruit(double a) => Math.Sqrt(Math.Max(0, EcartTypeStyle * EcartTypeStyle - Math.Pow(a * physique.TailleEcartType, 2))) * Aleatoire.Gauss(rng);
        return j with
        {
            TailleCm = Math.Round(taille),
            Puissance = B(75 + physique.TaillePuissance * (taille - 188) + Bruit(physique.TaillePuissance)),
            Explosivite = B(75 - physique.TailleExplosivite * (taille - 188) + Bruit(physique.TailleExplosivite)),
        };
    }

    public static ConfigPopulation Charger(string chemin) =>
        JsonSerializer.Deserialize<ConfigPopulation>(File.ReadAllText(chemin), new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower })
        ?? throw new InvalidDataException($"Configuration vide : {chemin}");
}

/// <summary>
/// Population de test (règles d'usage de objectifs_equilibrage/README.md) :
/// - joueurs et talents des 17 saisons du simulateur de circuit (<c>donnees/sim_classements.csv</c>) : classement aux points, bruité (règle 3) ;
/// - adversaires tirés dans la répartition ATP réelle de la surface, avec le format (<c>adversaires_*.csv</c>, règle 2) ;
/// - Service, Retour et Échange : talent du simulateur plus un profil de spécialisation (ConfigPopulation) ; les stats de style
///   restent à leur référence. Ce sont les mêmes joueurs sur toutes les surfaces.
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

    public static PopulationAtp Construire(string cheminClassements, string cheminAdversaires, int tiragesParJoueur, int graine, ConfigPopulation? population = null)
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

        var joueurs = new Dictionary<(int, int), Joueur>();
        foreach (var (an, table) in talents)
            foreach (var (rang, talent) in table)
                joueurs[(an, rang)] = population?.Profil($"{an}#{rang}", talent, an, rang) ?? new Joueur($"{an}#{rang}", talent);

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
