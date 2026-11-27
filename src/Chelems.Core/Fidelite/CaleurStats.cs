using Chelems.Core.Configuration;

namespace Chelems.Core.Fidelite;

/// <summary>
/// Roadmap, étape 1 : calage des effets de la Taille et de l'Agressivité sur le duel au service, d'après les pentes mesurées
/// entre joueurs réels (<c>efficacite_joueurs.csv</c>, Match Charting Project).
/// Mesures : régression pondérée (points au service) des statistiques de service des joueurs sur leur taille, et sur leur part
/// de premières balles à taille égale. Une stat absolue (par cm, par point d'Agressivité) a 12 paramètres : décalage de la part
/// de premières, et décalage en logit de chaque issue sauf le neutre (référence) sur première et sur seconde. Ils sont obtenus par
/// moindres carrés sur les 8 pentes mesurées (avec un léger rappel vers 0 : les issues non mesurées ne bougent que si c'est utile).
/// </summary>
public static class CaleurStats
{
    /// <summary>Grandeurs mesurées, en proportion : premières, aces et services gagnants des deux balles, doubles fautes, points gagnés.</summary>
    public static readonly string[] Grandeurs = ["premieres", "p1_ace", "p1_gagnant", "p1_gagnes", "p2_ace", "p2_gagnant", "p2_double_faute", "p2_gagnes"];

    private static double Lire(AnalysePoint a, int g) => g switch
    {
        0 => a.PremieresRentrees,
        1 => a.IssuesService[0][(int)IssueService.Ace],
        2 => a.IssuesService[0][(int)IssueService.Gagnant],
        3 => a.GagnesPremiere,
        4 => a.IssuesService[1][(int)IssueService.Ace],
        5 => a.IssuesService[1][(int)IssueService.Gagnant],
        6 => a.IssuesService[1][(int)IssueService.DoubleFaute],
        _ => a.GagnesSeconde,
    };

    /// <summary>Pentes mesurées (en proportion par unité) : par cm de taille, et par point de premières balles à taille égale.</summary>
    /// <param name="ParCmRelanceur">Pentes des grandeurs du serveur adverse par cm de taille du relanceur (premières : non mesurées, NaN).</param>
    public sealed record Pentes(double[] ParCm, double[] ParPointDePremieres, int Joueurs, double[] ParCmRelanceur);

    public static Pentes Mesurer(IReadOnlyList<EfficaciteJoueur> joueurs)
    {
        var j = joueurs.Where(x => !double.IsNaN(x.TailleCm)).ToList();
        double[] w = [.. j.Select(x => x.PointsService)];
        double[] T = [.. j.Select(x => x.TailleCm)], P = [.. j.Select(x => x.Premieres)];
        double[] Y(int g) => [.. j.Select(x => g switch
        {
            0 => x.Premieres, 1 => x.Premiere.Ace, 2 => x.Premiere.Gagnant, 3 => x.GagnesPremiere,
            4 => x.Seconde.Ace, 5 => x.Seconde.Gagnant, 6 => x.Seconde.DoubleFaute, _ => x.GagnesSeconde,
        })];
        var parCm = new double[8];
        var parPremiere = new double[8];
        for (int g = 0; g < 8; g++)
        {
            parCm[g] = Regression(Y(g), [T], w)[1];                     // effet total de la taille (premières comprises)
            parPremiere[g] = g == 0 ? 1 : Regression(Y(g), [T, P], w)[2]; // à taille égale
        }
        // Au retour : grandeurs du serveur adverse face au joueur, en fonction de la taille du relanceur (pondération : points au retour).
        int[] colonne = [-1, 0, 1, 3, 4, 5, 7, 8]; // GrandeursRetour pour chaque grandeur de Grandeurs (premières : absentes)
        var parCmRelanceur = new double[8];
        for (int g = 0; g < 8; g++)
        {
            if (colonne[g] < 0) { parCmRelanceur[g] = double.NaN; continue; }
            var ok = j.Where(x => x.AuRetour.Length > 0 && !double.IsNaN(x.AuRetour[colonne[g]])).ToList();
            parCmRelanceur[g] = Regression([.. ok.Select(x => x.AuRetour[colonne[g]])], [[.. ok.Select(x => x.TailleCm)]], [.. ok.Select(x => x.PointsRetour)])[1];
        }
        return new(parCm, parPremiere, j.Count, parCmRelanceur);
    }

