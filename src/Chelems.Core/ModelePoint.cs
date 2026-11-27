using Chelems.Core.Configuration;

namespace Chelems.Core;

/// <summary>État du joueur qui va frapper dans l'échange.</summary>
public enum EtatEchange { Attaque, Neutre, Defense }

/// <summary>Issues du duel au service (même ordre que <see cref="IssuesService.EnTableau"/>).</summary>
public enum IssueService { Ace, Gagnant, Attaque, Neutre, Defense, RetourGagnant, DoubleFaute }

/// <summary>Issues d'un coup de l'échange (même ordre que <see cref="IssuesEchange.EnTableau"/>).</summary>
public enum IssueEchange { Gagnant, AdvDefense, Neutre, AdvAttaque, Faute }

/// <summary>
/// Fin d'un point. ServiceGagnant : service non retourné (hors ace), compté comme faute provoquée du relanceur.
/// FauteDirecte : faute en attaque ou en position neutre. FauteProvoquee : faute en défense.
/// </summary>
/// <summary>
/// FauteRetour : service gagnant dont le retour, sur une balle retournable, est une faute directe (comptage du Match Charting Project :
/// faute directe du relanceur, pas faute provoquée). Même issue du duel au service que <see cref="ServiceGagnant"/>.
/// </summary>
public enum FinPoint { Ace, DoubleFaute, ServiceGagnant, RetourGagnant, Gagnant, FauteDirecte, FauteProvoquee, FauteRetour }

/// <summary>
/// Un point joué. Auteur : 0 = serveur, 1 = relanceur, joueur qui a frappé le dernier coup (gagnant) ou fait la faute.
/// Coups : longueur de l'échange, convention du Match Charting Project (service compris, faute finale exclue).
/// Attaquant : joueur en attaque au premier déséquilibre de l'échange (0 serveur, 1 relanceur, −1 aucun), pour Conversion% et Steal%.
/// EtatApresService : état du serveur pour son coup suivant quand le service est remis en jeu (null sinon).
/// </summary>
public readonly record struct DeroulementPoint(bool ServeurGagne, bool PremiereRentree, FinPoint Fin, int Auteur, int Coups, int Attaquant,
                                             EtatEchange? EtatApresService = null);

/// <summary>
/// Lois précalculées d'un serveur face à un relanceur sur une surface (fixes pendant tout le match en V0.3).
/// Echange[h][état] : issues du coup du joueur h (0 serveur, 1 relanceur) quand il frappe dans cet état.
/// </summary>
public sealed class DuelPoint
{
    public required double PremiereRentree { get; init; }
    public required double[] Premiere { get; init; }
    public required double[] Seconde { get; init; }
    public required double[][][] Echange { get; init; }
    /// <summary>Part des services gagnants comptés comme fautes directes de retour : [0 première, 1 seconde]. Comptage seulement.</summary>
    public double[] FautesRetour { get; init; } = [0, 0];
}

/// <summary>
/// Le seul moteur de point (principe 4), en deux phases :
/// 1. duel au service (Service du serveur contre Retour du relanceur) : ace, service gagnant, retour gagnant, double faute,
///    ou état du serveur pour son coup suivant (attaque, neutre, défense) ;
/// 2. échange (Échange contre Échange) : à chaque coup, selon l'état du frappeur, coup gagnant, faute,
///    ou état de l'adversaire pour le coup suivant. Boucle jusqu'à la fin du point.
/// Chaque loi est celle de deux joueurs égaux, décalée par l'écart de stats : p_k ∝ base_k × exp(x × orientation_k),
/// avec x = pente × écart (+ vitesse de la surface dans l'échange). L'orientation donne la valeur de chaque issue pour le joueur avantagé.
/// La surface agit sur le service par ses propres lois de base (<see cref="ConfigMoteur.LoisService"/>), sur l'échange par sa vitesse.
/// </summary>
public sealed class ModelePoint
{
    private const int CoupsMax = 1000; // garde-fou : un échange de 1000 coups n'arrive jamais

    public ConfigMoteur Config { get; }

    public ModelePoint(ConfigMoteur config)
    {
        config.Valider();
        Config = config;
    }

    /// <summary>Loi décalée : p_k ∝ base_k × exp(x × orientation_k).</summary>
    public static double[] Decaler(double[] loi, double[] orientation, double x)
    {
        var p = new double[loi.Length];
        double somme = 0;
        for (int k = 0; k < loi.Length; k++) somme += p[k] = loi[k] * Math.Exp(x * orientation[k]);
        for (int k = 0; k < loi.Length; k++) p[k] /= somme;
        return p;
    }

    /// <summary>Loi décalée par un vecteur : p_k ∝ base_k × exp(x_k).</summary>
    public static double[] Decaler(double[] loi, double[] x)
    {
        var p = new double[loi.Length];
        double somme = 0;
        for (int k = 0; k < loi.Length; k++) somme += p[k] = loi[k] * Math.Exp(x[k]);
        for (int k = 0; k < loi.Length; k++) p[k] /= somme;
        return p;
    }

