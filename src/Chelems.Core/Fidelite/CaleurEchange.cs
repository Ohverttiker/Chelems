using Chelems.Core.Configuration;

namespace Chelems.Core.Fidelite;

/// <summary>
/// Calage exact d'une surface entre deux joueurs égaux, sans simulation : <see cref="AnalysePoint"/> donne directement
/// la longueur des échanges, les fins de point, les points au service et Conversion%.
/// Paramètres libres :
/// - les lois du duel au service de la surface (première et seconde), rappelées vers les lois de référence du créateur ;
///   la part de premières rentrées est celle de l'ATP sur la surface ;
/// - sur la surface de référence (dur) : les lois de l'échange (trois états × cinq issues), rappelées vers le profil de référence ;
///   sur les autres : la seule vitesse de la surface, les lois de l'échange restant celles du dur.
/// Puis calcul des pentes : écart de p(point au service) par point de stat, partagé entre Service et Échange.
/// </summary>
public static class CaleurEchange
{
    /// <summary>Une cible du calage : clé de <see cref="CiblesAtp"/>, tolérance en points de %, et lecture dans l'analyse.</summary>
    public sealed record Terme(CleMesure Cle, double Tolerance, Func<AnalysePoint, double> Lire);

    public static List<Terme> Termes()
    {
        var t = new List<Terme>();
        string[] noms = AnalysePoint.NomsTranches;
        string[] fins = ["gagnants", "provoquees", "directes"];
        for (int k = 0; k < 4; k++)
        {
            int kk = k;
            t.Add(new(new("mcp", 0, $"part_{noms[k]}"), 1.0, a => a.Part[kk]));
            t.Add(new(new("mcp", 0, $"serveur_{noms[k]}"), 1.0, a => a.ServeurParTranche[kk]));
            for (int f = 0; f < 3; f++)
            {
                int ff = f;
                t.Add(new(new("mcp", 0, $"{fins[f]}_{noms[k]}"), 1.5, a => a.Fins[kk][ff]));
            }
        }
        // Fins de point hors service (définition du créateur : aces, services gagnants et doubles fautes exclus).
        for (int f = 0; f < 3; f++)
        {
            int ff = f;
            t.Add(new(new("mcp", 0, $"{fins[f]}_hors_service"), 1.0, a => a.FinsHorsService[ff]));
        }
        // Service : la bible ATP de la surface (top 150).
        t.Add(new(new("moyen", 0, "points_gagnes_1re"), 0.5, a => a.GagnesPremiere));
        t.Add(new(new("moyen", 0, "points_gagnes_2e"), 0.5, a => a.GagnesSeconde));
        t.Add(new(new("moyen", 0, "aces_par_point_service"), 0.3, a => a.Aces));
        t.Add(new(new("moyen", 0, "doubles_fautes_par_point_service"), 0.3, a => a.DoublesFautes));
        t.Add(new(new("efficacite", 0, "conversion"), 0.5, a => a.Conversion));
        return t;
    }

    public static double Ecart(ConfigMoteur cfg, CiblesAtp cibles, Surface surface, IReadOnlyList<Terme> termes)
    {
        var a = Analyser(cfg, surface);
        double s = 0;
        foreach (var t in termes)
        {
            if (!cibles.Valeurs.TryGetValue(t.Cle, out var c)) continue;
            double e = 100 * (t.Lire(a) - c.Valeur) / t.Tolerance;
            s += e * e;
        }
        return s;
    }

    public static AnalysePoint Analyser(ConfigMoteur cfg, Surface surface, double ecartService = 0, double ecartEchange = 0)
    {
        var m = new ModelePoint(cfg);
        var serveur = new Joueur("S", 75 + ecartService / 2, 75, 75 + ecartEchange / 2);
        var relanceur = new Joueur("R", 75, 75 - ecartService / 2, 75 - ecartEchange / 2);
        return AnalysePoint.Calculer(m.Preparer(serveur, relanceur, surface));
    }

