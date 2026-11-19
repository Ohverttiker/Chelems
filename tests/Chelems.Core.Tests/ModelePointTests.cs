using Chelems.Core;
using Chelems.Core.Configuration;

namespace Chelems.Core.Tests;

public class ModelePointTests
{
    /// <summary>Configuration de test : sans forme, pression, élan, affinité ni contre, sauf demande.</summary>
    internal static ConfigMoteur Config(double centre = 0.64, double pente = 0.005, double borneMin = 0.4, double borneMax = 0.9,
                                        double contre = 0, double affinite = 0, double formeMatch = 0, double formeSet = 0,
                                        ConfigPression? pression = null, ConfigElan? elan = null) =>
        new("test", new ConfigPointService(pente, borneMin, borneMax),
            new Dictionary<string, ConfigSurface>
            {
                ["dur"] = new(centre, [0, affinite], contre),
                ["terre"] = new(centre - 0.02, [-affinite, 0], contre),
                ["gazon"] = new(centre + 0.02, [affinite, 0], contre),
            },
            new ConfigForme(formeMatch, 0.2, 0.9, formeSet), new ConfigConstance(0.5),
            pression ?? new ConfigPression(0, 0, 0, 0, 0, 0, 0), elan ?? new ConfigElan(0, 0, 0));

    [Fact]
    public void TalentsEgauxStylesNeutres_DonneLeCentreDeLaSurface()
    {
        var m = new ModelePoint(Config());
        var a = new Joueur("A", 80); var b = new Joueur("B", 80);
        Assert.Equal(0.64, m.ProbaBase(a, b, Surface.Dur), 12);
        Assert.Equal(0.62, m.ProbaBase(a, b, Surface.Terre), 12);
        Assert.Equal(0.66, m.ProbaBase(a, b, Surface.Gazon), 12);
    }

    [Fact]
    public void LineaireEnTalent_EtSymetrique()
    {
        var m = new ModelePoint(Config());
        var a = new Joueur("A", 84); var b = new Joueur("B", 80);
        Assert.Equal(0.66, m.ProbaBase(a, b, Surface.Dur), 12);
        Assert.Equal(2 * 0.64, m.ProbaBase(a, b, Surface.Dur) + m.ProbaBase(b, a, Surface.Dur), 12);
    }

    [Fact]
    public void Bornes_SontAppliquees()
    {
        var m = new ModelePoint(Config());
        Assert.Equal(0.9, m.Borner(0.97));
        Assert.Equal(0.4, m.Borner(0.1));
    }

    [Fact]
    public void Contre_EstAntisymetrique()
    {
        var m = new ModelePoint(Config(contre: 0.01));
        var a = new Joueur("A", 80, new Style(1.0, 0.5));
        var b = new Joueur("B", 80, new Style(-0.3, 1.2));
        double ea = m.ProbaBase(a, b, Surface.Dur) - 0.64, eb = m.ProbaBase(b, a, Surface.Dur) - 0.64;
        Assert.NotEqual(0, ea);
        Assert.Equal(-ea, eb, 12);
    }

    [Fact]
    public void Affinite_DependDeLaSurface_PasDuJoueur()
    {
        // Même joueur, styles orientés « gazon » : avantagé sur gazon, désavantagé sur terre (principe 3).
        var m = new ModelePoint(Config(affinite: 0.01));
        var serveur = new Joueur("S", 80, new Style(1.5, 0));
        var neutre = new Joueur("N", 80);
        Assert.True(m.ProbaBase(serveur, neutre, Surface.Gazon) > 0.66);
        Assert.True(m.ProbaBase(serveur, neutre, Surface.Terre) < 0.62);
        Assert.Equal(0.64, m.ProbaBase(serveur, neutre, Surface.Dur), 12); // direction du dur orthogonale
    }

    [Fact]
    public void Pression_SuitLeTypeDePoint()
    {
        var m = new ModelePoint(Config(pression: new ConfigPression(0.01, -0.02, -0.03, -0.04, 0.05, 0.06, -0.07)));
        Assert.Equal(0.01, m.Pression(TypePoint.BalleDeSetContreServeur));
        Assert.Equal(-0.02, m.Pression(TypePoint.BalleDeSetPourServeur));
        Assert.Equal(-0.03, m.Pression(TypePoint.TieBreak));
        Assert.Equal(-0.04, m.Pression(TypePoint.BalleDeBreak));
        Assert.Equal(0.05, m.Pression(TypePoint.BalleDeJeu));
        Assert.Equal(0.06, m.Pression(TypePoint.Egalite));
        Assert.Equal(-0.07, m.Pression(TypePoint.Autre));
    }

