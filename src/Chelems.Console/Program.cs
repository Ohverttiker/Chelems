using System.Diagnostics;
using System.Globalization;
using Chelems.Core;
using Chelems.Core.Configuration;
using Chelems.Core.Fidelite;

CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
Console.OutputEncoding = System.Text.Encoding.UTF8;

var chemins = CheminsDepot.Trouver();
var options = Options.Lire(args.Skip(1).ToArray());
string configChemin = options.Texte("config", chemins.Config);
var config = ConfigMoteur.Charger(configChemin);

switch (args.FirstOrDefault())
{
    case "match": JouerMatch(); break;
    case "fidelite": Fidelite(); break;
    case "calibrer": Calibrer(); break;
    case "bareme": Bareme(); break;
    default:
        Console.WriteLine("""
            Chelem — moteur de match V0.2 (point par point : talent, style caché, surface, forme, Constance, pression, élan)

              match     --a 85 --b 78 [--bo 3|5] [--surface dur|terre|gazon] [--constance-a 50] [--constance-b 50] [--graine 1] [--detail]
              fidelite  [--surface dur|terre|gazon|toutes] [--tirages 40] [--graine 1] [--csv]
                        Simulation de masse comparée aux cibles ATP de objectifs_equilibrage/ (dur à la racine, terre/ et gazon/).
              calibrer  [--surface toutes] [--tirages 20] [--graine 1] [--tours 4] [--parametres centres,pente] [--ecrire]
                        Paramètres possibles : centres, pente, forme_match, forme_set, enjeu.

              Option commune : --config <chemin> (défaut config/moteur.json).
            """);
        break;
}

IReadOnlyList<Surface> SurfacesDemandees()
{
    string s = options.Texte("surface", "toutes");
    return s == "toutes" ? [Surface.Dur, Surface.Terre, Surface.Gazon] : [LireSurface(s)];
}

static Surface LireSurface(string s) => s switch
{
    "dur" => Surface.Dur, "terre" => Surface.Terre, "gazon" => Surface.Gazon,
    _ => throw new ArgumentException($"Surface inconnue : {s} (dur, terre ou gazon)"),
};

void JouerMatch()
{
    int graine = options.Entier("graine", 1);
    var rng = new Random(graine);
    var surface = LireSurface(options.Texte("surface", "dur"));
    var a = new Joueur("A", options.Reel("a", 80), Style.Tirer(rng), options.Reel("constance-a", 50));
    var b = new Joueur("B", options.Reel("b", 75), Style.Tirer(rng), options.Reel("constance-b", 50));
    var moteur = new MoteurMatch(new ModelePoint(config));
    Console.WriteLine($"p de base (sans forme) : A au service {moteur.Modele.ProbaBase(a, b, surface):P1}, B au service {moteur.Modele.ProbaBase(b, a, surface):P1}");
    Action<PointJoue>? obs = options.Contient("detail")
        ? p => Console.WriteLine($"  set {p.Set + 1} {p.JeuxA}-{p.JeuxB} {(p.TieBreak ? "TB " : "")}{p.PointsServeur}-{p.PointsRelanceur} " +
                                 $"serveur {(p.Serveur == 0 ? "A" : "B")} p={p.Probabilite:P1} {p.Type} → {(p.Gagnant == 0 ? "A" : "B")}")
        : null;
    var res = moteur.Jouer(a, b, FormatMatch.DepuisBestOf(options.Entier("bo", 3)), surface, rng, observateur: obs);
    Console.WriteLine($"Forme du jour : A {100 * res.FormeDuMatch[0]:+0.0;-0.0} pts, B {100 * res.FormeDuMatch[1]:+0.0;-0.0} pts");
    Console.WriteLine(res);
}

