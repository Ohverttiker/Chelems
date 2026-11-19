using Chelems.Core.Configuration;

namespace Chelems.Core;

/// <summary>
/// Le seul moteur de point (principe 4). Probabilité que le serveur gagne le point :
/// p = clamp(base + écart de forme + pression + élan, bornes), avec
/// base = centre(surface) + pente × (talent serveur − talent relanceur)
///      + affinité(serveur) − affinité(relanceur) + contre(surface) × opposition des styles / √2.
/// Les termes de forme, de pression et d'élan sont ajoutés par le moteur de match.
/// </summary>
public sealed class ModelePoint
{
    public ConfigMoteur Config { get; }

    public ModelePoint(ConfigMoteur config)
    {
        config.Valider();
        Config = config;
    }

    /// <summary>Probabilité de base, sans forme, pression ni élan.</summary>
    public double ProbaBase(Joueur serveur, Joueur relanceur, Surface surface)
    {
        var s = Config.Surface(surface);
        return Config.PointService.PenteParPointDeTalent * (serveur.Talent - relanceur.Talent) + s.Centre
               + serveur.Style.Projection(s.Affinite) - relanceur.Style.Projection(s.Affinite)
               + s.Contre / Math.Sqrt(2.0) * Style.Opposition(serveur.Style, relanceur.Style);
    }

    public double Borner(double p) => Math.Clamp(p, Config.PointService.BorneMin, Config.PointService.BorneMax);

    /// <summary>Décalage de pression selon l'enjeu du point.</summary>
    public double Pression(TypePoint type)
    {
        var c = Config.Pression;
        return type switch
        {
            TypePoint.BalleDeSetContreServeur => c.BalleSetContre,
            TypePoint.BalleDeSetPourServeur => c.BalleSetPour,
            TypePoint.TieBreak => c.TieBreak,
            TypePoint.BalleDeBreak => c.BalleDeBreak,
            TypePoint.BalleDeJeu => c.BalleDeJeu,
            TypePoint.Egalite => c.Egalite,
            _ => c.Autres,
        };
    }
}

/// <summary>Enjeu d'un point, vu du serveur (même découpage que la mesure du Match Charting Project). Balle de match comprise dans balle de set.</summary>
public enum TypePoint { BalleDeSetContreServeur, BalleDeSetPourServeur, TieBreak, BalleDeBreak, BalleDeJeu, Egalite, Autre }
