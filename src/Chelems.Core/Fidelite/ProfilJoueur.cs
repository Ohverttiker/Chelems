using Chelems.Core.Configuration;

namespace Chelems.Core.Fidelite;

/// <summary>
/// Roadmap, étape 1 : estimation des stats d'un joueur réel à partir de ses statistiques mesurées (MCP), pour vérifier que le lien
/// stats → efficacité au service reproduit les vrais profils.
/// Taille : celle de l'ATP. Deux temps, face à un adversaire de référence (stats au niveau de l'a priori, Taille 188) :
/// 1. au service : Service, Puissance, Agressivité, Échange et Régularité, sur les 8 grandeurs de <see cref="CaleurStats.Grandeurs"/> ;
/// 2. au retour : Retour et Explosivité (Échange de l'étape 1), sur les grandeurs du serveur adverse (<see cref="EfficaciteJoueur.GrandeursRetour"/>).
/// Chaque grandeur est pondérée par son erreur d'échantillonnage (binomiale sur le nombre de points).
/// A priori normal (maximum a posteriori) [créateur, 2026-11-25] : stats de niveau N(72,8 ; 6,4), Agressivité N(50 ; 20) ;
/// Puissance et Explosivité centrées selon la taille (plus grand : plus puissant, moins explosif), comme dans la population de test.
/// Le MCP suit surtout des matchs entre bons joueurs : les stats estimées sont relatives à cet adversaire moyen.
/// </summary>
public sealed record ProfilJoueur(EfficaciteJoueur Mesure, Joueur Estime, double[] Simule, double Ecart, double[] SimuleRetour, double EcartRetour)
{
    public sealed record APriori(double Moyenne = 72.8, double EcartType = 6.4, double AgressiviteEcartType = 20, double NiveauAdversaire = 72.8,
                                 double TaillePuissance = 0.43, double TailleExplosivite = 0.43, double TailleEcartType = 7.4)
    {
        public static APriori Depuis(ConfigPopulation p) => new(TaillePuissance: p.TaillePuissance, TailleExplosivite: p.TailleExplosivite, TailleEcartType: p.TailleEcartType);
    }

