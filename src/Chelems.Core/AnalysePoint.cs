namespace Chelems.Core;

/// <summary>
/// Résultats exacts d'un <see cref="DuelPoint"/> (sans simulation) : la chaîne d'états est résolue coup par coup.
/// Sert au calage des lois de base, à la version allégée du simulateur de circuit (décision ouverte n°6)
/// et au contrôle du moteur aléatoire. Tranches de longueur : celles du Match Charting Project (1-3, 4-6, 7-9, 10+).
/// </summary>
public sealed class AnalysePoint
{
    public static readonly string[] NomsTranches = ["1-3", "4-6", "7-9", "10+"];
    public static int Tranche(int coups) => coups <= 3 ? 0 : coups <= 6 ? 1 : coups <= 9 ? 2 : 3;

    /// <summary>Probabilité que le serveur gagne le point.</summary>
    public double Serveur { get; private set; }
    /// <summary>Points gagnés sur première rentrée / premières rentrées.</summary>
    public double GagnesPremiere { get; private set; }
    /// <summary>Points gagnés sur seconde / points joués sur seconde (doubles fautes comprises).</summary>
    public double GagnesSeconde { get; private set; }
    public double PremieresRentrees { get; private set; }
    public double Aces { get; private set; }
    public double DoublesFautes { get; private set; }
    /// <summary>Services gagnants (non retournés, hors aces) / points au service.</summary>
    public double ServicesGagnants { get; private set; }
    /// <summary>Issues du duel au service (ordre de <see cref="IssueService"/>) : [0 première rentrée, 1 seconde].</summary>
    public double[][] IssuesService { get; private set; } = [];
    /// <summary>Fins hors service (aces, services gagnants, doubles fautes exclus) : [0 gagnants, 1 provoquées, 2 directes] / points concernés.</summary>
    public double[] FinsHorsService { get; } = new double[3];
    /// <summary>Part des points par tranche de longueur.</summary>
    public double[] Part { get; } = new double[4];
    /// <summary>Points gagnés par le serveur dans chaque tranche / points de la tranche.</summary>
    public double[] ServeurParTranche { get; } = new double[4];
    /// <summary>[tranche (0-3, 4 = total)][0 gagnants aces compris, 1 fautes provoquées services non retournés compris, 2 fautes directes] / points de la tranche.</summary>
    public double[][] Fins { get; } = [new double[3], new double[3], new double[3], new double[3], new double[3]];
    /// <summary>Conversion% : points gagnés par le joueur en attaque au premier déséquilibre (Steal% = 1 − Conversion%).</summary>
    public double Conversion { get; private set; }
    public double ConversionServeur { get; private set; }
    public double ConversionRelanceur { get; private set; }
    /// <summary>Part des points qui connaissent un déséquilibre (dénominateur de Conversion%).</summary>
    public double PartDesequilibre { get; private set; }
    /// <summary>V[h][état] : probabilité que le frappeur h (0 serveur, 1 relanceur) gagne le point depuis cet état.</summary>
    public double[][] Valeur { get; } = [new double[3], new double[3]];
    public double LongueurMoyenne { get; private set; }

    public static AnalysePoint Calculer(DuelPoint d)
    {
        var a = new AnalysePoint();
        a.ResoudreValeurs(d);
        double pr = d.PremiereRentree;
        double g1 = a.Service(d, d.Premiere, pr, d.FautesRetour[0]);
        double g2 = a.Service(d, d.Seconde, 1 - pr, d.FautesRetour[1]);
        a.PremieresRentrees = pr;
        a.GagnesPremiere = g1 / pr;
        a.GagnesSeconde = g2 / (1 - pr);
        a.Serveur = g1 + g2;
        a.Aces = pr * d.Premiere[(int)IssueService.Ace] + (1 - pr) * d.Seconde[(int)IssueService.Ace];
        a.DoublesFautes = (1 - pr) * d.Seconde[(int)IssueService.DoubleFaute];
        a.ServicesGagnants = pr * d.Premiere[(int)IssueService.Gagnant] + (1 - pr) * d.Seconde[(int)IssueService.Gagnant];
        double fautesRetour = pr * d.Premiere[(int)IssueService.Gagnant] * d.FautesRetour[0]
                              + (1 - pr) * d.Seconde[(int)IssueService.Gagnant] * d.FautesRetour[1];
        a.IssuesService = [(double[])d.Premiere.Clone(), (double[])d.Seconde.Clone()];
        for (int t = 0; t < 4; t++)
        {
            for (int k = 0; k < 3; k++) a.Fins[4][k] += a.Fins[t][k];
            if (a.Part[t] > 0)
            {
                a.ServeurParTranche[t] /= a.Part[t];
                for (int k = 0; k < 3; k++) a.Fins[t][k] /= a.Part[t];
            }
        }
        double horsService = 1 - a.Aces - a.ServicesGagnants - a.DoublesFautes;
        a.FinsHorsService[0] = (a.Fins[4][0] - a.Aces) / horsService;
        a.FinsHorsService[1] = (a.Fins[4][1] - a.ServicesGagnants + fautesRetour) / horsService;
        a.FinsHorsService[2] = (a.Fins[4][2] - fautesRetour) / horsService;
        a.Desequilibres(d);
        return a;
    }

