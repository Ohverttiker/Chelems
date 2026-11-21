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
    case "point": AnalyserPoint(); break;
    case "caler-echange": CalerEchange(); break;
    default:
        Console.WriteLine("""
            Chelem — moteur de match V0.3 (point en deux phases : duel au service, puis échange ; stats Service, Retour, Échange)

              match     --a 85 --b 78 [--service-a ..] [--retour-a ..] [--echange-a ..] (idem -b) [--bo 3|5] [--surface dur] [--graine 1] [--detail]
              point     [--a 75 --b 75 et stats détaillées comme pour match] [--surface dur] : résultats exacts d'un point (sans simulation)
              caler-echange [--surface dur] [--rappel 1] [--ecart-service 1.5] [--echange] [--pente 0.003375] [--part-service 0.6667] [--ecrire]
                        lois du service de la surface ; sur dur (ou avec --echange) lois de l'échange et pentes, ailleurs vitesse
                        Cale les lois de l'échange (Match Charting Project, Conversion%) puis les pentes, sans simulation.
              fidelite  [--surface dur|terre|gazon|toutes] [--tirages 40] [--graine 1] [--csv]
                        Simulation de masse comparée aux cibles ATP de objectifs_equilibrage/ (dur à la racine, terre/ et gazon/).
              calibrer  [--surface toutes] [--tirages 20] [--graine 1] [--tours 4] [--parametres pentes] [--ecrire]
                        Paramètres possibles : pentes (les deux à rapport fixe), pente_service, pente_echange, vitesse.

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

Joueur LireJoueur(string nom, double defaut)
{
    string k = nom.ToLowerInvariant();
    double t = options.Reel(k, defaut);
    return new Joueur(nom, options.Reel($"service-{k}", t), options.Reel($"retour-{k}", t), options.Reel($"echange-{k}", t));
}

void JouerMatch()
{
    int graine = options.Entier("graine", 1);
    var rng = new Random(graine);
    var surface = LireSurface(options.Texte("surface", "dur"));
    var a = LireJoueur("A", 80);
    var b = LireJoueur("B", 75);
    var moteur = new MoteurMatch(new ModelePoint(config));
    var pa = AnalysePoint.Calculer(moteur.Modele.Preparer(a, b, surface));
    var pb = AnalysePoint.Calculer(moteur.Modele.Preparer(b, a, surface));
    Console.WriteLine($"Points au service (exact) : A {pa.Serveur:P1}, B {pb.Serveur:P1}");
    Action<PointJoue>? obs = options.Contient("detail")
        ? p => Console.WriteLine($"  set {p.Set + 1} {p.JeuxA}-{p.JeuxB} {(p.TieBreak ? "TB " : "")}{p.PointsServeur}-{p.PointsRelanceur} " +
                                 $"serveur {(p.Serveur == 0 ? "A" : "B")} {(p.Deroulement.PremiereRentree ? "1re" : "2e")} " +
                                 $"{p.Deroulement.EtatApresService?.ToString() ?? "-"} {p.Deroulement.Fin} en {p.Deroulement.Coups} coups → {(p.Gagnant == 0 ? "A" : "B")}")
        : null;
    var res = moteur.Jouer(a, b, FormatMatch.DepuisBestOf(options.Entier("bo", 3)), surface, rng, observateur: obs);
    Console.WriteLine(res);
}

// Résultats exacts d'un point entre A (au service) et B, comparés aux cibles du dur quand les deux joueurs sont égaux.
void AnalyserPoint()
{
    var surface = LireSurface(options.Texte("surface", "dur"));
    var a = LireJoueur("A", 75);
    var b = LireJoueur("B", 75);
    var an = AnalysePoint.Calculer(new ModelePoint(config).Preparer(a, b, surface));
    var cibles = CiblesAtp.Charger(chemins.Objectifs(surface));
    string C(string famille, string mesure) =>
        cibles.Valeurs.TryGetValue(new CleMesure(famille, 0, mesure), out var c) ? $"{100 * c.Valeur,6:F1}" : "     -";
    Console.WriteLine($"{a.Nom} (S {a.Service} R {a.Retour} E {a.Echange}) au service contre {b.Nom} (S {b.Service} R {b.Retour} E {b.Echange}), {ConfigMoteur.SurfaceCle(surface)}");
    Console.WriteLine($"{"mesure",-28}{"moteur",8}{"cible",8}   (cible : ATP top 150 « moyen », Match Charting Project « mcp », créateur « efficacite »)");
    void L(string nom, double v, string famille, string mesure) => Console.WriteLine($"{nom,-28}{100 * v,8:F1}  {C(famille, mesure)}");
    L("points gagnés au service", an.Serveur, "moyen", "points_gagnes_service");
    L("gagnés sur 1re", an.GagnesPremiere, "moyen", "points_gagnes_1re");
    L("gagnés sur 2e", an.GagnesSeconde, "moyen", "points_gagnes_2e");
    L("aces / point", an.Aces, "moyen", "aces_par_point_service");
    L("doubles fautes / point", an.DoublesFautes, "moyen", "doubles_fautes_par_point_service");
    L("conversion (serveur, rel.)", an.Conversion, "efficacite", "conversion");
    Console.WriteLine($"{"",-28}{100 * an.ConversionServeur,8:F1}{100 * an.ConversionRelanceur,8:F1}   part des points avec déséquilibre {100 * an.PartDesequilibre:F1} %");
    string[] fins = ["gagnants", "provoquees", "directes"];
    for (int t = 0; t < 4; t++)
    {
        string n = AnalysePoint.NomsTranches[t];
        L($"part {n} coups", an.Part[t], "mcp", $"part_{n}");
        L($"  serveur gagne", an.ServeurParTranche[t], "mcp", $"serveur_{n}");
        for (int k = 0; k < 3; k++) L($"  {fins[k]}", an.Fins[t][k], "mcp", $"{fins[k]}_{n}");
    }
    for (int k = 0; k < 3; k++) L($"{fins[k]} (tous les points)", an.Fins[4][k], "mcp", $"{fins[k]}_total");
    Console.WriteLine($"Longueur moyenne : {an.LongueurMoyenne:F2} coups");
    Console.WriteLine("Probabilité de gagner le point selon l'état du frappeur (attaque / neutre / défense) : " +
                      $"serveur {string.Join(" / ", an.Valeur[0].Select(v => $"{100 * v:F1}"))}, relanceur {string.Join(" / ", an.Valeur[1].Select(v => $"{100 * v:F1}"))}");
}

void CalerEchange()
{
    var surface = LireSurface(options.Texte("surface", "dur"));
    var cibles = CiblesAtp.Charger(chemins.Objectifs(surface));
    // Le dur est la surface de référence : on y cale les lois de l'échange ; ailleurs, seulement la vitesse (et partout le service).
    bool echangeLibre = surface == Surface.Dur || options.Contient("echange");
    var cfg = CaleurEchange.Caler(config, cibles, surface, echangeLibre, options.Reel("rappel", 1), options.Reel("ecart-service", 1.5) / 100, Console.WriteLine);
    if (echangeLibre) cfg = CaleurEchange.CalerPentes(cfg, surface, options.Reel("pente", 0.003375), options.Reel("part-service", 2.0 / 3));
    foreach (var (nom, l) in new[] { ("attaque", cfg.Echange.Attaque), ("neutre", cfg.Echange.Neutre), ("defense", cfg.Echange.Defense) })
        Console.WriteLine($"{nom,-8} gagnant {l.Gagnant:F4}  adv_defense {l.AdvDefense:F4}  neutre {l.Neutre:F4}  adv_attaque {l.AdvAttaque:F4}  faute {l.Faute:F4}");
    var (pr, l1, l2) = cfg.LoisService(surface);
    Console.WriteLine($"service ({ConfigMoteur.SurfaceCle(surface)}) : premières {pr:F4} ; vitesse {cfg.Surface(surface).Vitesse}");
    foreach (var (nom, l) in new[] { ("premiere", l1), ("seconde", l2) })
        Console.WriteLine($"{nom,-8} ace {l.Ace:F4}  gagnant {l.Gagnant:F4}  attaque {l.Attaque:F4}  neutre {l.Neutre:F4}  defense {l.Defense:F4}  retour_gagnant {l.RetourGagnant:F4}  double_faute {l.DoubleFaute:F4}");
    Console.WriteLine($"pentes : service {cfg.Service.Pente}, échange {cfg.Echange.Pente}");
    foreach (var t in CaleurEchange.Termes())
        if (cibles.Valeurs.TryGetValue(t.Cle, out var c))
            Console.WriteLine($"  {t.Cle,-34}{100 * c.Valeur,7:F1}{100 * t.Lire(CaleurEchange.Analyser(cfg, surface)),7:F1}");
    if (options.Contient("ecrire"))
    {
        cfg.Sauver(configChemin);
        Console.WriteLine($"Écrit : {configChemin}");
    }
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
// Les trois stats de chaque joueur valent son talent.
void Bareme()
{
    int n = options.Entier("matchs", 20000), graine = options.Entier("graine", 1);
    var surface = LireSurface(options.Texte("surface", "dur"));
    var format = FormatMatch.DepuisBestOf(options.Entier("bo", 3));
    var moteur = new MoteurMatch(new ModelePoint(config));
    int[] niveaux = [95, 85, 75, 65];
    int[] ecarts = [0, 1, 2, 3, 5, 7, 10, 15, 20];
    Console.WriteLine($"Barème {ConfigMoteur.SurfaceCle(surface)}, Bo{format.BestOf}, {n} matchs par case");
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
                var a = new Joueur("A", t);
                var b = new Joueur("B", t - d);
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
    foreach (var nom in options.Texte("parametres", "pentes").Split(','))
        switch (nom.Trim())
        {
            case "pentes": parametres.Add(Parametre.Pentes); break;
            case "pente_service": parametres.Add(Parametre.PenteService); break;
            case "pente_echange": parametres.Add(Parametre.PenteEchange); break;
            case "vitesse": parametres.AddRange(surfaces.Select(Parametre.Vitesse)); break;
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
