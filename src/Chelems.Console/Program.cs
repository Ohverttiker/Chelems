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
    case "efficacite-joueurs": EfficaciteJoueurs(); break;
    case "caler-stats": CalerStats(); break;
    case "profils-joueurs": ProfilsJoueurs(); break;
    case "caler-echelles": CalerEchelles(); break;
    case "caler-specialisation": CalerSpecialisation(); break;
    default:
        Console.WriteLine("""
            Chelem — moteur de match V0.3 (point en deux phases : duel au service, puis échange ; stats Service, Retour, Échange)

              match     --a 85 --b 78 [--service-a ..] [--retour-a ..] [--echange-a ..] (idem -b) [--bo 3|5] [--surface dur] [--graine 1] [--detail]
              point     [--a 75 --b 75 et stats détaillées comme pour match] [--surface dur] : résultats exacts d'un point (sans simulation)
              caler-echange [--surface dur] [--rappel 1] [--ecart-service 1.5] [--echange] [--pente 0.003375] [--part-service 0.6667] [--ecrire]
                        lois du service de la surface ; sur dur (ou avec --echange) lois de l'échange et pentes, ailleurs vitesse
                        Cale les lois de l'échange (Match Charting Project, Conversion%) puis les pentes, sans simulation.
              efficacite-joueurs [--surface dur] [--joueurs 40] : efficacité au service des joueurs réels (MCP), attaque / neutre / défense déduits
              caler-stats [--surface dur] [--premieres-par-point 0.163] [--part-service 0.6] [--agressivite-mesuree] [--ecrire] : effets des stats sur le service (Taille, Agressivité calées sur les joueurs réels)
              profils-joueurs [--surface dur] [--joueurs 30 | --noms Isner,Zverev] : stats de service estimées des joueurs réels, et ce que le moteur en reproduit
              caler-echelles [--surface dur] [--ecart-type 6.4] [--tours 4] [--ecrire] : effet par point des stats de service, pour que les joueurs réels suivent l'échelle
              caler-specialisation [--surface dur] [--rang-max 81] [--ecrire] : profils Service / Retour / Échange de la population (spécialisation ATP)
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
    return new Joueur(nom, options.Reel($"service-{k}", t), options.Reel($"retour-{k}", t), options.Reel($"echange-{k}", t),
                      options.Reel($"puissance-{k}", 75), options.Reel($"explosivite-{k}", 75), options.Reel($"taille-{k}", 188),
                      options.Reel($"agressivite-{k}", 50), options.Reel($"regularite-{k}", 75));
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

// Roadmap, étape 1 : efficacité au service des joueurs réels (MCP), partage attaque / neutre / défense déduit des points gagnés.
void EfficaciteJoueurs()
{
    var surface = LireSurface(options.Texte("surface", "dur"));
    string fichier = options.Texte("fichier", Path.Combine(chemins.Objectifs(surface), "efficacite_joueurs.csv"));
    var joueurs = EfficaciteJoueur.Charger(fichier, config, surface);
    int n = options.Entier("joueurs", 40);
    static string L(IssuesService l, bool seconde) =>
        $"{100 * l.Ace,5:F1}{100 * l.Gagnant,5:F1}{100 * l.Attaque,5:F1}{100 * l.Neutre,5:F1}{100 * l.Defense,5:F1}{100 * l.RetourGagnant,5:F1}" +
        (seconde ? $"{100 * l.DoubleFaute,5:F1}" : "");
    Console.WriteLine($"{joueurs.Count} joueurs ({fichier}). Première : ace gagnant attaque neutre défense retour_gagnant ; seconde : idem + double faute.");
    Console.WriteLine($"{"joueur",-24}{"taille",7}{"1res",6}  {"première (%)",-30}{"x1",6}  {"seconde (%)",-35}{"échange",8}");
    foreach (var j in joueurs.Take(n))
        Console.WriteLine($"{j.Nom,-24}{j.TailleCm,7:F0}{100 * j.Premieres,6:F1}  {L(j.Premiere, false),-30}{j.DecalagePremiere,6:F2}  {L(j.Seconde, true),-35}{j.EcartEchange,8:+0.0;-0.0}");

    // Lien avec la taille (seul attribut mesuré pour l'instant) : pente par 10 cm, pondérée par les points au service.
    var avecTaille = joueurs.Where(j => !double.IsNaN(j.TailleCm)).ToList();
    void Pente(string nom, Func<EfficaciteJoueur, double> y)
    {
        double w = avecTaille.Sum(j => j.PointsService);
        double mx = avecTaille.Sum(j => j.PointsService * j.TailleCm) / w, my = avecTaille.Sum(j => j.PointsService * y(j)) / w;
        double sxy = avecTaille.Sum(j => j.PointsService * (j.TailleCm - mx) * (y(j) - my));
        double sxx = avecTaille.Sum(j => j.PointsService * (j.TailleCm - mx) * (j.TailleCm - mx));
        double syy = avecTaille.Sum(j => j.PointsService * (y(j) - my) * (y(j) - my));
        Console.WriteLine($"  {nom,-28} moyenne {100 * my,5:F1}   pente {1000 * sxy / sxx,6:F2} pt / 10 cm   corrélation {sxy / Math.Sqrt(sxx * syy),5:F2}");
    }
    Console.WriteLine($"Lien avec la taille ({avecTaille.Count} joueurs, moyennes pondérées par les points au service) :");
    Pente("premières balles", j => j.Premieres);
    Pente("aces sur première", j => j.Premiere.Ace);
    Pente("gagnants sur première", j => j.Premiere.Gagnant);
    Pente("attaque sur première", j => j.Premiere.Attaque);
    Pente("neutre sur première", j => j.Premiere.Neutre);
    Pente("points gagnés sur première", j => j.GagnesPremiere);
    Pente("aces sur seconde", j => j.Seconde.Ace);
    Pente("gagnants sur seconde", j => j.Seconde.Gagnant);
    Pente("doubles fautes", j => j.Seconde.DoubleFaute);
    Pente("points gagnés sur seconde", j => j.GagnesSeconde);
    Pente("écart d'Échange (pts de stat / 100)", j => j.EcartEchange / 100);
    Pente("décalage du service (x1 / 100)", j => j.DecalagePremiere / 100);
}

// Roadmap, étape 1 : effets des stats sur le duel au service. Taille et Agressivité calées sur les pentes mesurées entre joueurs réels ;
// Service / Retour et Puissance / Explosivité : effets de design, par paires qui s'annulent à valeurs égales.
void CalerStats()
{
    var surface = LireSurface(options.Texte("surface", "dur"));
    var joueurs = EfficaciteJoueur.Charger(Path.Combine(chemins.Objectifs(surface), "efficacite_joueurs.csv"), config, surface);
    var pentes = CaleurStats.Mesurer(joueurs);
    // Échelle de l'Agressivité [Proposé] : de 1 à 99, la part de premières passe d'environ 72 % à 56 % (étendue ATP sur 52 semaines).
    double echelle = options.Reel("premieres-par-point", 0.163) / 100;
    double[] cibleTaille = pentes.ParCm;
    // Agressivité [Proposé, créateur 2026-11-25] : le risque doit payer sur la première. Par défaut, à capacité de référence, il ne change
    // pas le total des points gagnés au service : moins de premières et plus de doubles fautes (pentes mesurées), compensés par plus
    // de points gagnés sur première. Les pentes mesurées brutes (--agressivite-mesuree) en font un pur handicap : elles mêlent
    // risque et capacité (les bons serveurs passent à la fois beaucoup de premières et une bonne seconde).
    double[] cibleAgressivite;
    if (options.Contient("agressivite-mesuree"))
        cibleAgressivite = [.. pentes.ParPointDePremieres.Select((v, g) => g is 4 or 5 ? 0 : -echelle * v)];
    else
    {
        var egaux = AnalysePoint.Calculer(new ModelePoint(config with { Service = config.Service with { Stats = null } })
                                              .Preparer(new Joueur("S", 75), new Joueur("R", 75), surface));
        double pr = egaux.PremieresRentrees, w1 = egaux.GagnesPremiere, w2 = egaux.GagnesSeconde;
        double df = egaux.IssuesService[1][(int)IssueService.DoubleFaute];
        double dPr = -echelle, dDf = -echelle * pentes.ParPointDePremieres[6];
        double dW2 = -w2 / (1 - df) * dDf;                         // chaque double faute en plus coûte un point qui aurait été joué
        double dW1 = -(dPr * (w1 - w2) + (1 - pr) * dW2) / pr;       // total des points au service inchangé
        cibleAgressivite = [dPr, -echelle * pentes.ParPointDePremieres[1], -echelle * pentes.ParPointDePremieres[2], dW1, 0, 0, dDf, dW2];
    }
    var agressivite = CaleurStats.Caler(config, surface, 50, cibleAgressivite);
    // Service (placement, effets) : services gagnants et coup suivant en attaque ; sur seconde, moins de doubles fautes.
    // Puissance (vitesse) : aces et services gagnants. Retour et Explosivité en sont les pendants côté relanceur [créateur, 2026-11-25].
    // Aces à 0,7 : la valeur qui laisse la part des aces stable quand le Service ou le Retour varient
    // Retour (sinon, la renormalisation la fait monter avec le Retour : un grand relanceur, qui concède des aces, recevait un gros Retour).
    var qualite1 = new IssuesService(0.7, 1, 1, 0, -1, -1, 0);
    var qualite2 = qualite1; // les doubles fautes relèvent de la Régularité (créateur, 2026-11-25)
    // Régularité [créateur] : une part des premières balles (l'autre est l'Agressivité), et la plus grande part des doubles fautes.
    var regularite1 = new IssuesService(0, 0, 0, 0, 0, 0, 0);
    var regularite2 = new IssuesService(0, 0, 0, 0, 0, 0, -4);
    var vitesse = new IssuesService(1, 1, 0, 0, 0, 0, 0);
    var stats = new Dictionary<string, EffetStat>(config.Service.Stats ?? []) { ["agressivite"] = agressivite };
    double partService = options.Reel("part-service", 0.6);
    // Échelles réglées par caler-echelles. Puissance et Explosivité : stats de style, effet absolu par point (sans part de la pente).
    double Echelle(string cle, double defaut = 1) => config.Service.Stats?.GetValueOrDefault(cle)?.Echelle ?? defaut;
    // Ce dont chaque balle tire son résultat [créateur, 2026-11-26], par ordre d'importance :
    //   1re balle : Taille, Service, Puissance, Agressivité, Régularité ;  2e balle : Taille, Régularité, Service, Agressivité ;
    //   retour de 1re : Retour, Explosivité, Taille, Échange ;  retour de 2e : Retour, Échange, Explosivité, Taille.
    // La Puissance ne joue qu'au service et seulement sur la première ; l'Explosivité, qu'au retour (réflexes face aux services rapides),
    // surtout sur la première. Poids sur la seconde [Proposé] : Service 0,7, Retour 0,6, Explosivité 0,3.
    // Retour de 1re et de 2e sans l'Échange [créateur, 2026-11-26] : la conversion des points lancés relève de l'échange.
    static IssuesService Fois(IssuesService o, double k) => IssuesService.DepuisTableau([.. o.EnTableau().Select(v => v * k)]);
    var zero = new IssuesService(0, 0, 0, 0, 0, 0, 0);
    stats.Remove("echange_relanceur"); // [créateur, 2026-11-26] l'Échange ne joue pas sur la capacité à retourner : il convertit les points lancés (Nadal, Federer)
    stats["service"] = new EffetStat(75, qualite1, Fois(qualite2, 0.7), 0, null, Echelle("service", 0.0138));
    stats["retour"] = new EffetStat(75, qualite1, Fois(qualite2, 0.6), 0, null, Echelle("retour", 0.0138));
    stats["puissance"] = new EffetStat(75, vitesse, zero, 0, null, Echelle("puissance", 0.007));
    stats["regularite"] = new EffetStat(75, regularite1, regularite2, 1, null, Echelle("regularite", 0.0137));
    stats["explosivite"] = new EffetStat(75, vitesse, Fois(vitesse, 0.3), 0, null, Echelle("explosivite", 0.007));
    // Taille [créateur, 2026-11-25] : l'effet mesuré de la taille passe en partie par la Puissance (plus grand, plus puissant) et, au
    // retour, par l'Explosivité (plus grand, moins explosif) : Puissance = 75 + a × (taille − 188), Explosivité = 75 − b × (taille − 188)
    // dans la population (config/population.json). L'effet direct de la Taille (hauteur de frappe, marge au service ; gêne au retour)
    // est le reste : pentes mesurées moins ce qui passe par ces deux stats.
    var pop = ConfigPopulation.Charger(chemins.ConfigPopulation);
    stats.Remove("taille"); stats.Remove("taille_relanceur");
    var sansTaille = config with { Service = config.Service with { Stats = new(stats) } };
    var parPuissance = CaleurStats.PentesDuMoteur(sansTaille, surface, (j, d) => j with { Puissance = 75 + d }, 5);
    var parExplosivite = CaleurStats.PentesDuMoteurAuRetour(sansTaille, surface, (j, d) => j with { Explosivite = 75 + d }, 5);
    double[] cibleTailleDirecte = [.. cibleTaille.Select((v, g) => v - pop.TaillePuissance * parPuissance[g])];
    double[] cibleTailleRelanceur = [.. pentes.ParCmRelanceur.Select((v, g) => v + pop.TailleExplosivite * parExplosivite[g])];
    var taille = CaleurStats.Caler(sansTaille, surface, 188, cibleTailleDirecte);
    var tailleRelanceur = CaleurStats.Caler(sansTaille, surface, 188, cibleTailleRelanceur);
    stats["taille"] = taille;
    stats["taille_relanceur"] = tailleRelanceur;
    var cfg = config with { Service = config.Service with { Stats = stats } };

    Console.WriteLine($"Pentes mesurées sur {pentes.Joueurs} joueurs (MCP, {ConfigMoteur.SurfaceCle(surface)}), comparées au moteur :");
    void Tableau(string titre, double[] cible, double[] moteur, double unite, string nomUnite)
    {
        Console.WriteLine($"  {titre} (points de % par {nomUnite}) :");
        for (int g = 0; g < 8; g++)
            Console.WriteLine($"    {CaleurStats.Grandeurs[g],-18}{(double.IsNaN(cible[g]) ? "   libre" : $"{100 * unite * cible[g],8:+0.00;-0.00}")}{100 * unite * moteur[g],9:+0.00;-0.00}");
    }
    Tableau("Taille du serveur, effet total mesuré", cibleTaille,
            CaleurStats.PentesDuMoteur(cfg, surface, (j, d) => j with { TailleCm = 188 + d, Puissance = 75 + pop.TaillePuissance * d }, 5), 10, "10 cm");
    Tableau("  dont effet direct", cibleTailleDirecte, CaleurStats.PentesDuMoteur(cfg, surface, (j, d) => j with { TailleCm = 188 + d }, 5), 10, "10 cm");
    Tableau("Taille du relanceur, effet total mesuré", pentes.ParCmRelanceur,
            CaleurStats.PentesDuMoteurAuRetour(cfg, surface, (j, d) => j with { TailleCm = 188 + d, Explosivite = 75 - pop.TailleExplosivite * d }, 5), 10, "10 cm");
    Tableau("  dont effet direct", cibleTailleRelanceur, CaleurStats.PentesDuMoteurAuRetour(cfg, surface, (j, d) => j with { TailleCm = 188 + d }, 5), 10, "10 cm");
    Tableau("Agressivité", cibleAgressivite, CaleurStats.PentesDuMoteur(cfg, surface, (j, d) => j with { Agressivite = 50 + d }, 10), 10, "10 points");
    Console.WriteLine(options.Contient("agressivite-mesuree")
        ? "  Agressivité : pentes mesurées brutes (aces et services gagnants sur seconde fixés à 0)."
        : "  Agressivité : neutre à capacité de référence (total des points au service inchangé), le risque paie sur la première.");
    Console.WriteLine($"  Mesure à taille égale, pour 1 point de premières en moins : seconde ace {-pentes.ParPointDePremieres[4]:+0.000;-0.000}, gagnant {-pentes.ParPointDePremieres[5]:+0.000;-0.000} point de %");
    foreach (var (nom, e) in new[] { ("taille", taille), ("taille_rel.", tailleRelanceur), ("agressivite", agressivite) })
        Console.WriteLine($"  {nom,-12} premières {e.Rentree:+0.0000;-0.0000}  1re [{string.Join(" ", e.Premiere.EnTableau().Select(v => v.ToString("+0.0000;-0.0000")))}]" +
                          $"  2e [{string.Join(" ", e.Seconde.EnTableau().Select(v => v.ToString("+0.0000;-0.0000")))}]");
    if (options.Contient("ecrire"))
    {
        cfg.Sauver(configChemin);
        Console.WriteLine($"Écrit : {configChemin}");
    }
}

// Roadmap, étape 1 : stats de service estimées pour les joueurs réels (MCP), et ce que le moteur reproduit de leurs statistiques.
void ProfilsJoueurs()
{
    var surface = LireSurface(options.Texte("surface", "dur"));
    var joueurs = EfficaciteJoueur.Charger(Path.Combine(chemins.Objectifs(surface), "efficacite_joueurs.csv"), config, surface);
    string? noms = options.Contient("noms") ? options.Texte("noms", "") : null;
    var choisis = noms is null ? joueurs.Take(options.Entier("joueurs", 30)).ToList()
                               : [.. joueurs.Where(j => noms.Split(',').Any(n => j.Nom.Contains(n.Trim(), StringComparison.OrdinalIgnoreCase)))];
    var profils = choisis.AsParallel().AsOrdered().Select(j => ProfilJoueur.Estimer(config, surface, j, ProfilJoueur.APriori.Depuis(ConfigPopulation.Charger(chemins.ConfigPopulation)))).ToList();
    Console.WriteLine("Stats estimées (a priori : niveau N(72,8 ; 6,4), Agressivité N(50 ; 20)) face à un adversaire de référence (72,8 partout, 1m88) ; mesuré MCP → moteur, en %.");
    Console.WriteLine($"{"joueur",-22}{"taille",7}{"Serv",5}{"Puis",5}{"Agr",5}{"Éch",5}{"Rég",5}{"Ret",5}{"Exp",5}  {"1res",-12}{"aces 1re",-12}{"gagn. 1re",-12}{"pts 1re",-12}{"DF / 2e",-12}{"pts 2e",-12}{"χ²/8",6}{"χ² ret.",8}");
    string C(double m, double s) => $"{100 * m,5:F1}→{100 * s,-5:F1} ";
    foreach (var p in profils)
    {
        var m = p.Mesure; var e = p.Estime; var s = p.Simule;
        Console.WriteLine($"{m.Nom,-22}{m.TailleCm,7:F0}{e.Service,5:F0}{e.Puissance,5:F0}{e.Agressivite,5:F0}{e.Echange,5:F0}{e.Regularite,5:F0}{e.Retour,5:F0}{e.Explosivite,5:F0}  " +
                          $"{C(m.Premieres, s[0])}{C(m.Premiere.Ace, s[1])}{C(m.Premiere.Gagnant, s[2])}{C(m.GagnesPremiere, s[3])}{C(m.Seconde.DoubleFaute, s[6])}{C(m.GagnesSeconde, s[7])}{p.Ecart,6:F1}{p.EcartRetour,8:F1}");
    }
    static double Correlation(List<(double X, double Y)> v)
    {
        double mx = v.Average(p => p.X), my = v.Average(p => p.Y);
        double sxy = v.Sum(p => (p.X - mx) * (p.Y - my)), sxx = v.Sum(p => (p.X - mx) * (p.X - mx)), syy = v.Sum(p => (p.Y - my) * (p.Y - my));
        return sxy / Math.Sqrt(sxx * syy);
    }
    var ecarts = profils.Select(p => p.Ecart).OrderBy(x => x).ToList();
    Console.WriteLine($"χ²/8 médian {ecarts[ecarts.Count / 2]:F1} (1 = écart de l'ordre du bruit de mesure) ; " +
                      $"{ecarts.Count(x => x <= 2)} joueurs ≤ 2, {ecarts.Count(x => x <= 5)} ≤ 5, {ecarts.Count(x => x > 10)} > 10.");
    var ecartsR = profils.Select(p => p.EcartRetour).Where(x => !double.IsNaN(x)).OrderBy(x => x).ToList();
    if (ecartsR.Count > 0) Console.WriteLine($"Au retour : χ² par grandeur médian {ecartsR[ecartsR.Count / 2]:F1}.");
    // Répartition des stats estimées (créateur : centrée vers 70, très peu au-delà de 90).
    foreach (var (nom, lire) in new (string, Func<Joueur, double>)[] { ("Service", e => e.Service), ("Puissance", e => e.Puissance), ("Agressivité", e => e.Agressivite), ("Échange", e => e.Echange), ("Régularité", e => e.Regularite), ("Retour", e => e.Retour), ("Explosivité", e => e.Explosivite) })
    {
        var v = profils.Select(p => lire(p.Estime)).ToList();
        double moy = v.Average(), et = Math.Sqrt(v.Sum(x => (x - moy) * (x - moy)) / v.Count);
        Console.WriteLine($"  {nom,-12} moyenne {moy,5:F1}  écart-type {et,4:F1}  min {v.Min(),3:F0}  max {v.Max(),3:F0}  au-delà de 90 : {v.Count(x => x > 90)}  corrélation avec la taille {Correlation(profils.Where(p => !double.IsNaN(p.Mesure.TailleCm)).Select(p => (p.Mesure.TailleCm, lire(p.Estime))).ToList()),5:F2}");
    }
}

// Échelle des stats [créateur, 2026-11-25] : répartition normale autour de 70, très peu au-delà de 90, 99 pour une saison parfaite.
// L'effet par point de chaque stat de service est agrandi ou réduit jusqu'à ce que les joueurs réels (MCP), estimés avec l'a priori
// de l'échelle, se répartissent avec son écart-type (6,4). Service / Retour et Puissance / Explosivité gardent la même échelle.
void CalerEchelles()
{
    var surface = LireSurface(options.Texte("surface", "dur"));
    var joueurs = EfficaciteJoueur.Charger(Path.Combine(chemins.Objectifs(surface), "efficacite_joueurs.csv"), config, surface);
    double cible = options.Reel("ecart-type", 6.4);
    var cfg = config;
    for (int tour = 1; tour <= options.Entier("tours", 4); tour++)
    {
        var c = cfg;
        var profils = joueurs.AsParallel().Select(j => ProfilJoueur.Estimer(c, surface, j, ProfilJoueur.APriori.Depuis(ConfigPopulation.Charger(chemins.ConfigPopulation)))).ToList();
        double Et(Func<Joueur, double> lire)
        {
            var v = profils.Select(p => lire(p.Estime)).ToList();
            double m = v.Average();
            return Math.Sqrt(v.Sum(x => (x - m) * (x - m)) / v.Count);
        }
        double etS = Et(e => e.Service), etP = Et(e => e.Puissance), etR = Et(e => e.Regularite), etE = Et(e => e.Explosivite);
        var stats = new Dictionary<string, EffetStat>(cfg.Service.Stats!);
        foreach (var (cles, et) in new[] { (new[] { "service", "retour" }, etS), (["puissance"], etP), (["explosivite"], etE), (["regularite"], etR) })
            foreach (var cle in cles) stats[cle] = stats[cle] with { Echelle = Math.Round(stats[cle].Echelle * et / cible, 4) };
        cfg = cfg with { Service = cfg.Service with { Stats = stats } };
        var med = profils.Select(p => p.Ecart).OrderBy(x => x).ElementAt(profils.Count / 2);
        Console.WriteLine($"tour {tour} : écart-type estimé Service {etS:F1}, Puissance {etP:F1}, Explosivité {etE:F1}, Régularité {etR:F1} (χ²/8 médian {med:F1}) → échelles Puissance {stats["puissance"].Echelle}, Régularité {stats["regularite"].Echelle}");
    }
    if (options.Contient("ecrire"))
    {
        cfg.Sauver(configChemin);
        // β (part du talent suivie par Service et Retour dans la population) : l'effet du talent sur le service, β × effet d'un point
        // de Service, reste celui que la simulation de masse a calé.
        double avant = config.Service.Stats!["service"].Echelle, apres = cfg.Service.Stats!["service"].Echelle;
        var pop = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(chemins.ConfigPopulation))!.AsObject();
        double beta = (pop["specialisation_part_talent"]?.GetValue<double>() ?? 1) * avant / apres;
        pop["specialisation_part_talent"] = Math.Round(beta, 4);
        File.WriteAllText(chemins.ConfigPopulation, pop.ToJsonString(new System.Text.Json.JsonSerializerOptions
            { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping }) + Environment.NewLine);
        Console.WriteLine($"Écrit : {configChemin} et {chemins.ConfigPopulation} (β = {beta:F3}). Recaler ensuite la spécialisation (caler-specialisation --ecrire).");
    }
}

// Roadmap, étape 1 : profils Service / Retour / Échange de la population de test, calés sur la spécialisation mesurée à l'ATP.
void CalerSpecialisation()
{
    var surface = LireSurface(options.Texte("surface", "dur"));
    int rangMax = options.Entier("rang-max", 81);
    var joueurs = CaleurSpecialisation.JoueursDuSimulateur(chemins.Classements, rangMax);
    var cible = CaleurSpecialisation.MesurerAtp(Path.Combine(chemins.Objectifs(Surface.Dur), "atp_classements_52_semaines.csv"));
    Console.WriteLine($"ATP (top 81, 52 semaines) : écart-type service {cible.EcartTypeService:F2}, retour {cible.EcartTypeRetour:F2}, corrélation {cible.Correlation:F2}");
    var pop = ConfigPopulation.Charger(chemins.ConfigPopulation);
    var sans = CaleurSpecialisation.Simuler(config, surface, joueurs, 0, 0, 1, pop.TalentReference, pop);
    Console.WriteLine($"Population sans spécialisation (taille, Puissance et Explosivité tirées) ({joueurs.Count} joueurs de rang ≤ {rangMax}) : service {sans.EcartTypeService:F2}, retour {sans.EcartTypeRetour:F2}, corrélation {sans.Correlation:F2}");
    Console.WriteLine($"Part du talent suivie par Service et Retour : β = {pop.SpecialisationPartTalent}");
    var (et, rho, _) = CaleurSpecialisation.Caler(config, surface, joueurs, cible, pop.SpecialisationPartTalent, pop.TalentReference, pop, Console.WriteLine);
    if (options.Contient("ecrire"))
    {
        var noeud = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(chemins.ConfigPopulation))!.AsObject();
        noeud["specialisation_ecart_type"] = et;
        noeud["specialisation_correlation"] = rho;
        File.WriteAllText(chemins.ConfigPopulation, noeud.ToJsonString(new System.Text.Json.JsonSerializerOptions
            { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping }) + Environment.NewLine);
        Console.WriteLine($"Écrit : {chemins.ConfigPopulation}");
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
