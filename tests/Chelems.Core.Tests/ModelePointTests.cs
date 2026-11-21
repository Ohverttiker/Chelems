using Chelems.Core;
using Chelems.Core.Configuration;
using Chelems.Core.Fidelite;

namespace Chelems.Core.Tests;

public class ModelePointTests
{
    private static readonly IssuesService OrientationService = new(1, 1, 0.32, 0, -0.32, -1, -1);
    private static readonly IssuesEchange OrientationEchange = new(1, 0.32, 0, -0.32, -1);

    private static Dictionary<string, ConfigSurface> Surfaces(double vitesse = 0) => new()
    {
        ["dur"] = new(vitesse), ["terre"] = new(vitesse), ["gazon"] = new(vitesse),
    };

    /// <summary>Configuration où le serveur gagne chaque point avec la probabilité p (ace ou retour gagnant, pas d'échange).</summary>
    internal static ConfigMoteur ConfigPointFixe(double p, double penteService = 0)
    {
        var loi = new IssuesService(p, 0, 0, 0, 0, 1 - p);
        return new ConfigMoteur("test", new ConfigService(0.6, loi, loi, OrientationService, penteService),
                                ConfigTest().Echange, Surfaces());
    }

    /// <summary>Configuration complète à lois simples (service et échange), sans écart de stats sauf demande.</summary>
    internal static ConfigMoteur ConfigTest(double penteService = 0, double penteEchange = 0, double vitesse = 0) =>
        new("test",
            new ConfigService(0.62, new IssuesService(0.15, 0.2, 0.2, 0.4, 0.04, 0.01), new IssuesService(0.01, 0.15, 0.06, 0.58, 0.06, 0.04, 0.10),
                              OrientationService, penteService),
            new ConfigEchange(new IssuesEchange(0.2, 0.5, 0.2, 0.04, 0.06), new IssuesEchange(0.04, 0.25, 0.4, 0.1, 0.21),
                              new IssuesEchange(0.03, 0.02, 0.15, 0.6, 0.2), OrientationEchange, penteEchange),
            Surfaces(vitesse));

    [Fact]
    public void ConfigDuDepot_SeCharge()
    {
        var cfg = ConfigMoteur.Charger(CheminsDepot.Trouver().Config);
        Assert.Equal("0.3", cfg.Version);
        Assert.True(cfg.Service.Pente > 0 && cfg.Echange.Pente > 0);
    }

    [Fact]
    public void ConfigInvalide_EstRefusee()
    {
        var c = ConfigTest();
        Assert.Throws<InvalidDataException>(() => (c with { Echange = c.Echange with { Neutre = new IssuesEchange(0.5, 0.5, 0.5, 0, 0) } }).Valider());
        Assert.Throws<InvalidDataException>(() => (c with { Service = c.Service with { Premiere = new IssuesService(0.5, 0, 0, 0.3, 0, 0, 0.2) } }).Valider());
    }

    [Fact]
    public void JoueursEgaux_RetrouventLesLoisDeBase()
    {
        var cfg = ConfigTest(penteService: 0.05, penteEchange: 0.05);
        var d = new ModelePoint(cfg).Preparer(new Joueur("A", 80), new Joueur("B", 80), Surface.Dur);
        Assert.Equal(cfg.Service.Premiere.EnTableau(), d.Premiere, new Proche(1e-12));
        Assert.Equal(cfg.Service.Seconde.EnTableau(), d.Seconde, new Proche(1e-12));
        Assert.Equal(cfg.Echange.Defense.EnTableau(), d.Echange[1][(int)EtatEchange.Defense], new Proche(1e-12));
        var a = AnalysePoint.Calculer(d);
        Assert.Equal(a.Valeur[0], a.Valeur[1], new Proche(1e-12)); // même joueur des deux côtés : mêmes valeurs d'état
    }

    [Fact]
    public void ChaqueStat_AugmenteLePointDeSonCote()
    {
        var m = new ModelePoint(ConfigTest(penteService: 0.02, penteEchange: 0.02));
        double P(Joueur s, Joueur r) => AnalysePoint.Calculer(m.Preparer(s, r, Surface.Dur)).Serveur;
        var ref_ = new Joueur("R", 75);
        double base_ = P(ref_, ref_);
        Assert.True(P(ref_ with { Service = 85 }, ref_) > base_);
        Assert.True(P(ref_ with { Echange = 85 }, ref_) > base_);
        Assert.True(P(ref_, ref_ with { Retour = 85 }) < base_);
        Assert.True(P(ref_, ref_ with { Echange = 85 }) < base_);
        // Le Retour du serveur ne joue pas sur son service, ni le Service du relanceur.
        Assert.Equal(base_, P(ref_ with { Retour = 99 }, ref_ with { Service = 99 }), 12);
    }