    private static readonly int[] IssuesEchangeLibres = [0, 1, 3, 4];      // relatives à Neutre
    private static readonly int[] IssuesPremiereLibres = [0, 1, 2, 4, 5];  // relatives à Neutre, pas de double faute
    private static readonly int[] IssuesSecondeLibres = [0, 1, 2, 4, 5, 6];
    private const int Neutre = 2, NeutreService = 3;

    /// <summary>Log-rapports log(p_k / p_ref) des issues libres (la référence vaut 1 avant normalisation).</summary>
    private static IEnumerable<double> LogRapports(double[] p, int reference, int[] issues) =>
        issues.Select(k => Math.Log(Math.Max(p[k], 1e-9) / Math.Max(p[reference], 1e-9)));

    private static double[] DepuisLogRapports(int n, int reference, int[] issues, double[] x, int debut, bool arrondir)
    {
        var v = new double[n];
        v[reference] = 1;
        for (int i = 0; i < issues.Length; i++) v[issues[i]] = Math.Exp(x[debut + i]);
        double s = v.Sum();
        for (int k = 0; k < n; k++) v[k] /= s;
        return arrondir ? Corriger(v) : v;
    }

    /// <summary>Après arrondi à 6 décimales, reporte le reliquat sur la plus grande issue pour que la somme fasse exactement 1.</summary>
    private static double[] Corriger(double[] loi)
    {
        var p = loi.Select(y => Math.Round(y, 6)).ToArray();
        int k = Array.IndexOf(p, p.Max());
        p[k] = Math.Round(p[k] + 1 - p.Sum(), 6);
        return p;
    }

    private static double[] ParametresEchange(ConfigEchange e) =>
        [.. new[] { e.Attaque, e.Neutre, e.Defense }.SelectMany(x => LogRapports(x.EnTableau(), Neutre, IssuesEchangeLibres))];

    private static double[] ParametresService(IssuesService premiere, IssuesService seconde) =>
        [.. LogRapports(premiere.EnTableau(), NeutreService, IssuesPremiereLibres), .. LogRapports(seconde.EnTableau(), NeutreService, IssuesSecondeLibres)];