void Fidelite()
{
    int tirages = options.Entier("tirages", 40), graine = options.Entier("graine", 1);
    foreach (var s in SurfacesDemandees())
    {
        var chrono = Stopwatch.StartNew();
        var banc = Banc.Construire(chemins, s, tirages, graine);
        var mesures = banc.Mesurer(config, graine);
        var lignes = Comparaison.Comparer(banc.Cibles, mesures);
        Console.WriteLine($"######## {ConfigMoteur.SurfaceCle(s).ToUpperInvariant()} — {mesures.Matchs} matchs, {mesures.Points} points en {chrono.Elapsed.TotalSeconds:F1} s (graine {graine})\n");
        Console.WriteLine(Comparaison.Tableau(lignes));
        Console.WriteLine($"chi2 ajusté (hold, pts, vict) = {Comparaison.Chi2(banc.Cibles, mesures, Comparaison.FamillesAjustees):F0} ; " +
                          $"chi2 contrôles (sets, tie-breaks, dépendance) = {Comparaison.Chi2(banc.Cibles, mesures, ["score", "deux_sets", "meme12"]):F0}");
        Console.WriteLine($"Statuts : {lignes.Count(l => l.Statut == Statut.Ok)} ok, {lignes.Count(l => l.Statut == Statut.Limite)} limite, " +
                          $"{lignes.Count(l => l.Statut == Statut.Ecart)} écart\n");
        if (options.Contient("clutch"))
        {
            double V(string famille, string t) => 100 * mesures.Valeurs.GetValueOrDefault(new CleMesure(famille, 0, t));
            Console.WriteLine("Balles de break simulées (pts de %, service / retour) : " + string.Join("  ", Categories.TranchesRang.Select(t =>
                $"{t.Nom} {V("clutch_service", t.Nom):+0.0;-0.0} / {V("clutch_retour", t.Nom):+0.0;-0.0}")) + "\n");
        }
        if (options.Contient("csv"))
        {
            Directory.CreateDirectory(chemins.Resultats);
            string f = Path.Combine(chemins.Resultats, $"fidelite_v{config.Version}_{ConfigMoteur.SurfaceCle(s)}_graine{graine}.csv");
            File.WriteAllText(f, Comparaison.EnCsv(lignes));
            Console.WriteLine($"Écrit : {f}\n");
        }
    }
}

// Barème : victoire du meilleur et jeux de service tenus selon l'écart de talent, à plusieurs niveaux.
// Styles tirés au hasard à chaque match ; Constance tirée comme dans la population de test (liée au talent), ou fixée à 50.
void Bareme()
{
    int n = options.Entier("matchs", 20000), graine = options.Entier("graine", 1);
    var surface = LireSurface(options.Texte("surface", "dur"));
    var format = FormatMatch.DepuisBestOf(options.Entier("bo", 3));
    bool constanceFixe = options.Contient("constance-fixe");
    var pop = ConfigPopulation.Charger(chemins.ConfigPopulation);
    var moteur = new MoteurMatch(new ModelePoint(config));
    int[] niveaux = [95, 85, 75, 65];
    int[] ecarts = [0, 1, 2, 3, 5, 7, 10, 15, 20];
    Console.WriteLine($"Barème {ConfigMoteur.SurfaceCle(surface)}, Bo{format.BestOf}, {n} matchs par case, Constance {(constanceFixe ? "fixée à 50" : "tirée comme la population de test")}");
    Console.WriteLine("Victoire du meilleur (%) et, entre parenthèses, jeux de service tenus par le meilleur / par l'autre (%)\n");
    Console.WriteLine($"{"écart",6}" + string.Concat(niveaux.Select(t => $"{$"meilleur à {t}",24}")));
    foreach (int d in ecarts)
    {
        var ligne = new System.Text.StringBuilder($"{d,6}");
        foreach (int t in niveaux)
        {
            int victoires = 0; long jA = 0, tA = 0, jB = 0, tB = 0;
            var lockObj = new object();
            Parallel.For(0, n, () => (V: 0, JA: 0L, TA: 0L, JB: 0L, TB: 0L), (i, _, acc) =>
            {
                var rng = new Random(Graines.Deriver(graine, (long)t * 1_000_000 + d * 100_000 + i));
                double Constance(double talent) => constanceFixe ? 50 : Math.Clamp(
                    pop.ConstanceMoyenne + pop.ConstancePenteTalent * (talent - pop.TalentReference) + pop.ConstanceEcartType * Aleatoire.Gauss(rng), 1, 99);
                var a = new Joueur("A", t, Style.Tirer(rng), Constance(t));
                var b = new Joueur("B", t - d, Style.Tirer(rng), Constance(t - d));
                var r = moteur.Jouer(a, b, format, surface, rng);
                return (acc.V + (r.Vainqueur == 0 ? 1 : 0), acc.JA + r.Service[0].Jeux, acc.TA + r.Service[0].JeuxTenus,
                        acc.JB + r.Service[1].Jeux, acc.TB + r.Service[1].JeuxTenus);
            }, acc => { lock (lockObj) { victoires += acc.V; jA += acc.JA; tA += acc.TA; jB += acc.JB; tB += acc.TB; } });
            ligne.Append($"{$"{100.0 * victoires / n:F1} ({100.0 * tA / jA:F1}/{100.0 * tB / jB:F1})",24}");
        }
        Console.WriteLine(ligne);
    }

    // Où se situent les talents dans le classement du simulateur de circuit (moyenne sur les 17 saisons).
    var parRang = new Dictionary<int, List<double>>();
    foreach (var l in File.ReadLines(chemins.Classements).Skip(1))
    {
        var f = l.Split(',');
        int rang = int.Parse(f[1], CultureInfo.InvariantCulture);
        if (!parRang.TryGetValue(rang, out var v)) parRang[rang] = v = [];
        v.Add(double.Parse(f[2], CultureInfo.InvariantCulture));
    }
    Console.WriteLine("\nTalent moyen par rang dans la population (simulateur de circuit, 17 saisons) :");
    Console.WriteLine(string.Join("  ", new[] { 1, 2, 5, 10, 20, 30, 50, 75, 100, 150, 250, 400 }
        .Where(parRang.ContainsKey).Select(r => $"n°{r} : {parRang[r].Average():F1}")));
}