    [Fact]
    public void Decalage_DeplaceLaMasseVersLesIssuesFavorables()
    {
        var m = new ModelePoint(ConfigTest(penteService: 0.03));
        var d = m.Preparer(new Joueur("S", 90, 75, 75), new Joueur("R", 75), Surface.Dur);
        var b = ConfigTest().Service.Premiere;
        Assert.True(d.Premiere[(int)IssueService.Ace] > b.Ace);
        Assert.True(d.Premiere[(int)IssueService.RetourGagnant] < b.RetourGagnant);
        Assert.Equal(1, d.Premiere.Sum(), 12);
        Assert.Equal(0, d.Premiere[(int)IssueService.DoubleFaute]);
    }

    [Fact]
    public void VitesseDeLaSurface_AvantageLAttaque()
    {
        var lent = AnalysePoint.Calculer(new ModelePoint(ConfigTest(vitesse: -0.3)).Preparer(new Joueur("A", 75), new Joueur("B", 75), Surface.Dur));
        var rapide = AnalysePoint.Calculer(new ModelePoint(ConfigTest(vitesse: 0.3)).Preparer(new Joueur("A", 75), new Joueur("B", 75), Surface.Dur));
        Assert.True(rapide.Conversion > lent.Conversion);
        Assert.True(rapide.Serveur > lent.Serveur);
        Assert.True(rapide.LongueurMoyenne < lent.LongueurMoyenne);
    }

    [Fact]
    public void LoisDeServiceDeLaSurface_RemplacentCellesDeReference()
    {
        var c = ConfigTest();
        var gazon = new IssuesService(0.3, 0.2, 0.1, 0.3, 0.05, 0.05);
        c = c with { Surfaces = new(c.Surfaces) { ["gazon"] = new ConfigSurface(0, 0.7, gazon) } };
        var m = new ModelePoint(c);
        var g = m.Preparer(new Joueur("A", 75), new Joueur("B", 75), Surface.Gazon);
        var d = m.Preparer(new Joueur("A", 75), new Joueur("B", 75), Surface.Dur);
        Assert.Equal(0.7, g.PremiereRentree);
        Assert.Equal(gazon.EnTableau(), g.Premiere, new Proche(1e-12));
        Assert.Equal(c.Service.Seconde.EnTableau(), g.Seconde, new Proche(1e-12)); // seconde absente : celle de référence
        Assert.Equal(c.Service.Premiere.EnTableau(), d.Premiere, new Proche(1e-12));
        Assert.Throws<InvalidDataException>(() => (c with { Surfaces = new(c.Surfaces) { ["terre"] = new ConfigSurface(0, null, gazon with { DoubleFaute = 0.1 }) } }).Valider());
    }

    [Fact]
    public void VitesseDeLaSurface_NeTouchePasAuDuelDuService()
    {
        var lent = new ModelePoint(ConfigTest(penteService: 0.02, vitesse: -0.3)).Preparer(new Joueur("A", 80, 75, 75), new Joueur("B", 75), Surface.Dur);
        var rapide = new ModelePoint(ConfigTest(penteService: 0.02, vitesse: 0.3)).Preparer(new Joueur("A", 80, 75, 75), new Joueur("B", 75), Surface.Dur);
        Assert.Equal(lent.Premiere, rapide.Premiere, new Proche(1e-12));
        Assert.NotEqual(lent.Echange[0][0][0], rapide.Echange[0][0][0]);
    }

    /// <summary>Décision du créateur : la faute est la plus probable en défense, la moins probable en neutre.</summary>
    [Fact]
    public void ConfigDuDepot_FautesMaximalesEnDefenseMinimalesEnNeutre()
    {
        var e = ConfigMoteur.Charger(CheminsDepot.Trouver().Config).Echange;
        Assert.True(e.Defense.Faute > e.Attaque.Faute && e.Attaque.Faute > e.Neutre.Faute,
                    $"fautes : défense {e.Defense.Faute}, attaque {e.Attaque.Faute}, neutre {e.Neutre.Faute}");
    }

