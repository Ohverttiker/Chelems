using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace Chelems.Core.Configuration;

/// <summary>
/// Issues du duel au service, en proportion. Pour la première balle : sur les premières rentrées (DoubleFaute = 0).
/// Pour la seconde : sur les secondes jouées, doubles fautes comprises. Ordre de <see cref="EnTableau"/> = <see cref="IssueService"/>.
/// </summary>
public sealed record IssuesService(double Ace, double Gagnant, double Attaque, double Neutre, double Defense,
                                   double RetourGagnant, double DoubleFaute = 0)
{
    public double[] EnTableau() => [Ace, Gagnant, Attaque, Neutre, Defense, RetourGagnant, DoubleFaute];

    public static IssuesService DepuisTableau(double[] v) => new(v[0], v[1], v[2], v[3], v[4], v[5], v[6]);
}

/// <summary>
/// Issues d'un coup de l'échange pour le joueur qui frappe, en proportion. Ordre de <see cref="EnTableau"/> = <see cref="IssueEchange"/>.
/// AdvDefense : l'adversaire jouera son coup en défense ; AdvAttaque : il le jouera en attaque.
/// </summary>
public sealed record IssuesEchange(double Gagnant, double AdvDefense, double Neutre, double AdvAttaque, double Faute)
{
    public double[] EnTableau() => [Gagnant, AdvDefense, Neutre, AdvAttaque, Faute];

    public static IssuesEchange DepuisTableau(double[] v) => new(v[0], v[1], v[2], v[3], v[4]);
}

/// <summary>
/// Duel au service. PremiereRentree : part des premières balles rentrées (constante en V0.3).
/// Premiere, Seconde : issues entre deux joueurs égaux. Ce sont les lois de référence du créateur (moyenne du tour 2023) ;
/// chaque surface peut avoir les siennes (<see cref="ConfigSurface"/>), calées sur les points au service de l'ATP. Orientation : valeur de chaque issue pour le serveur (−1 à +1),
/// qui oriente le décalage dû à l'écart Service − Retour. Pente : décalage (en logit, multiplié par l'orientation) par point d'écart.
/// Stats : effet de chaque stat sur le duel (roadmap, étape 1). Absent : seul l'écart Service − Retour joue, avec Orientation.
/// </summary>
public sealed record ConfigService(double PremiereRentree, IssuesService Premiere, IssuesService Seconde,
                                   IssuesService Orientation, double Pente, Dictionary<string, EffetStat>? Stats = null)
{
    /// <summary>Clés de <see cref="Stats"/>, et le joueur qui porte la stat (serveur ou relanceur).</summary>
    public static readonly string[] ClesStats = ["service", "puissance", "retour", "explosivite", "echange_relanceur", "taille", "taille_relanceur", "agressivite", "regularite"];
}

/// <summary>
/// Effet d'une stat sur le duel au service : décalage en logit de chaque issue,
/// x_k = coefficient × (valeur − Reference) × Premiere_k (ou Seconde_k), et de la part de premières rentrées, coefficient × (valeur − Reference) × Rentree.
/// Coefficient = Part × pente du service si Part est donné (stats de niveau, recalées avec la pente), sinon 1 (effet par unité, mesuré),
/// multiplié par Echelle : réglée pour que les joueurs réels se répartissent selon l'échelle des stats (écart-type 6,4 autour de 72,8).
/// Les stats du relanceur (Retour, Explosivité) jouent avec le signe opposé : à valeurs égales, elles annulent leur pendant
/// (Service, Puissance) quand leurs orientations sont égales. La Taille du serveur (« taille ») et celle du relanceur (« taille_relanceur »)
/// ont chacune leur effet, mesuré séparément :
/// un grand serveur a un meilleur service, un grand relanceur un moins bon retour (CLAUDE.md §5.1).
/// </summary>
public sealed record EffetStat(double Reference, IssuesService Premiere, IssuesService Seconde, double Rentree = 0, double? Part = null, double Echelle = 1);

/// <summary>
/// Échange. Attaque, Neutre, Defense : issues d'un coup selon l'état du joueur qui frappe, entre deux joueurs égaux.
/// Orientation : valeur de chaque issue pour le frappeur (−1 à +1). Pente : décalage en logit par point d'écart d'Échange.
/// </summary>
public sealed record ConfigEchange(IssuesEchange Attaque, IssuesEchange Neutre, IssuesEchange Defense,
                                   IssuesEchange Orientation, double Pente, ProfilEchange? Profil = null)
{
    public IssuesEchange Etat(EtatEchange e) => e switch
    {
        EtatEchange.Attaque => Attaque, EtatEchange.Neutre => Neutre, _ => Defense,
    };
}

/// <summary>
/// Profil de référence de l'échange [Proposé] : vers lui le calage rappelle les lois (les données ne disent pas dans quel état
/// surviennent fautes et coups gagnants). Il ne sert pas pendant un match.
/// </summary>
public sealed record ProfilEchange(IssuesEchange Attaque, IssuesEchange Neutre, IssuesEchange Defense);

