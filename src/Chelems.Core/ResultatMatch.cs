using System.Text;

namespace Chelems.Core;

/// <summary>Compteurs de service d'un joueur sur un match (mêmes définitions que les colonnes ATP de Jeff Sackmann).</summary>
public sealed class StatsService
{
    /// <summary>Points joués au service, tie-breaks compris (svpt).</summary>
    public int PointsJoues { get; internal set; }
    public int PointsGagnes { get; internal set; }
    /// <summary>Jeux de service, tie-breaks exclus (SvGms).</summary>
    public int Jeux { get; internal set; }
    public int JeuxTenus { get; internal set; }
    public int BallesDeBreakSubies { get; internal set; }
    public int BallesDeBreakSauvees { get; internal set; }
    public int PremieresRentrees { get; internal set; }
    public int GagnesPremiere { get; internal set; }
    public int GagnesSeconde { get; internal set; }
    public int Aces { get; internal set; }
    public int DoublesFautes { get; internal set; }
    /// <summary>Issues du duel au service : [0 première, 1 seconde][<see cref="IssueService"/>], sur les premières rentrées / secondes jouées.</summary>
    public int[][] Issues { get; } = [new int[7], new int[7]];

    public double PctPointsGagnes => PointsJoues == 0 ? double.NaN : (double)PointsGagnes / PointsJoues;
    public double PctJeuxTenus => Jeux == 0 ? double.NaN : (double)JeuxTenus / Jeux;
}

/// <summary>Compteurs de l'échange d'un joueur sur un match (au service et au retour).</summary>
public sealed class StatsEchange
{
    /// <summary>Coups gagnants, aces exclus, retours gagnants compris.</summary>
    public int Gagnants { get; internal set; }
    /// <summary>Fautes en attaque ou en position neutre, retours ratés sur une balle retournable compris.</summary>
    public int FautesDirectes { get; internal set; }
    /// <summary>Dont retours ratés sur une balle retournable (services gagnants comptés en fautes directes).</summary>
    public int FautesDirectesRetour { get; internal set; }
    /// <summary>Fautes en défense, services non retournés compris.</summary>
    public int FautesProvoquees { get; internal set; }
    /// <summary>Points où le joueur était en attaque au premier déséquilibre, et ceux qu'il a gagnés (Conversion%).</summary>
    public int Attaques { get; internal set; }
    public int AttaquesGagnees { get; internal set; }
    /// <summary>Points où le joueur était en défense au premier déséquilibre, et ceux qu'il a gagnés (Steal%).</summary>
    public int Defenses { get; internal set; }
    public int DefensesGagnees { get; internal set; }
}

/// <summary>Longueur des échanges sur un match (tranches du Match Charting Project, voir <see cref="AnalysePoint"/>).</summary>
public sealed class StatsLongueur
{
    public int[] Points { get; } = new int[4];
    public int[] ServeurGagne { get; } = new int[4];
    /// <summary>[tranche][0 gagnants aces compris, 1 fautes provoquées services non retournés compris, 2 fautes directes].</summary>
    public int[][] Fins { get; } = [new int[3], new int[3], new int[3], new int[3]];
    public long Coups { get; internal set; }
}

/// <summary>Score d'un set, vu du joueur A. Points du tie-break renseignés seulement si le set s'y est joué.</summary>
public readonly record struct ScoreSet(int JeuxA, int JeuxB, int PointsTieBreakA = 0, int PointsTieBreakB = 0)
{
    public bool TieBreak => PointsTieBreakA + PointsTieBreakB > 0;
    public int Gagnant => JeuxA > JeuxB ? 0 : 1;

    public override string ToString() =>
        TieBreak ? $"{JeuxA}-{JeuxB}({Math.Min(PointsTieBreakA, PointsTieBreakB)})" : $"{JeuxA}-{JeuxB}";
}

/// <summary>Un point joué, transmis à l'observateur facultatif du moteur (séries, interface, journal).</summary>
public readonly record struct PointJoue(
    int Set, int Serveur, int Gagnant, bool TieBreak, bool BalleDeBreak,
    int JeuxA, int JeuxB, int PointsServeur, int PointsRelanceur, DeroulementPoint Deroulement);

public sealed class ResultatMatch
{
    internal ResultatMatch(Joueur a, Joueur b, FormatMatch format, Surface surface, int premierServeur,
                           List<ScoreSet> sets, StatsService[] service, StatsEchange[] echange, StatsLongueur longueurs)
    {
        A = a;
        B = b;
        Format = format;
        Surface = surface;
        PremierServeur = premierServeur;
        Sets = sets;
        Service = service;
        Echange = echange;
        Longueurs = longueurs;
    }

    public Joueur A { get; }
    public Joueur B { get; }
    public FormatMatch Format { get; }
    public Surface Surface { get; }
    /// <summary>0 = A sert le premier jeu, 1 = B.</summary>
    public int PremierServeur { get; }
    public IReadOnlyList<ScoreSet> Sets { get; }
    /// <summary>Indice 0 = A, 1 = B.</summary>
    public IReadOnlyList<StatsService> Service { get; }
    /// <summary>Indice 0 = A, 1 = B.</summary>
    public IReadOnlyList<StatsEchange> Echange { get; }
    public StatsLongueur Longueurs { get; }

    /// <summary>0 = A gagne, 1 = B gagne.</summary>
    public int Vainqueur => Sets.Count(s => s.Gagnant == 0) == Format.SetsGagnants ? 0 : 1;
    public int NombreTieBreaks => Sets.Count(s => s.TieBreak);

    public string Score => string.Join(' ', Sets);

    public override string ToString()
    {
        var sb = new StringBuilder();
        var gagnant = Vainqueur == 0 ? A : B;
        sb.AppendLine($"{A.Nom} (S {A.Service} / R {A.Retour} / E {A.Echange}) contre {B.Nom} (S {B.Service} / R {B.Retour} / E {B.Echange}), " +
                      $"Bo{Format.BestOf}, {Surface} : {gagnant.Nom} gagne {Score}");
        for (int i = 0; i < 2; i++)
        {
            var j = i == 0 ? A : B;
            var s = Service[i];
            var e = Echange[i];
            int secondes = s.PointsJoues - s.PremieresRentrees;
            sb.AppendLine($"  {j.Nom,-12} service : {s.PointsGagnes}/{s.PointsJoues} ({100 * s.PctPointsGagnes:F1} %), " +
                          $"jeux tenus {s.JeuxTenus}/{s.Jeux}, balles de break sauvées {s.BallesDeBreakSauvees}/{s.BallesDeBreakSubies}");
            sb.AppendLine($"  {"",-12} 1res {100.0 * s.PremieresRentrees / s.PointsJoues:F0} %, gagnés sur 1re {s.GagnesPremiere}/{s.PremieresRentrees}, " +
                          $"sur 2e {s.GagnesSeconde}/{secondes}, aces {s.Aces}, doubles fautes {s.DoublesFautes}");
            sb.AppendLine($"  {"",-12} échange : gagnants {e.Gagnants}, fautes directes {e.FautesDirectes}, provoquées {e.FautesProvoquees}, " +
                          $"conversion {e.AttaquesGagnees}/{e.Attaques}, steal {e.DefensesGagnees}/{e.Defenses}");
        }
        int total = Longueurs.Points.Sum();
        sb.AppendLine($"  Longueur des échanges : {(double)Longueurs.Coups / total:F2} coups en moyenne ; " +
                      string.Join(", ", AnalysePoint.NomsTranches.Select((t, k) => $"{t} : {100.0 * Longueurs.Points[k] / total:F0} %")));
        return sb.ToString();
    }
}