    /// <param name="echangeLibre">Vrai sur la surface de référence : les lois de l'échange sont calées ; sinon seule la vitesse l'est.</param>
    /// <param name="rappelEchange">
    /// Poids du rappel des lois de l'échange vers le profil de référence (somme des carrés des écarts de log-rapports).
    /// Les données ne disent pas dans quel état surviennent fautes et coups gagnants : sans rappel, le calage part dans un coin.
    /// </param>
    /// <param name="ecartService">
    /// Écart type admis (en proportion) entre les lois du service calées et la référence de la surface (<see cref="ReferenceService"/>) :
    /// chaque issue paie ((p − référence) / écart)². Avec 0,015, s'écarter de 1,5 point coûte autant qu'une cible manquée d'une tolérance.
    /// Décision du créateur : les lois du service restent proches de la réalité, quelques points au plus.
    /// </param>
    public static ConfigMoteur Caler(ConfigMoteur cfg, CiblesAtp cibles, Surface surface, bool echangeLibre,
                                     double rappelEchange = 1, double ecartService = 0.015, Action<string>? journal = null, int iterations = 6000)
    {
        // Conversion% : moyenne du tour toutes surfaces, cible de la seule surface de référence (elle doit varier avec la vitesse).
        var termes = echangeLibre ? Termes() : Termes().Where(t => t.Cle.Famille != "efficacite").ToList();
        string cle = ConfigMoteur.SurfaceCle(surface);
        var surf = cfg.Surface(surface);
        double prActuelle = cfg.LoisService(surface).PremiereRentree;
        double premiereRentree = cibles.Valeurs.TryGetValue(new("moyen", 0, "premieres_balles"), out var pb) ? Math.Round(pb.Valeur, 4) : prActuelle;
        PartsFauteRetour? fautesRetour = cibles.Valeurs.TryGetValue(new("mcp", 0, "premiere_faute_retour"), out var f1)
                                         && cibles.Valeurs.TryGetValue(new("mcp", 0, "seconde_faute_retour"), out var f2)
            ? new(Math.Round(f1.Valeur, 4), Math.Round(f2.Valeur, 4)) : surf.FautesRetour;
        var profil = cfg.Echange.Profil is { } p ? cfg.Echange with { Attaque = p.Attaque, Neutre = p.Neutre, Defense = p.Defense } : cfg.Echange;
        double[] xProfil = ParametresEchange(profil);
        var (ref1, ref2) = ReferenceService(cfg.Service, cibles);
        double[] reference = [.. ref1.EnTableau(), .. ref2.EnTableau()];
        const int nS = 11;

        ConfigMoteur Appliquer(double[] x, bool arrondir = false)
        {
            var premiere = IssuesService.DepuisTableau(DepuisLogRapports(7, NeutreService, IssuesPremiereLibres, x, 0, arrondir));
            var seconde = IssuesService.DepuisTableau(DepuisLogRapports(7, NeutreService, IssuesSecondeLibres, x, 5, arrondir));
            var echange = cfg.Echange;
            double vitesse = surf.Vitesse;
            if (echangeLibre)
            {
                IssuesEchange Loi(int i) => IssuesEchange.DepuisTableau(DepuisLogRapports(5, Neutre, IssuesEchangeLibres, x, nS + 4 * i, arrondir));
                echange = echange with { Attaque = Loi(0), Neutre = Loi(1), Defense = Loi(2) };
            }
            else vitesse = arrondir ? Math.Round(x[nS], 4) : x[nS];
            return cfg with
            {
                Echange = echange,
                Surfaces = new(cfg.Surfaces) { [cle] = new ConfigSurface(vitesse, premiereRentree, premiere, seconde, fautesRetour) },
            };
        }
        double F(double[] x)
        {
            var c = Appliquer(x);
            var (_, l1, l2) = c.LoisService(surface);
            double[] lois = [.. l1.EnTableau(), .. l2.EnTableau()];
            double r = lois.Select((v, i) => Math.Pow((v - reference[i]) / ecartService, 2)).Sum();
            if (echangeLibre)
                r += rappelEchange * xProfil.Select((v, i) => (x[nS + i] - v) * (x[nS + i] - v)).Sum() + OrdreDesFautes(c.Echange);
            return Ecart(c, cibles, surface, termes) + r;
        }
        double[] x0 = echangeLibre ? [.. ParametresService(ref1, ref2), .. ParametresEchange(cfg.Echange)] : [.. ParametresService(ref1, ref2), surf.Vitesse];
        journal?.Invoke($"départ : écart = {F(x0):F1}");
        for (int essai = 0; essai < 4; essai++)
        {
            x0 = NelderMead(F, x0, essai == 0 ? 0.5 : 0.15, iterations);
            journal?.Invoke($"passe {essai + 1} : écart = {F(x0):F2}");
        }
        return Appliquer(x0, arrondir: true);
    }

    /// <summary>
    /// Référence du duel au service sur la surface : les lois du créateur (moyenne du tour 2023), où aces et services gagnants
    /// sont remplacés par l'efficacité mesurée sur la surface (MCP : aces ; services hors aces non remis en jeu, c'est-à-dire non
    /// retournés, retour en faute provoquée ou retour raté sur une balle retournable). Les autres issues gardent les proportions du créateur.
    /// </summary>
    public static (IssuesService Premiere, IssuesService Seconde) ReferenceService(ConfigService s, CiblesAtp cibles)
    {
        double? Mcp(string m) => cibles.Valeurs.TryGetValue(new("mcp", 0, m), out var c) ? c.Valeur : null;
        static IssuesService Fixer(IssuesService loi, double? ace, double? gagnant)
        {
            var p = loi.EnTableau();
            double fixe = 0, libre = 0;
            for (int k = 0; k < 7; k++)
            {
                bool f = (k == 0 && ace is not null) || (k == 1 && gagnant is not null);
                if (f) fixe += k == 0 ? ace!.Value : gagnant!.Value; else libre += p[k];
            }
            for (int k = 0; k < 7; k++)
                p[k] = k == 0 && ace is not null ? ace.Value : k == 1 && gagnant is not null ? gagnant.Value : p[k] * (1 - fixe) / libre;
            return IssuesService.DepuisTableau(p);
        }
        return (Fixer(s.Premiere, Mcp("premiere_ace"), Mcp("premiere_gagnant")), Fixer(s.Seconde, Mcp("seconde_ace"), Mcp("seconde_gagnant")));
    }

