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

    public double PctPointsGagnes => PointsJoues == 0 ? double.NaN : (double)PointsGagnes / PointsJoues;
    public double PctJeuxTenus => Jeux == 0 ? double.NaN : (double)JeuxTenus / Jeux;
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
    int JeuxA, int JeuxB, int PointsServeur, int PointsRelanceur, TypePoint Type, double Probabilite);

public sealed class ResultatMatch
{
    internal ResultatMatch(Joueur a, Joueur b, FormatMatch format, Surface surface, int premierServeur,
                           List<ScoreSet> sets, StatsService[] service, double[] formeMatch)
    {
        A = a;
        B = b;
        Format = format;
        Surface = surface;
        FormeDuMatch = formeMatch;
        PremierServeur = premierServeur;
        Sets = sets;
        Service = service;
    }

    public Joueur A { get; }
    public Joueur B { get; }
    public FormatMatch Format { get; }
    public Surface Surface { get; }
    /// <summary>Forme fixe sur le match (phase lente + aléa du jour), en probabilité de point ; indice 0 = A.</summary>
    public IReadOnlyList<double> FormeDuMatch { get; }
    /// <summary>0 = A sert le premier jeu, 1 = B.</summary>
    public int PremierServeur { get; }
    public IReadOnlyList<ScoreSet> Sets { get; }
    /// <summary>Indice 0 = A, 1 = B.</summary>
    public IReadOnlyList<StatsService> Service { get; }

    /// <summary>0 = A gagne, 1 = B gagne.</summary>
    public int Vainqueur => Sets.Count(s => s.Gagnant == 0) == Format.SetsGagnants ? 0 : 1;
    public int NombreTieBreaks => Sets.Count(s => s.TieBreak);

    public string Score => string.Join(' ', Sets);

    public override string ToString()
    {
        var sb = new StringBuilder();
        var gagnant = Vainqueur == 0 ? A : B;
        sb.AppendLine($"{A.Nom} ({A.Talent}) contre {B.Nom} ({B.Talent}), Bo{Format.BestOf}, {Surface} : {gagnant.Nom} gagne {Score}");
        for (int i = 0; i < 2; i++)
        {
            var j = i == 0 ? A : B;
            var s = Service[i];
            sb.AppendLine($"  {j.Nom,-12} points gagnés au service {s.PointsGagnes}/{s.PointsJoues} ({100 * s.PctPointsGagnes:F1} %), " +
                          $"jeux tenus {s.JeuxTenus}/{s.Jeux} ({100 * s.PctJeuxTenus:F1} %), " +
                          $"balles de break sauvées {s.BallesDeBreakSauvees}/{s.BallesDeBreakSubies}");
        }
        return sb.ToString();
    }
}
