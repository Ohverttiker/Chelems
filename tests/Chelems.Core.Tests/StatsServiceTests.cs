using Chelems.Core;
using Chelems.Core.Configuration;
using Chelems.Core.Fidelite;

namespace Chelems.Core.Tests;

/// <summary>Roadmap, étape 1 : effets des stats (Service, Retour, Puissance, Explosivité, Taille, Agressivité) sur le duel au service.</summary>
public class StatsServiceTests
{
    private static readonly ConfigMoteur Config = ConfigMoteur.Charger(CheminsDepot.Trouver().Config);

    private static AnalysePoint Point(Joueur serveur, Joueur relanceur) =>
        AnalysePoint.Calculer(new ModelePoint(Config).Preparer(serveur, relanceur, Surface.Dur));

    [Fact]
    public void ConfigDuDepot_DecritLesStatsDuService()
    {
        Assert.NotNull(Config.Service.Stats);
        string[] attendues = ["service", "puissance", "retour", "explosivite", "taille", "taille_relanceur", "agressivite", "regularite"];
        Assert.Equal(attendues.OrderBy(c => c), Config.Service.Stats!.Keys.OrderBy(c => c));
    }

    /// <summary>Deux joueurs de référence (toutes stats à 75, taille et agressivité de référence) retrouvent les lois de base de la surface.</summary>
    [Theory]
    [InlineData(75)]
    public void JoueursEgaux_RetrouventLesLoisDeBase(double niveau)
    {
        var d = new ModelePoint(Config).Preparer(new Joueur("A", niveau), new Joueur("B", niveau), Surface.Dur);
        var (pr, p1, p2) = Config.LoisService(Surface.Dur);
        Assert.Equal(pr, d.PremiereRentree, 12);
        for (int k = 0; k < 7; k++)
        {
            Assert.Equal(p1.EnTableau()[k], d.Premiere[k], 12);
            Assert.Equal(p2.EnTableau()[k], d.Seconde[k], 12);
        }
    }

    [Fact]
    public void Taille_AideLeServeurEtGeneLeRelanceur()
    {
        var reference = new Joueur("R", 75);
        var base_ = Point(reference, reference);
        var grandServeur = Point(reference with { TailleCm = 205 }, reference);
        var grandRelanceur = Point(reference, reference with { TailleCm = 205 });
        Assert.True(grandServeur.Aces > base_.Aces + 0.02);
        Assert.True(grandServeur.PremieresRentrees > base_.PremieresRentrees);
        Assert.True(grandServeur.Serveur > base_.Serveur);
        Assert.True(grandRelanceur.Serveur > base_.Serveur); // CLAUDE.md §5.1 : le grand relanceur retourne moins bien
    }

    [Fact]
    public void Agressivite_MoinsDePremieresPlusDePointsGratuitsPlusDeDoublesFautes()
    {
        var reference = new Joueur("R", 75);
        var prudent = Point(reference with { Agressivite = 10 }, reference);
        var agressif = Point(reference with { Agressivite = 90 }, reference);
        Assert.True(agressif.PremieresRentrees < prudent.PremieresRentrees - 0.1);
        Assert.True(agressif.IssuesService[0][(int)IssueService.Ace] > prudent.IssuesService[0][(int)IssueService.Ace]);
        Assert.True(agressif.DoublesFautes > prudent.DoublesFautes);
        Assert.True(agressif.GagnesSeconde < prudent.GagnesSeconde);
    }

    /// <summary>Créateur : le risque paie sur la première. À capacité de référence, il ne change pas le total des points au service.</summary>
    [Fact]
    public void Agressivite_PaieSurLaPremiere_SansChangerLeTotal()
    {
        var reference = new Joueur("R", 75);
        var prudent = Point(reference with { Agressivite = 10 }, reference);
        var agressif = Point(reference with { Agressivite = 90 }, reference);
        Assert.True(agressif.GagnesPremiere > prudent.GagnesPremiere + 0.03);
        Assert.InRange(agressif.Serveur - prudent.Serveur, -0.003, 0.003);
    }

    /// <summary>Créateur : la Puissance donne des aces et des services gagnants, le Service des services gagnants et l'attaque.</summary>
    [Fact]
    public void Puissance_DonneDesAces_Service_DonneLAttaque()
    {
        var r = new Joueur("R", 75);
        var b = Point(r, r);
        var puissant = Point(r with { Puissance = 90 }, r);
        var placeur = Point(r with { Service = 90 }, r);
        double Ace(AnalysePoint a) => a.IssuesService[0][(int)IssueService.Ace];
        double Attaque(AnalysePoint a) => a.IssuesService[0][(int)IssueService.Attaque];
        Assert.True(Ace(puissant) - Ace(b) > Ace(placeur) - Ace(b));
        Assert.True(Attaque(placeur) > Attaque(b));
        Assert.True(Attaque(placeur) - Attaque(b) > Attaque(puissant) - Attaque(b));
    }