    /// <summary>
    /// Décision du créateur : la faute est la plus probable en défense (on cherche à se sortir d'une situation, sans pouvoir jouer
    /// un coup efficace) et la moins probable en neutre (on prend plus de risques en attaque). Pénalité si l'ordre
    /// défense ≥ attaque ≥ neutre n'est pas respecté (marge 1 point).
    /// </summary>
    private static double OrdreDesFautes(ConfigEchange e)
    {
        const double marge = 0.01, poids = 1e6;
        double a = Math.Max(0, e.Neutre.Faute + marge - e.Attaque.Faute), b = Math.Max(0, e.Attaque.Faute + marge - e.Defense.Faute);
        return poids * (a * a + b * b);
    }

    /// <summary>
    /// Pentes pour qu'un point de stat vaille, sur p(point au service) entre joueurs égaux : partService × pente pour le Service
    /// (et le Retour au retour), (1 − partService) × pente pour l'Échange du serveur.
    /// </summary>
    public static ConfigMoteur CalerPentes(ConfigMoteur cfg, Surface surface, double pente, double partService)
    {
        const double eps = 0.01;
        var unitaire = cfg with { Service = cfg.Service with { Pente = 1 }, Echange = cfg.Echange with { Pente = 1 } };
        // Dérivées de p par unité de x (logit), mesurées autour de l'égalité ; l'écart d'Échange agit sur les deux frappeurs.
        double dS = (Analyser(unitaire, surface, eps).Serveur - Analyser(unitaire, surface, -eps).Serveur) / (2 * eps);
        double dE = (Analyser(unitaire, surface, 0, eps).Serveur - Analyser(unitaire, surface, 0, -eps).Serveur) / (2 * eps);
        // Effets du service par stat (service.stats) : chacun a son effet absolu par point, calé sur les joueurs réels ;
        // la pente du service ne joue plus et l'on ne recalcule que celle de l'échange (calée ensuite par simulation de masse).
        if (cfg.Service.Stats is not null) return cfg;
        return cfg with
        {
            Service = cfg.Service with { Pente = Math.Round(partService * pente / dS, 6) },
            Echange = cfg.Echange with { Pente = Math.Round((1 - partService) * pente / dE, 6) },
        };
    }

    internal static double[] NelderMead(Func<double[], double> f, double[] x0, double pas, int iterations)
    {
        int n = x0.Length;
        var pts = new double[n + 1][];
        var val = new double[n + 1];
        for (int i = 0; i <= n; i++)
        {
            pts[i] = (double[])x0.Clone();
            if (i > 0) pts[i][i - 1] += pas;
            val[i] = f(pts[i]);
        }
        for (int it = 0; it < iterations; it++)
        {
            Array.Sort(val, pts);
            var centre = new double[n];
            for (int i = 0; i < n; i++)
                for (int j = 0; j < n; j++) centre[j] += pts[i][j] / n;
            double[] Vers(double t) => [.. centre.Select((c, j) => c + t * (pts[n][j] - c))];
            var r = Vers(-1); double fr = f(r);
            if (fr < val[0])
            {
                var e = Vers(-2); double fe = f(e);
                (pts[n], val[n]) = fe < fr ? (e, fe) : (r, fr);
            }
            else if (fr < val[n - 1]) (pts[n], val[n]) = (r, fr);
            else
            {
                var c = Vers(fr < val[n] ? -0.5 : 0.5); double fc = f(c);
                if (fc < Math.Min(fr, val[n])) (pts[n], val[n]) = (c, fc);
                else
                    for (int i = 1; i <= n; i++)
                    {
                        pts[i] = [.. pts[i].Select((v, j) => pts[0][j] + 0.5 * (v - pts[0][j]))];
                        val[i] = f(pts[i]);
                    }
            }
        }
        Array.Sort(val, pts);
        return pts[0];
    }
}