/// <summary>
/// Physique d'une surface (principe 3). Vitesse : décalage de l'échange (en logit, multiplié par l'orientation) en faveur
/// de l'attaquant en attaque et contre le défenseur en défense. Positive = surface rapide.
/// PremiereRentree, Premiere, Seconde : lois du duel au service propres à la surface (à défaut, celles de <see cref="ConfigService"/>).
/// Elles partent des lois du créateur et sont ajustées pour retrouver les points au service de l'ATP sur la surface.
/// </summary>
/// FautesRetour [Mesuré, MCP] : part des services gagnants qui sont des retours ratés sur une balle retournable ; comptage seulement
/// (fautes directes du relanceur au lieu de fautes provoquées), sans effet sur l'issue du point.
public sealed record ConfigSurface(double Vitesse, double? PremiereRentree = null, IssuesService? Premiere = null, IssuesService? Seconde = null,
                                   PartsFauteRetour? FautesRetour = null);

/// <summary>Part des services gagnants comptés comme fautes directes de retour, après une première et après une seconde.</summary>
public sealed record PartsFauteRetour(double Premiere, double Seconde);

/// <summary>
/// Toutes les constantes de calibrage du moteur (CLAUDE.md §9 : jamais dans le code). Chargées depuis <c>config/moteur.json</c>.
/// Clés de surface : « dur », « terre », « gazon ».
/// </summary>
public sealed record ConfigMoteur(string Version, ConfigService Service, ConfigEchange Echange, Dictionary<string, ConfigSurface> Surfaces)
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

    /// <summary>Lois du duel au service sur la surface : celles de la surface si elle en a, sinon celles de référence.</summary>
    public (double PremiereRentree, IssuesService Premiere, IssuesService Seconde) LoisService(Surface s)
    {
        var c = Surface(s);
        return (c.PremiereRentree ?? Service.PremiereRentree, c.Premiere ?? Service.Premiere, c.Seconde ?? Service.Seconde);
    }

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
        var s = Service ?? throw new InvalidDataException("service manquant");
        if (s.PremiereRentree is <= 0 or >= 1) throw new InvalidDataException("service : premiere_rentree dans ]0 ; 1[");
        VerifierLoi("service.premiere", s.Premiere?.EnTableau());
        VerifierLoi("service.seconde", s.Seconde?.EnTableau());
        if (s.Premiere!.DoubleFaute != 0) throw new InvalidDataException("service.premiere : pas de double faute sur une première rentrée");
        if (s.Orientation is null || s.Pente < 0) throw new InvalidDataException("service : orientation manquante ou pente négative");
        foreach (var (cle, effet) in s.Stats ?? [])
        {
            if (!ConfigService.ClesStats.Contains(cle)) throw new InvalidDataException($"service.stats : stat inconnue « {cle} »");
            if (effet.Premiere is null || effet.Seconde is null) throw new InvalidDataException($"service.stats.{cle} : premiere et seconde requises");
        }
        var e = Echange ?? throw new InvalidDataException("echange manquant");
        VerifierLoi("echange.attaque", e.Attaque?.EnTableau());
        VerifierLoi("echange.neutre", e.Neutre?.EnTableau());
        VerifierLoi("echange.defense", e.Defense?.EnTableau());
        if (e.Orientation is null || e.Pente < 0) throw new InvalidDataException("echange : orientation manquante ou pente négative");
        foreach (var x in new[] { e.Attaque!, e.Neutre!, e.Defense! })
            if (x.Gagnant + x.Faute <= 0) throw new InvalidDataException("echange : chaque état doit pouvoir finir le point");
        if (Surfaces is null || Surfaces.Count == 0) throw new InvalidDataException("surfaces manquantes");
        foreach (var (cle, c) in Surfaces)
        {
            if (c.PremiereRentree is { } pr && pr is <= 0 or >= 1) throw new InvalidDataException($"surfaces.{cle} : premiere_rentree dans ]0 ; 1[");
            if (c.Premiere is { } p1)
            {
                VerifierLoi($"surfaces.{cle}.premiere", p1.EnTableau());
                if (p1.DoubleFaute != 0) throw new InvalidDataException($"surfaces.{cle}.premiere : pas de double faute sur une première rentrée");
            }
            if (c.Seconde is { } p2) VerifierLoi($"surfaces.{cle}.seconde", p2.EnTableau());
        }
    }

    private static void VerifierLoi(string nom, double[]? p)
    {
        if (p is null) throw new InvalidDataException($"{nom} manquant");
        if (p.Any(x => x < 0 || double.IsNaN(x))) throw new InvalidDataException($"{nom} : probabilité négative");
        if (Math.Abs(p.Sum() - 1) > 1e-4) throw new InvalidDataException($"{nom} : la somme vaut {p.Sum():F4} au lieu de 1");
    }
}