void Calibrer()
{
    int tirages = options.Entier("tirages", 20), graine = options.Entier("graine", 1);
    var chrono = Stopwatch.StartNew();
    var surfaces = SurfacesDemandees();
    var bancs = surfaces.Select(s => Banc.Construire(chemins, s, tirages, graine)).ToList();
    var parametres = new List<Parametre>();
    foreach (var nom in options.Texte("parametres", "centres,pente").Split(','))
        switch (nom.Trim())
        {
            case "centres": parametres.AddRange(surfaces.Select(Parametre.Centre)); break;
            case "pente": parametres.Add(Parametre.Pente); break;
            case "forme_match": parametres.Add(Parametre.FormeMatch); break;
            case "forme_set": parametres.Add(Parametre.FormeSet); break;
            case "enjeu": parametres.Add(Parametre.Enjeu); break;
            default: throw new ArgumentException($"Paramètre inconnu : {nom}");
        }
    string[] familles = options.Contient("familles") ? options.Texte("familles", "").Split(",")
        : options.Contient("controles")
        ? [.. Comparaison.FamillesAjustees, "score", "meme12"]
        : Comparaison.FamillesAjustees;
    Console.WriteLine($"Calibrage sur {bancs.Sum(b => b.Population.Rencontres.Count)} rencontres ({string.Join(", ", surfaces)}), familles : {string.Join(", ", familles)}");
    // --sans-161 : écarte la catégorie « 161 places et plus », biaisée par la population de test (adversaires non classés ramenés au dernier rang simulé).
    Func<CleMesure, bool>? exclure = options.Contient("sans-161") ? c => c.Categorie == "161+" : null;
    var res = Calibreur.Ajuster(config, bancs, parametres, familles, graine, options.Entier("tours", 4), Console.WriteLine, exclure);
    Console.WriteLine($"{res.Evaluations} évaluations en {chrono.Elapsed.TotalSeconds:F0} s ; chi2 = {res.Chi2:F0}");
    if (options.Contient("ecrire"))
    {
        res.Config.Sauver(configChemin);
        Console.WriteLine($"Écrit : {configChemin}");
    }
}

/// <summary>Options en ligne de commande de la forme --nom valeur ou --drapeau.</summary>
sealed class Options
{
    private readonly Dictionary<string, string?> _valeurs = new(StringComparer.OrdinalIgnoreCase);

    public static Options Lire(string[] args)
    {
        var o = new Options();
        for (int i = 0; i < args.Length; i++)
        {
            if (!args[i].StartsWith("--", StringComparison.Ordinal)) continue;
            string nom = args[i][2..];
            string? valeur = i + 1 < args.Length && !args[i + 1].StartsWith("--", StringComparison.Ordinal) ? args[++i] : null;
            o._valeurs[nom] = valeur;
        }
        return o;
    }

    public bool Contient(string nom) => _valeurs.ContainsKey(nom);
    public string Texte(string nom, string defaut) => _valeurs.GetValueOrDefault(nom) ?? defaut;
    public int Entier(string nom, int defaut) =>
        _valeurs.GetValueOrDefault(nom) is { } v ? int.Parse(v, CultureInfo.InvariantCulture) : defaut;
    public double Reel(string nom, double defaut) =>
        _valeurs.GetValueOrDefault(nom) is { } v ? double.Parse(v, CultureInfo.InvariantCulture) : defaut;
}