    /// <summary>
    /// Décalages du duel au service (en logit) : par issue de la première et de la seconde, et de la part de premières rentrées.
    /// Sans <see cref="ConfigService.Stats"/> : pente × (Service − Retour) × orientation (moteur V0.3).
    /// </summary>
    public (double[] Premiere, double[] Seconde, double Rentree) DecalagesService(Joueur serveur, Joueur relanceur)
    {
        var s = Config.Service;
        double[] x1 = new double[7], x2 = new double[7];
        double rentree = 0;
        if (s.Stats is null)
        {
            double x = s.Pente * (serveur.Service - relanceur.Retour);
            var o = s.Orientation.EnTableau();
            for (int k = 0; k < 7; k++) x1[k] = x2[k] = x * o[k];
            return (x1, x2, 0);
        }
        foreach (var (cle, effet) in s.Stats)
        {
            double ecart = cle switch
            {
                "service" => serveur.Service - effet.Reference,
                "puissance" => serveur.Puissance - effet.Reference,
                "agressivite" => serveur.Agressivite - effet.Reference,
                "regularite" => serveur.Regularite - effet.Reference,
                "retour" => -(relanceur.Retour - effet.Reference),
                "explosivite" => -(relanceur.Explosivite - effet.Reference),
                "echange_relanceur" => -(relanceur.Echange - effet.Reference),
                "taille" => serveur.TailleCm - effet.Reference,
                "taille_relanceur" => relanceur.TailleCm - effet.Reference,
                _ => 0,
            };
            double c = (effet.Part is { } part ? part * s.Pente : 1) * effet.Echelle * ecart;
            if (c == 0) continue;
            double[] o1 = effet.Premiere.EnTableau(), o2 = effet.Seconde.EnTableau();
            for (int k = 0; k < 7; k++) { x1[k] += c * o1[k]; x2[k] += c * o2[k]; }
            rentree += c * effet.Rentree;
        }
        return (x1, x2, rentree);
    }

    public DuelPoint Preparer(Joueur serveur, Joueur relanceur, Surface surface)
    {
        var s = Config.Service;
        var e = Config.Echange;
        var (premiereRentree, premiere, seconde) = Config.LoisService(surface);
        double v = Config.Surface(surface).Vitesse;
        var (x1, x2, xRentree) = DecalagesService(serveur, relanceur);
        double[] oE = e.Orientation.EnTableau();
        double[][] Joueur(double ecart) =>
        [
            Decaler(e.Attaque.EnTableau(), oE, e.Pente * ecart + v),
            Decaler(e.Neutre.EnTableau(), oE, e.Pente * ecart),
            Decaler(e.Defense.EnTableau(), oE, e.Pente * ecart - v),
        ];
        double d = serveur.Echange - relanceur.Echange;
        return new DuelPoint
        {
            PremiereRentree = xRentree == 0 ? premiereRentree : 1 / (1 + Math.Exp(-(Math.Log(premiereRentree / (1 - premiereRentree)) + xRentree))),
            Premiere = Decaler(premiere.EnTableau(), x1),
            Seconde = Decaler(seconde.EnTableau(), x2),
            Echange = [Joueur(d), Joueur(-d)],
            FautesRetour = Config.Surface(surface).FautesRetour is { } f ? [f.Premiere, f.Seconde] : [0, 0],
        };
    }

    public static DeroulementPoint Jouer(DuelPoint duel, Random rng)
    {
        bool premiere = rng.NextDouble() < duel.PremiereRentree;
        var issue = (IssueService)Tirer(premiere ? duel.Premiere : duel.Seconde, rng);
        switch (issue)
        {
            case IssueService.Ace: return new(true, premiere, FinPoint.Ace, 0, 1, -1);
            case IssueService.Gagnant:
                // Le tirage du comptage n'a lieu que si la part est non nulle (même suite aléatoire qu'avant sinon).
                double f = duel.FautesRetour[premiere ? 0 : 1];
                return new(true, premiere, f > 0 && rng.NextDouble() < f ? FinPoint.FauteRetour : FinPoint.ServiceGagnant, 1, 1, -1);
            case IssueService.RetourGagnant: return new(false, premiere, FinPoint.RetourGagnant, 1, 2, -1);
            case IssueService.DoubleFaute: return new(false, premiere, FinPoint.DoubleFaute, 0, 0, -1);
        }

        var etat = issue switch { IssueService.Attaque => EtatEchange.Attaque, IssueService.Neutre => EtatEchange.Neutre, _ => EtatEchange.Defense };
        var initial = etat;
        int frappeur = 0, coups = 2;
        int attaquant = etat == EtatEchange.Attaque ? 0 : etat == EtatEchange.Defense ? 1 : -1;
        while (coups < CoupsMax)
        {
            int n = coups + 1;
            var loi = duel.Echange[frappeur][(int)etat];
            var coup = (IssueEchange)Tirer(loi, rng);
            if (coup == IssueEchange.Gagnant)
                return new(frappeur == 0, premiere, FinPoint.Gagnant, frappeur, n, attaquant, initial);
            if (coup == IssueEchange.Faute)
                return new(frappeur == 1, premiere, etat == EtatEchange.Defense ? FinPoint.FauteProvoquee : FinPoint.FauteDirecte,
                           frappeur, n - 1, attaquant, initial);
            var suivant = coup switch { IssueEchange.AdvDefense => EtatEchange.Defense, IssueEchange.Neutre => EtatEchange.Neutre, _ => EtatEchange.Attaque };
            if (attaquant < 0 && suivant != EtatEchange.Neutre)
                attaquant = suivant == EtatEchange.Attaque ? 1 - frappeur : frappeur;
            frappeur = 1 - frappeur;
            etat = suivant;
            coups = n;
        }
        throw new InvalidOperationException("Échange sans fin : vérifier les probabilités de coup gagnant et de faute.");
    }

    private static int Tirer(double[] p, Random rng)
    {
        double u = rng.NextDouble();
        int dernier = 0;
        for (int k = 0; k < p.Length; k++)
        {
            if (p[k] <= 0) continue;
            dernier = k;
            u -= p[k];
            if (u < 0) return k;
        }
        return dernier; // arrondi : jamais une issue de probabilité nulle (pas de double faute sur une première)
    }
}