    [Fact]
    public void ConfigInvalide_EstRefusee()
    {
        Assert.Throws<InvalidDataException>(() => new ModelePoint(Config(centre: 0.95)));
    }

    [Fact]
    public void ConfigDuDepot_SeCharge()
    {
        var cfg = ConfigMoteur.Charger(Fidelite.CheminsDepot.Trouver().Config);
        foreach (var s in new[] { Surface.Dur, Surface.Terre, Surface.Gazon })
            Assert.InRange(cfg.Surface(s).Centre, 0.55, 0.75);
        Assert.InRange(cfg.Forme.EcartTypeMatch, 0, 0.1);
    }
}

public class ModeleFormeTests
{
    [Theory]
    [InlineData(50, 1.0)]
    [InlineData(99, 0.5)]
    [InlineData(1, 1.5)]
    public void Constance_ModuleLaVariance(double constance, double attendu)
    {
        var f = new ModeleForme(ModelePointTests.Config(formeMatch: 0.03));
        Assert.Equal(attendu, f.Multiplicateur(new Joueur("J", 80, Style.Neutre, constance)), 12);
    }

    [Fact]
    public void PhaseLente_GardeSaVarianceEtSaPersistance()
    {
        var cfg = ModelePointTests.Config(formeMatch: 0.03);
        var f = new ModeleForme(cfg);
        var j = new Joueur("J", 80);
        var rng = new Random(3);
        var etat = new EtatForme();
        var serie = new List<double>();
        for (int i = 0; i < 200_000; i++) { f.Avancer(etat, j, rng); serie.Add(etat.Lente); }
        double v = serie.Average(x => x * x);
        double c = serie.Zip(serie.Skip(1)).Average(p => p.First * p.Second);
        Assert.InRange(v, 0.95 * 0.2 * 0.03 * 0.03, 1.05 * 0.2 * 0.03 * 0.03); // part lente de la variance
        Assert.InRange(c / v, 0.88, 0.92);                                     // persistance 0,9
    }

    [Fact]
    public void FormeDuMatch_ALaVarianceConfiguree()
    {
        var f = new ModeleForme(ModelePointTests.Config(formeMatch: 0.03));
        var j = new Joueur("J", 80, Style.Neutre, 1); // Constance minimale : variance × 1,5
        var rng = new Random(4);
        double s2 = 0; const int n = 200_000;
        for (int i = 0; i < n; i++) { double x = f.FormeDuMatch(j, f.TirerLente(j, rng), rng); s2 += x * x; }
        Assert.InRange(s2 / n, 0.97 * 1.5 * 0.0009, 1.03 * 1.5 * 0.0009);
    }
}

public class EnjeuTests
{
    [Fact]
    public void Enjeu_ReduitLAleaDuJour_SelonLaConstance()
    {
        var cfg = ModelePointTests.Config(formeMatch: 0.03) with
        {
            Enjeu = new ConfigEnjeu(0.5, new Dictionary<string, double> { ["G"] = 1, ["M"] = 0 }),
        };
        var f = new ModeleForme(cfg);
        var constant = new Joueur("C", 80, Style.Neutre, 99);
        var irregulier = new Joueur("I", 80, Style.Neutre, 1);
        Assert.Equal(0.5, f.FacteurEnjeu(constant, cfg.Enjeu!.Valeur("G")), 12);   // Constance 99 : variance du jour divisée par 2
        Assert.Equal(1.0, f.FacteurEnjeu(irregulier, cfg.Enjeu.Valeur("G")), 12);  // Constance 1 : aucun effet
        Assert.Equal(1.0, f.FacteurEnjeu(constant, cfg.Enjeu.Valeur("M")), 12);    // tournoi sans enjeu
        Assert.Equal(0.0, cfg.Enjeu.Valeur("inconnu"));
    }
}