    /// <summary>Les retours ratés changent le comptage des fins (faute directe au lieu de provoquée), pas l'issue du point.</summary>
    [Fact]
    public void FautesDeRetour_ComptageSeulement()
    {
        var c = ConfigTest();
        var avec = c with { Surfaces = new(c.Surfaces) { ["dur"] = new ConfigSurface(0, FautesRetour: new(0.1, 0.6)) } };
        var a0 = AnalysePoint.Calculer(new ModelePoint(c).Preparer(new Joueur("A", 75), new Joueur("B", 75), Surface.Dur));
        var duel = new ModelePoint(avec).Preparer(new Joueur("A", 75), new Joueur("B", 75), Surface.Dur);
        var a1 = AnalysePoint.Calculer(duel);
        Assert.Equal(a0.Serveur, a1.Serveur, 12);
        double sg = 0.62 * 0.2 * 0.1 + 0.38 * 0.15 * 0.6;
        Assert.Equal(a0.Fins[4][2] + sg, a1.Fins[4][2], 12);
        Assert.Equal(a0.Fins[4][1] - sg, a1.Fins[4][1], 12);
        var rng = new Random(3);
        int n = 200_000, retours = 0;
        for (int i = 0; i < n; i++) if (ModelePoint.Jouer(duel, rng).Fin == FinPoint.FauteRetour) retours++;
        Assert.InRange((double)retours / n, sg - 4 * Math.Sqrt(sg / n), sg + 4 * Math.Sqrt(sg / n));
    }

    /// <summary>La résolution exacte et la simulation coup par coup décrivent le même point.</summary>
    [Fact]
    public void AnalyseExacte_EgaleLaSimulation()
    {
        var m = new ModelePoint(ConfigTest(penteService: 0.02, penteEchange: 0.02, vitesse: 0.1));
        var duel = m.Preparer(new Joueur("A", 85, 70, 80), new Joueur("B", 72, 78, 76), Surface.Dur);
        var a = AnalysePoint.Calculer(duel);
        var rng = new Random(4);
        const int n = 400_000;
        int gagnes = 0, aces = 0, att = 0, attGagnes = 0;
        long coups = 0;
        int[] tranches = new int[4];
        for (int i = 0; i < n; i++)
        {
            var d = ModelePoint.Jouer(duel, rng);
            if (d.ServeurGagne) gagnes++;
            if (d.Fin == FinPoint.Ace) aces++;
            if (d.Attaquant >= 0) { att++; if ((d.Attaquant == 0) == d.ServeurGagne) attGagnes++; }
            coups += d.Coups;
            tranches[AnalysePoint.Tranche(d.Coups)]++;
        }
        void Proche(double exact, double simule, int effectif)
        {
            double et = Math.Sqrt(exact * (1 - exact) / effectif);
            Assert.InRange(simule, exact - 4 * et, exact + 4 * et);
        }
        Proche(a.Serveur, (double)gagnes / n, n);
        Proche(a.Aces, (double)aces / n, n);
        Proche(a.PartDesequilibre, (double)att / n, n);
        Proche(a.Conversion, (double)attGagnes / att, att);
        for (int t = 0; t < 4; t++) Proche(a.Part[t], (double)tranches[t] / n, n);
        Assert.InRange((double)coups / n, a.LongueurMoyenne - 0.02, a.LongueurMoyenne + 0.02);
    }

    [Fact]
    public void ConventionDesCoups_DuMatchChartingProject()
    {
        // Ace : 1 coup ; retour gagnant : 2 ; double faute : 0 (tranche 1-3).
        var rng = new Random(1);
        var m = new ModelePoint(ConfigTest());
        var duel = m.Preparer(new Joueur("A", 75), new Joueur("B", 75), Surface.Dur);
        for (int i = 0; i < 20_000; i++)
        {
            var d = ModelePoint.Jouer(duel, rng);
            switch (d.Fin)
            {
                case FinPoint.Ace or FinPoint.ServiceGagnant: Assert.Equal(1, d.Coups); Assert.True(d.ServeurGagne); break;
                case FinPoint.RetourGagnant: Assert.Equal(2, d.Coups); Assert.False(d.ServeurGagne); break;
                case FinPoint.DoubleFaute: Assert.Equal(0, d.Coups); Assert.False(d.PremiereRentree); break;
                case FinPoint.Gagnant: Assert.Equal(d.Auteur == 0, d.ServeurGagne); Assert.True(d.Coups >= 3); break;
                default: Assert.Equal(d.Auteur == 1, d.ServeurGagne); Assert.True(d.Coups >= 2); break;
            }
            // Parité : le serveur frappe les coups impairs, donc un coup gagnant du serveur termine un échange de longueur impaire.
            if (d.Fin == FinPoint.Gagnant) Assert.Equal(d.Auteur == 0, d.Coups % 2 == 1);
        }
    }

    private sealed class Proche(double tolerance) : IEqualityComparer<double>
    {
        public bool Equals(double x, double y) => Math.Abs(x - y) <= tolerance;
        public int GetHashCode(double obj) => 0;
    }
}