    /// <summary>
    /// Effet d'une stat absolue qui retrouve les pentes cibles (en proportion par unité de la stat), autour de deux joueurs égaux
    /// de référence. Cibles NaN : non contraintes. Rappel : poids du rappel vers 0 des 12 paramètres.
    /// </summary>
    public static EffetStat Caler(ConfigMoteur cfg, Surface surface, double reference, double[] cibles, double rappel = 1e-4)
    {
        var modele = new ModelePoint(cfg with { Service = cfg.Service with { Stats = null } });
        var duel = modele.Preparer(new Joueur("S", 75), new Joueur("R", 75), surface);
        // Paramètres : 0 premières ; 1-5 première (ace, gagnant, attaque, défense, retour gagnant) ; 6-11 seconde (idem + double faute).
        int[] issues1 = [0, 1, 2, 4, 5], issues2 = [0, 1, 2, 4, 5, 6];
        double[] Grandeurs(double[] theta)
        {
            double[] x1 = new double[7], x2 = new double[7];
            for (int i = 0; i < 5; i++) x1[issues1[i]] = theta[1 + i];
            for (int i = 0; i < 6; i++) x2[issues2[i]] = theta[6 + i];
            double pr = duel.PremiereRentree, lr = Math.Log(pr / (1 - pr)) + theta[0];
            var d = new DuelPoint
            {
                PremiereRentree = 1 / (1 + Math.Exp(-lr)),
                Premiere = ModelePoint.Decaler(duel.Premiere, x1),
                Seconde = ModelePoint.Decaler(duel.Seconde, x2),
                Echange = duel.Echange,
                FautesRetour = duel.FautesRetour,
            };
            var a = AnalysePoint.Calculer(d);
            return [.. Enumerable.Range(0, 8).Select(g => Lire(a, g))];
        }
        // Jacobien des grandeurs par rapport aux paramètres, en 0 (les effets par unité sont petits : régime linéaire).
        const double h = 1e-4;
        var J = new double[8, 12];
        for (int p = 0; p < 12; p++)
        {
            var plus = new double[12]; plus[p] = h;
            var moins = new double[12]; moins[p] = -h;
            double[] gp = Grandeurs(plus), gm = Grandeurs(moins);
            for (int g = 0; g < 8; g++) J[g, p] = (gp[g] - gm[g]) / (2 * h);
        }
        // Moindres carrés avec rappel : (JᵀJ + λI) θ = Jᵀ t, sur les seules cibles données.
        var A = new double[12, 12];
        var b = new double[12];
        double echelle = 0;
        for (int g = 0; g < 8; g++) for (int p = 0; p < 12; p++) echelle = Math.Max(echelle, J[g, p] * J[g, p]);
        for (int g = 0; g < 8; g++)
        {
            if (double.IsNaN(cibles[g])) continue;
            for (int p = 0; p < 12; p++)
            {
                b[p] += J[g, p] * cibles[g];
                for (int q = 0; q < 12; q++) A[p, q] += J[g, p] * J[g, q];
            }
        }
        for (int p = 0; p < 12; p++) A[p, p] += rappel * echelle;
        var theta = Resoudre(A, b);
        double[] P1 = new double[7], P2 = new double[7];
        for (int i = 0; i < 5; i++) P1[issues1[i]] = Math.Round(theta[1 + i], 6);
        for (int i = 0; i < 6; i++) P2[issues2[i]] = Math.Round(theta[6 + i], 6);
        return new EffetStat(reference, IssuesService.DepuisTableau(P1), IssuesService.DepuisTableau(P2), Math.Round(theta[0], 6));
    }

    /// <summary>Pentes produites par le moteur pour une stat (en proportion par unité), mesurées de part et d'autre de la référence.</summary>
    public static double[] PentesDuMoteur(ConfigMoteur cfg, Surface surface, Func<Joueur, double, Joueur> modifier, double pas)
    {
        var m = new ModelePoint(cfg);
        AnalysePoint A(double d) => AnalysePoint.Calculer(m.Preparer(modifier(new Joueur("S", 75), d), new Joueur("R", 75), surface));
        AnalysePoint plus = A(pas), moins = A(-pas);
        return [.. Enumerable.Range(0, 8).Select(g => (Lire(plus, g) - Lire(moins, g)) / (2 * pas))];
    }

    /// <summary>Idem, en modifiant le relanceur : pentes des grandeurs du serveur par unité de la stat du relanceur.</summary>
    public static double[] PentesDuMoteurAuRetour(ConfigMoteur cfg, Surface surface, Func<Joueur, double, Joueur> modifier, double pas)
    {
        var m = new ModelePoint(cfg);
        AnalysePoint A(double d) => AnalysePoint.Calculer(m.Preparer(new Joueur("S", 75), modifier(new Joueur("R", 75), d), surface));
        AnalysePoint plus = A(pas), moins = A(-pas);
        return [.. Enumerable.Range(0, 8).Select(g => (Lire(plus, g) - Lire(moins, g)) / (2 * pas))];
    }

    private static double[] Regression(double[] y, double[][] x, double[] w)
    {
        int k = x.Length + 1;
        var A = new double[k, k];
        var b = new double[k];
        for (int i = 0; i < y.Length; i++)
        {
            double[] v = [1, .. x.Select(c => c[i])];
            for (int p = 0; p < k; p++)
            {
                b[p] += w[i] * v[p] * y[i];
                for (int q = 0; q < k; q++) A[p, q] += w[i] * v[p] * v[q];
            }
        }
        return Resoudre(A, b);
    }

    /// <summary>Système linéaire carré, élimination de Gauss avec pivot partiel.</summary>
    private static double[] Resoudre(double[,] A, double[] b)
    {
        int n = b.Length;
        var M = (double[,])A.Clone();
        var v = (double[])b.Clone();
        for (int c = 0; c < n; c++)
        {
            int piv = c;
            for (int r = c + 1; r < n; r++) if (Math.Abs(M[r, c]) > Math.Abs(M[piv, c])) piv = r;
            for (int q = 0; q < n; q++) (M[c, q], M[piv, q]) = (M[piv, q], M[c, q]);
            (v[c], v[piv]) = (v[piv], v[c]);
            for (int r = 0; r < n; r++)
            {
                if (r == c) continue;
                double f = M[r, c] / M[c, c];
                for (int q = c; q < n; q++) M[r, q] -= f * M[c, q];
                v[r] -= f * v[c];
            }
        }
        return [.. Enumerable.Range(0, n).Select(i => v[i] / M[i, i])];
    }
}
