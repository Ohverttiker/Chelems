using Chelems.Core.Configuration;

namespace Chelems.Core.Fidelite;

/// <summary>
/// Efficacité au service d'un joueur réel (roadmap, étape 1), d'après <c>efficacite_joueurs.csv</c> (Match Charting Project).
/// Aces, services gagnants, retours gagnants et doubles fautes sont mesurés. Le partage entre attaque, neutre et défense ne l'est pas :
/// il est déduit des points gagnés (idée du créateur), en deux temps, avec V(s) = valeur de l'état s pour le serveur (moteur V0.3) :
/// 1. la seconde balle, surtout neutre, garde le partage de référence et donne le niveau d'échange du joueur : l'écart d'Échange
///    (en points de stat, face à un adversaire moyen) qui retrouve ses points gagnés sur seconde ;
/// 2. à ce niveau d'échange, la première donne le partage du service : la référence est décalée
///    (p_s ∝ référence_s × exp(x × o_s), o = +1 attaque, 0 neutre, −1 défense) jusqu'à retrouver ses points gagnés sur première.
/// Sans l'étape 1, tout l'écart de points sur première irait au service, alors qu'une part vient du niveau dans l'échange.
/// </summary>
public sealed record EfficaciteJoueur(string Nom, double TailleCm, string Main, double PointsService, double Premieres,
                                      IssuesService Premiere, IssuesService Seconde, double EcartEchange, double DecalagePremiere,
                                      double GagnesPremiere, double GagnesSeconde)
{
    /// <summary>Noms des grandeurs au retour : issues du service adverse (vues du serveur) face à ce joueur.</summary>
    public static readonly string[] GrandeursRetour = ["r1_ace", "r1_gagnant", "r1_retour_gagnant", "r1_gagnes", "r2_ace", "r2_gagnant", "r2_retour_gagnant", "r2_double_faute", "r2_gagnes"];

    /// <summary>Au retour, en proportion, dans l'ordre de <see cref="GrandeursRetour"/> (points gagnés = par le serveur adverse).</summary>
    public double[] AuRetour { get; init; } = [];
    public double PointsRetour { get; init; }

    public static List<EfficaciteJoueur> Charger(string fichier, ConfigMoteur cfg, Surface surface)
    {
        var (_, ref1, ref2) = cfg.LoisService(surface);
        var modele = new ModelePoint(cfg);
        double[] V(double ecart) =>
            AnalysePoint.Calculer(modele.Preparer(new Joueur("S", 75, 75, 75 + ecart / 2), new Joueur("R", 75, 75, 75 - ecart / 2), surface)).Valeur[0];
        var liste = new List<EfficaciteJoueur>();
        foreach (var l in Csv.Lire(fichier))
        {
            double P(string c) => l.Reel(c) / 100;
            // 1. Niveau d'échange : dichotomie sur l'écart d'Échange (−60 à +60) pour les points gagnés sur seconde, partage de référence.
            double ace2 = P("p2_ace"), gag2 = P("p2_gagnant"), rg2 = P("p2_retour_gagnant"), df2 = P("p2_double_faute");
            double reste2 = 1 - ace2 - gag2 - rg2 - df2, s2 = ref2.Attaque + ref2.Neutre + ref2.Defense;
            double Seconde(double e) { var v = V(e); return ace2 + gag2 + reste2 * (ref2.Attaque * v[0] + ref2.Neutre * v[1] + ref2.Defense * v[2]) / s2; }
            double lo = -60, hi = 60;
            for (int i = 0; i < 40; i++) { double m = (lo + hi) / 2; if (Seconde(m) < P("p2_gagnes")) lo = m; else hi = m; }
            double ecart = (lo + hi) / 2;
            var p2 = new IssuesService(ace2, gag2, reste2 * ref2.Attaque / s2, reste2 * ref2.Neutre / s2, reste2 * ref2.Defense / s2, rg2, df2);
            // 2. Partage du service sur première, à ce niveau d'échange.
            var (p1, x1) = Deduire(ref1, V(ecart), P("p1_ace"), P("p1_gagnant"), P("p1_retour_gagnant"), 0, P("p1_gagnes"));
            double taille = double.TryParse(l["taille_cm"], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var t) ? t : double.NaN;
            liste.Add(new(l["joueur"], taille, l["main"], l.Reel("points_service"), P("premieres"), p1, p2, ecart, x1, P("p1_gagnes"), P("p2_gagnes"))
            {
                AuRetour = [.. GrandeursRetour.Select(P)],
                PointsRetour = l.Reel("points_retour"),
            });
        }
        return liste;
    }

    /// <summary>Loi complète d'une balle : issues mesurées, puis partage attaque / neutre / défense qui retrouve les points gagnés.</summary>
    public static (IssuesService Loi, double Decalage) Deduire(IssuesService reference, double[] valeurServeur,
                                                               double ace, double gagnant, double retourGagnant, double doubleFaute, double gagnes)
    {
        double reste = 1 - ace - gagnant - retourGagnant - doubleFaute;
        double[] b = [reference.Attaque, reference.Neutre, reference.Defense];
        double[] o = [1, 0, -1];
        double[] Partage(double x)
        {
            var p = b.Select((bi, i) => bi * Math.Exp(x * o[i])).ToArray();
            double s = p.Sum();
            return [.. p.Select(pi => reste * pi / s)];
        }
        double Gagnes(double x)
        {
            var p = Partage(x);
            return ace + gagnant + p[0] * valeurServeur[0] + p[1] * valeurServeur[1] + p[2] * valeurServeur[2];
        }
        // Gagnes(x) croît avec x : dichotomie sur [−6 ; 6] (bornée si les points gagnés sont hors d'atteinte).
        double lo = -6, hi = 6;
        for (int i = 0; i < 100; i++)
        {
            double m = (lo + hi) / 2;
            if (Gagnes(m) < gagnes) lo = m; else hi = m;
        }
        double xs = (lo + hi) / 2;
        var q = Partage(xs);
        return (new IssuesService(ace, gagnant, q[0], q[1], q[2], retourGagnant, doubleFaute), xs);
    }
}