    public static ProfilJoueur Estimer(ConfigMoteur cfg, Surface surface, EfficaciteJoueur j, APriori? apriori = null)
    {
        var ap = apriori ?? new APriori();
        var modele = new ModelePoint(cfg);
        var adversaire = new Joueur("Référence", ap.NiveauAdversaire);
        double taille = double.IsNaN(j.TailleCm) ? 188 : j.TailleCm;
        double moyP = 75 + ap.TaillePuissance * (taille - 188), moyE = 75 - ap.TailleExplosivite * (taille - 188);
        double EtCond(double a) => Math.Sqrt(Math.Max(1, ap.EcartType * ap.EcartType - Math.Pow(a * ap.TailleEcartType, 2)));
        double etP = EtCond(ap.TaillePuissance), etE = EtCond(ap.TailleExplosivite);
        static double Carre(double x) => x * x;
        static double Hors(double[] x) => x.Sum(v => v < 1 || v > 99 ? 1e3 * Carre(v < 1 ? 1 - v : v - 99) : 0); // échelle 1-99

        // 1. Au service. x = Service, Puissance, Agressivité, Échange, Régularité.
        double[] mesure = [j.Premieres, j.Premiere.Ace, j.Premiere.Gagnant, j.GagnesPremiere, j.Seconde.Ace, j.Seconde.Gagnant, j.Seconde.DoubleFaute, j.GagnesSeconde];
        double n1 = j.PointsService * j.Premieres, n2 = j.PointsService * (1 - j.Premieres);
        double[] sigma = Sigmas(mesure, [j.PointsService, n1, n1, n1, n2, n2, n2, n2]);
        Joueur AuService(double[] x) => new(j.Nom, Borne(x[0]), ap.Moyenne, Borne(x[3]), Borne(x[1]), moyE, taille, Borne(x[2]), Borne(x[4]));
        double[] Simuler(double[] x)
        {
            var a = AnalysePoint.Calculer(modele.Preparer(AuService(x), adversaire, surface));
            return [a.PremieresRentrees, a.IssuesService[0][0], a.IssuesService[0][1], a.GagnesPremiere,
                    a.IssuesService[1][0], a.IssuesService[1][1], a.IssuesService[1][6], a.GagnesSeconde];
        }
        double Donnees(double[] x) => Khi2(Simuler(x), mesure, sigma);
        double F(double[] x) => Donnees(x) + Carre((x[0] - ap.Moyenne) / ap.EcartType) + Carre((x[1] - moyP) / etP)
                                + Carre((x[2] - 50) / ap.AgressiviteEcartType) + Carre((x[3] - ap.Moyenne) / ap.EcartType)
                                + Carre((x[4] - ap.Moyenne) / ap.EcartType) + Hors(x);
        // Plusieurs départs : le problème a des optimums locaux (Service et Puissance se partagent les services gagnants).
        double m = ap.Moyenne;
        var x1 = Minimiser(F, [[m, moyP, 50, m, m], [m - 8, moyP + 8, 50, m, m], [m + 8, moyP - 8, 50, m, m], [m, moyP, 25, m + 8, m + 8], [m, moyP, 75, m - 8, m - 8]]);

        // 2. Au retour. y = Retour, Explosivité ; le serveur est l'adversaire de référence. L'Échange reste celui estimé au service :
        // il ne joue pas dans le duel au retour (créateur, 2026-11-26), et le réestimer ici le rendait indiscernable du Retour.
        double[] mesureR = j.AuRetour.Length == 0 ? [] : j.AuRetour;
        double[] simR = [];
        double ecartR = double.NaN;
        double[] y = [ap.Moyenne, moyE];
        if (mesureR.Length > 0)
        {
            double r1 = j.PointsRetour * 0.63, r2 = j.PointsRetour * 0.37; // part des premières adverses : moyenne du tour
            double[] sigmaR = Sigmas(mesureR, [r1, r1, r1, r1, r2, r2, r2, r2, r2]);
            Joueur AuRetour(double[] v) => AuService(x1) with { Retour = Borne(v[0]), Explosivite = Borne(v[1]) };
            double[] SimulerR(double[] v)
            {
                var a = AnalysePoint.Calculer(modele.Preparer(adversaire, AuRetour(v), surface));
                return [a.IssuesService[0][0], a.IssuesService[0][1], a.IssuesService[0][5], a.GagnesPremiere,
                        a.IssuesService[1][0], a.IssuesService[1][1], a.IssuesService[1][5], a.IssuesService[1][6], a.GagnesSeconde];
            }
            double G(double[] v) => Khi2(SimulerR(v), mesureR, sigmaR) + Carre((v[0] - ap.Moyenne) / ap.EcartType) + Carre((v[1] - moyE) / etE) + Hors(v);
            y = Minimiser(G, [[m, moyE], [m + 8, moyE - 8], [m - 8, moyE + 8]]);
            simR = SimulerR(y);
            ecartR = Khi2(simR, mesureR, sigmaR) / mesureR.Count(v => !double.IsNaN(v));
        }
        var estime = AuService(x1) with { Retour = Borne(y[0]), Explosivite = Borne(y[1]) };
        return new ProfilJoueur(j, estime, Simuler(x1), Donnees(x1) / 8, simR, ecartR);
    }

    private static double[] Sigmas(double[] p, double[] n) =>
        [.. p.Select((v, g) => double.IsNaN(v) ? double.NaN : Math.Sqrt(Math.Max(v * (1 - v), 1e-4) / Math.Max(n[g], 1)))];

    private static double Khi2(double[] s, double[] m, double[] sigma)
    {
        double e = 0;
        for (int g = 0; g < m.Length; g++) if (!double.IsNaN(m[g])) e += Math.Pow((s[g] - m[g]) / sigma[g], 2);
        return e;
    }

    private static double[] Minimiser(Func<double[], double> f, double[][] departs)
    {
        double[]? meilleur = null;
        foreach (var depart in departs)
        {
            var x = depart;
            for (int passe = 0; passe < 3; passe++) x = CaleurEchange.NelderMead(f, x, passe == 0 ? 6 : 2, 1200);
            if (meilleur is null || f(x) < f(meilleur)) meilleur = x;
        }
        return meilleur!;
    }

    private static double Borne(double v) => Math.Clamp(v, 1, 99);
}
