using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace Chelems.Core.Configuration;

/// <summary>Point au service : pente en talent (commune à toutes les surfaces) et bornes de sécurité.</summary>
public sealed record ConfigPointService(double PenteParPointDeTalent, double BorneMin, double BorneMax);

/// <summary>
/// Physique d'une surface (principe 3 : la surface agit, pas les stats).
/// Centre = probabilité de gagner un point au service entre deux joueurs égaux.
/// Affinite = direction (2D, en probabilité) dans l'espace des styles : affinité d'un joueur = Affinite · style.
/// Contre = écart-type du contre pur entre deux styles (produit vectoriel), en probabilité.
/// </summary>
public sealed record ConfigSurface(double Centre, double[] Affinite, double Contre);

/// <summary>
/// Forme (principe 6, mesurée : docs/mesure_rivalites.md). Écarts-types en probabilité de gagner un point au service,
/// pour un joueur de Constance 50. La forme d'un joueur augmente son % au service et baisse celui de l'adversaire.
/// EcartTypeMatch : forme fixe sur un match, dont PartLente (variance) est une phase qui dure d'un match à l'autre
/// (persistance PersistanceLente par match) ; le reste est tiré à chaque match. EcartTypeSet : variation d'un set à l'autre.
/// </summary>
public sealed record ConfigForme(double EcartTypeMatch, double PartLente, double PersistanceLente, double EcartTypeSet);

/// <summary>
/// Enjeu du tournoi (mesuré : docs/mesure_rivalites.md, étape 0 quinquies). Enjeu e ∈ [0 ; 1] par niveau de tournoi
/// (codes Sackmann : G, M, A, F, O). Variance de l'aléa du jour × (1 − Reduction × e × (Constance − 1) / 98) :
/// plus la Constance est haute, plus le joueur « ne passe pas à côté » d'un grand rendez-vous. La phase lente n'est pas touchée.
/// </summary>
public sealed record ConfigEnjeu(double Reduction, Dictionary<string, double> Niveaux)
{
    public double Valeur(string? niveau) => niveau is not null && Niveaux.TryGetValue(niveau, out double e) ? e : 0;
}

/// <summary>Constance : multiplicateur de la variance de forme = 1 + Amplitude × (50 − Constance) / 49.</summary>
public sealed record ConfigConstance(double Amplitude);

/// <summary>Pression : décalage de la probabilité du serveur selon l'enjeu du point (moyenne du circuit).</summary>
public sealed record ConfigPression(double BalleSetContre, double BalleSetPour, double TieBreak, double BalleDeBreak,
                                    double BalleDeJeu, double Egalite, double Autres);

/// <summary>
/// Élan. Point : ± sur le point suivant du même jeu de service. Jeu : ± sur tout le jeu de service selon le précédent
/// (tenu ou perdu) dans le set. Set : ± sur le niveau des deux joueurs pendant le set qui suit un set gagné ou perdu.
/// </summary>
public sealed record ConfigElan(double Point, double Jeu, double Set);

/// <summary>
/// Toutes les constantes de calibrage du moteur (CLAUDE.md §9 : jamais dans le code). Chargées depuis <c>config/moteur.json</c>.
/// Clés de surface : « dur », « terre », « gazon ».
/// </summary>
public sealed record ConfigMoteur(
    string Version, ConfigPointService PointService, Dictionary<string, ConfigSurface> Surfaces,
    ConfigForme Forme, ConfigConstance Constance, ConfigPression Pression, ConfigElan Elan, ConfigEnjeu? Enjeu = null)
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public ConfigSurface Surface(Surface s) =>
        Surfaces.TryGetValue(SurfaceCle(s), out var c) ? c : throw new InvalidDataException($"Surface absente de la configuration : {SurfaceCle(s)}");

    public static string SurfaceCle(Surface s) => s switch { Core.Surface.Dur => "dur", Core.Surface.Terre => "terre", _ => "gazon" };

    public static ConfigMoteur Charger(string chemin)
    {
        var cfg = JsonSerializer.Deserialize<ConfigMoteur>(File.ReadAllText(chemin), Options)
                  ?? throw new InvalidDataException($"Configuration vide : {chemin}");
        cfg.Valider();
        return cfg;
    }

    /// <summary>Réécrit les valeurs en conservant les commentaires (clés commençant par « _ ») du fichier existant.</summary>
    public void Sauver(string chemin)
    {
        var noeud = JsonNode.Parse(JsonSerializer.Serialize(this, Options))!.AsObject();
        if (File.Exists(chemin) && JsonNode.Parse(File.ReadAllText(chemin)) is JsonObject ancien)
            Fusionner(ancien, noeud);
        File.WriteAllText(chemin, noeud.ToJsonString(Options) + Environment.NewLine);
    }

    private static void Fusionner(JsonObject ancien, JsonObject nouveau)
    {
        var resultat = new List<KeyValuePair<string, JsonNode?>>();
        foreach (var (cle, valeur) in ancien)
            if (cle.StartsWith('_')) resultat.Add(new(cle, valeur?.DeepClone()));
        foreach (var (cle, valeur) in nouveau.ToList())
        {
            if (valeur is JsonObject sous && ancien[cle] is JsonObject sousAncien) Fusionner(sousAncien, sous);
            resultat.Add(new(cle, valeur?.DeepClone()));
        }
        nouveau.Clear();
        foreach (var (cle, valeur) in resultat) nouveau[cle] = valeur;
    }

    public void Valider()
    {
        var p = PointService ?? throw new InvalidDataException("point_service manquant");
        if (p.BorneMin <= 0 || p.BorneMax >= 1 || p.BorneMin >= p.BorneMax) throw new InvalidDataException("point_service : bornes hors de ]0 ; 1[");
        if (p.PenteParPointDeTalent < 0) throw new InvalidDataException("point_service : pente négative");
        if (Surfaces is null || Surfaces.Count == 0) throw new InvalidDataException("surfaces manquantes");
        foreach (var (nom, s) in Surfaces)
        {
            if (s.Centre <= p.BorneMin || s.Centre >= p.BorneMax) throw new InvalidDataException($"surface {nom} : centre hors des bornes");
            if (s.Affinite is not { Length: 2 }) throw new InvalidDataException($"surface {nom} : affinite doit avoir 2 composantes");
            if (s.Contre < 0) throw new InvalidDataException($"surface {nom} : contre négatif");
        }
        if (Forme is null || Forme.EcartTypeMatch < 0 || Forme.EcartTypeSet < 0 || Forme.PartLente is < 0 or > 1 || Forme.PersistanceLente is < 0 or >= 1)
            throw new InvalidDataException("forme : valeurs invalides");
        if (Constance is null || Constance.Amplitude is < 0 or >= 1) throw new InvalidDataException("constance : amplitude dans [0 ; 1[");
        if (Pression is null || Elan is null) throw new InvalidDataException("pression ou elan manquant");
        if (Enjeu is not null && (Enjeu.Reduction is < 0 or > 1 || Enjeu.Niveaux.Values.Any(v => v is < 0 or > 1)))
            throw new InvalidDataException("enjeu : reduction et niveaux dans [0 ; 1]");
    }
}