    /// <summary>V[h][s] = gagnant + Σ_t p_t × (1 − V[1−h][t]) : point fixe (contractant, chaque coup a une chance de finir le point).</summary>
    private void ResoudreValeurs(DuelPoint d)
    {
        for (int iter = 0; iter < 100_000; iter++)
        {
            double ecart = 0;
            for (int h = 0; h < 2; h++)
                for (int s = 0; s < 3; s++)
                {
                    var q = d.Echange[h][s];
                    double v = q[(int)IssueEchange.Gagnant]
                               + q[(int)IssueEchange.AdvDefense] * (1 - Valeur[1 - h][(int)EtatEchange.Defense])
                               + q[(int)IssueEchange.Neutre] * (1 - Valeur[1 - h][(int)EtatEchange.Neutre])
                               + q[(int)IssueEchange.AdvAttaque] * (1 - Valeur[1 - h][(int)EtatEchange.Attaque]);
                    ecart = Math.Max(ecart, Math.Abs(v - Valeur[h][s]));
                    Valeur[h][s] = v;
                }
            if (ecart < 1e-14) break;
        }
    }

    private void Ajouter(int coups, double masse, bool serveurGagne, int typeFin)
    {
        int t = Tranche(coups);
        Part[t] += masse;
        if (serveurGagne) ServeurParTranche[t] += masse;
        if (typeFin >= 0) Fins[t][typeFin] += masse;
        LongueurMoyenne += masse * coups;
    }

    /// <summary>Propage une balle de service (poids w) coup par coup ; renvoie la probabilité (pondérée) que le serveur gagne.</summary>
    private double Service(DuelPoint d, double[] issues, double w, double fauteRetour)
    {
        double gagne = 0;
        void Fin(int coups, double m, bool serveur, int type) { Ajouter(coups, m, serveur, type); if (serveur) gagne += m; }

        Fin(1, w * issues[(int)IssueService.Ace], true, 0);
        Fin(1, w * issues[(int)IssueService.Gagnant] * (1 - fauteRetour), true, 1);
        Fin(1, w * issues[(int)IssueService.Gagnant] * fauteRetour, true, 2); // retour raté : faute directe (comptage MCP)
        Fin(2, w * issues[(int)IssueService.RetourGagnant], false, 0);
        Fin(0, w * issues[(int)IssueService.DoubleFaute], false, -1);

        // masse[h][état] : probabilité que le joueur h s'apprête à frapper le coup n dans cet état.
        double[][] masse = [new double[3], new double[3]];
        masse[0][(int)EtatEchange.Attaque] = w * issues[(int)IssueService.Attaque];
        masse[0][(int)EtatEchange.Neutre] = w * issues[(int)IssueService.Neutre];
        masse[0][(int)EtatEchange.Defense] = w * issues[(int)IssueService.Defense];
        for (int n = 3; n < 2000; n++)
        {
            double[][] suite = [new double[3], new double[3]];
            double reste = 0;
            for (int h = 0; h < 2; h++)
                for (int s = 0; s < 3; s++)
                {
                    double m = masse[h][s];
                    if (m == 0) continue;
                    var q = d.Echange[h][s];
                    Fin(n, m * q[(int)IssueEchange.Gagnant], h == 0, 0);
                    Fin(n - 1, m * q[(int)IssueEchange.Faute], h == 1, s == (int)EtatEchange.Defense ? 1 : 2);
                    suite[1 - h][(int)EtatEchange.Defense] += m * q[(int)IssueEchange.AdvDefense];
                    suite[1 - h][(int)EtatEchange.Neutre] += m * q[(int)IssueEchange.Neutre];
                    suite[1 - h][(int)EtatEchange.Attaque] += m * q[(int)IssueEchange.AdvAttaque];
                }
            masse = suite;
            for (int h = 0; h < 2; h++) reste += masse[h].Sum();
            if (reste < 1e-15) break;
        }
        return gagne;
    }

    /// <summary>Premier déséquilibre : depuis le service, puis tant que l'échange reste neutre.</summary>
    private void Desequilibres(DuelPoint d)
    {
        double total = 0, gagnes = 0;
        double[] totalRole = new double[2], gagnesRole = new double[2];
        void Compter(int attaquant, double m, double pGagne)
        {
            total += m; gagnes += m * pGagne;
            totalRole[attaquant] += m; gagnesRole[attaquant] += m * pGagne;
        }

        double[] neutre = new double[2];
        foreach (var (issues, w) in new[] { (d.Premiere, d.PremiereRentree), (d.Seconde, 1 - d.PremiereRentree) })
        {
            Compter(0, w * issues[(int)IssueService.Attaque], Valeur[0][(int)EtatEchange.Attaque]);
            Compter(1, w * issues[(int)IssueService.Defense], 1 - Valeur[0][(int)EtatEchange.Defense]);
            neutre[0] += w * issues[(int)IssueService.Neutre];
        }
        for (int n = 0; n < 2000 && neutre[0] + neutre[1] > 1e-15; n++)
        {
            double[] suite = new double[2];
            for (int h = 0; h < 2; h++)
            {
                var q = d.Echange[h][(int)EtatEchange.Neutre];
                Compter(h, neutre[h] * q[(int)IssueEchange.AdvDefense], 1 - Valeur[1 - h][(int)EtatEchange.Defense]);
                Compter(1 - h, neutre[h] * q[(int)IssueEchange.AdvAttaque], Valeur[1 - h][(int)EtatEchange.Attaque]);
                suite[1 - h] += neutre[h] * q[(int)IssueEchange.Neutre];
            }
            neutre = suite;
        }
        PartDesequilibre = total;
        Conversion = gagnes / total;
        ConversionServeur = gagnesRole[0] / totalRole[0];
        ConversionRelanceur = gagnesRole[1] / totalRole[1];
    }
}