    /// <summary>
    /// [Créateur, 2026-11-26] Chaque stat ne joue que dans son rôle : la Puissance au service et sur la première seulement,
    /// l'Explosivité au retour, surtout sur la première ; l'Échange ne compte pas dans le duel au retour.
    /// </summary>
    [Fact]
    public void ChaqueStat_DansSonRole()
    {
        var r = new Joueur("R", 75);
        var b = Point(r, r);
        // La Puissance aide le serveur sur la première, pas sur la seconde ; au retour, elle ne joue pas.
        var puissant = Point(r with { Puissance = 90 }, r);
        Assert.True(puissant.GagnesPremiere > b.GagnesPremiere);
        Assert.Equal(b.GagnesSeconde, puissant.GagnesSeconde, 9);
        Assert.Equal(b.Serveur, Point(r, r with { Puissance = 90 }).Serveur, 12);
        // L'Explosivité ne joue pas au service ; au retour, plus sur la première que sur la seconde.
        Assert.Equal(b.Serveur, Point(r with { Explosivite = 90 }, r).Serveur, 12);
        var explosif = Point(r, r with { Explosivite = 90 });
        Assert.True(b.GagnesPremiere - explosif.GagnesPremiere > b.GagnesSeconde - explosif.GagnesSeconde);
        // Le Retour aide le relanceur sur les deux balles.
        var relanceur = Point(r, r with { Retour = 90 });
        Assert.True(b.GagnesPremiere > relanceur.GagnesPremiere && b.GagnesSeconde > relanceur.GagnesSeconde);
        // L'Échange ne joue pas dans le duel au retour (créateur, 2026-11-26) : il agit dans l'échange (échange neutralisé ici).
        var sansEchange = Config with { Echange = Config.Echange with { Pente = 0 } };
        var b0 = AnalysePoint.Calculer(new ModelePoint(sansEchange).Preparer(r, r, Surface.Dur));
        var e0 = AnalysePoint.Calculer(new ModelePoint(sansEchange).Preparer(r, r with { Echange = 90 }, Surface.Dur));
        Assert.Equal(b0.Serveur, e0.Serveur, 12);
    }

    /// <summary>
    /// Le moteur reproduit les pentes mesurées entre joueurs réels (MCP, dur) pour la taille, au service et au retour :
    /// effet direct de la Taille, plus ce qui passe par la Puissance (serveur) et l'Explosivité (relanceur), liées à la taille.
    /// </summary>
    [Fact]
    public void Taille_RetrouveLesPentesMesurees()
    {
        var chemins = CheminsDepot.Trouver();
        var pop = ConfigPopulation.Charger(chemins.ConfigPopulation);
        var joueurs = EfficaciteJoueur.Charger(Path.Combine(chemins.Objectifs(Surface.Dur), "efficacite_joueurs.csv"), Config, Surface.Dur);
        var mesure = CaleurStats.Mesurer(joueurs);
        var service = CaleurStats.PentesDuMoteur(Config, Surface.Dur, (j, d) => j with { TailleCm = 188 + d, Puissance = 75 + pop.TaillePuissance * d }, 5);
        var retour = CaleurStats.PentesDuMoteurAuRetour(Config, Surface.Dur, (j, d) => j with { TailleCm = 188 + d, Explosivite = 75 - pop.TailleExplosivite * d }, 5);
        for (int g = 0; g < 8; g++)
        {
            Assert.InRange(service[g], mesure.ParCm[g] - 0.0015, mesure.ParCm[g] + 0.0015); // 0,15 point de % par cm
            if (!double.IsNaN(mesure.ParCmRelanceur[g]))
                Assert.InRange(retour[g], mesure.ParCmRelanceur[g] - 0.0015, mesure.ParCmRelanceur[g] + 0.0015);
        }
    }

    /// <summary>Les liens Puissance et Explosivité / taille laissent l'effet direct de la taille dans le bon sens.</summary>
    [Fact]
    public void Taille_EffetDirectDansLeBonSens()
    {
        var r = new Joueur("R", 75);
        var b = Point(r, r);
        var grandServeur = Point(r with { TailleCm = 195 }, r);
        var grandRelanceur = Point(r, r with { TailleCm = 195 });
        Assert.True(grandServeur.GagnesPremiere > b.GagnesPremiere);
        Assert.True(grandServeur.IssuesService[0][(int)IssueService.Gagnant] >= b.IssuesService[0][(int)IssueService.Gagnant] - 3e-3); // à 0,3 point près
        Assert.True(grandRelanceur.GagnesPremiere > b.GagnesPremiere);
        Assert.True(grandRelanceur.GagnesSeconde >= b.GagnesSeconde - 1e-3);
    }
}
