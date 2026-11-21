using Chelems.Core;
using Chelems.Core.Configuration;
using Chelems.Core.Fidelite;

namespace Chelems.Core.Tests;

public class MoteurMatchTests
{
    private static MoteurMatch Moteur(ConfigMoteur cfg) => new(new ModelePoint(cfg));

    private static MoteurMatch MoteurDuDepot() => Moteur(ConfigMoteur.Charger(CheminsDepot.Trouver().Config));

    [Fact]
    public void MemeGraine_MemeMatch()
    {
        var m = MoteurDuDepot();
        var a = new Joueur("A", 82, 75, 80);
        var b = new Joueur("B", 74, 80, 79);
        var r1 = m.Jouer(a, b, FormatMatch.Bo5, Surface.Terre, new Random(7));
        var r2 = m.Jouer(a, b, FormatMatch.Bo5, Surface.Terre, new Random(7));
        Assert.Equal(r1.Score, r2.Score);
        Assert.Equal(r1.Service[0].PointsGagnes, r2.Service[0].PointsGagnes);
        Assert.Equal(r1.Longueurs.Coups, r2.Longueurs.Coups);
    }

    [Fact]
    public void JoueurQuiGagneTousLesPoints_GagneSixZeroSixZero()
    {
        var m = Moteur(ModelePointTests.ConfigPointFixe(0.5, penteService: 1));
        var r = m.Jouer(new Joueur("A", 99, 99, 50), new Joueur("B", 1, 1, 50), FormatMatch.Bo3, Surface.Dur, new Random(1), premierServeur: 0);
        Assert.Equal("6-0 6-0", r.Score);
        Assert.Equal(6, r.Service[0].Jeux);
        Assert.Equal(24, r.Service[0].PointsJoues);
        Assert.Equal(6, r.Service[1].BallesDeBreakSubies); // une balle de break par jeu (0-40), aussitôt convertie
    }

    [Fact]
    public void ServiceAlterne_EtTieBreakSuitLesRegles()
    {
        var m = Moteur(ModelePointTests.ConfigPointFixe(0.95));
        var points = new List<PointJoue>();
        var r = m.Jouer(new Joueur("A", 80), new Joueur("B", 80), FormatMatch.Bo3, Surface.Dur, new Random(3),
                        premierServeur: 0, observateur: points.Add);
        int setCourant = -1, serveurAttendu = 0, dernierServeurJeu = -1;
        var debutTieBreak = new Dictionary<int, int>();
        for (int i = 0; i < points.Count; i++)
        {
            var p = points[i];
            if (p.TieBreak)
            {
                if (!debutTieBreak.ContainsKey(p.Set)) debutTieBreak[p.Set] = i;
                int n = i - debutTieBreak[p.Set];
                int premier = points[debutTieBreak[p.Set]].Serveur;
                Assert.Equal(n % 4 is 0 or 3 ? premier : 1 - premier, p.Serveur);
                continue;
            }
            if (p.PointsServeur == 0 && p.PointsRelanceur == 0)
            {
                if (p.Set != setCourant)
                {
                    if (setCourant >= 0 && debutTieBreak.TryGetValue(setCourant, out int d)) serveurAttendu = 1 - points[d].Serveur;
                    setCourant = p.Set;
                }
                Assert.Equal(serveurAttendu, p.Serveur);
                dernierServeurJeu = p.Serveur;
                serveurAttendu = 1 - p.Serveur;
            }
            else Assert.Equal(dernierServeurJeu, p.Serveur);
        }
        Assert.True(r.NombreTieBreaks >= 1);
        Assert.Equal(points.Count, r.Service[0].PointsJoues + r.Service[1].PointsJoues);
    }

    [Fact]
    public void ScoresToujoursValides_ConfigDuDepot()
    {
        var m = MoteurDuDepot();
        var rng = new Random(11);
        Surface[] surfaces = [Surface.Dur, Surface.Terre, Surface.Gazon];
        for (int i = 0; i < 3_000; i++)
        {
            var format = i % 2 == 0 ? FormatMatch.Bo3 : FormatMatch.Bo5;
            var a = new Joueur("A", 60 + rng.Next(39), 60 + rng.Next(39), 60 + rng.Next(39));
            var b = new Joueur("B", 60 + rng.Next(39), 60 + rng.Next(39), 60 + rng.Next(39));
            var r = m.Jouer(a, b, format, surfaces[i % 3], rng);
            Assert.Equal(format.SetsGagnants, r.Sets.Count(s => s.Gagnant == r.Vainqueur));
            Assert.True(r.Sets.Count(s => s.Gagnant != r.Vainqueur) < format.SetsGagnants);
            Assert.Equal(r.Vainqueur, r.Sets[^1].Gagnant);
            foreach (var s in r.Sets)
            {
                int g = Math.Max(s.JeuxA, s.JeuxB), p = Math.Min(s.JeuxA, s.JeuxB);
                Assert.True((g == 6 && p <= 4) || (g == 7 && p == 5) || (g == 7 && p == 6 && s.TieBreak), $"set invalide {s}");
            }
            Assert.Equal(r.Sets.Sum(s => s.JeuxA + s.JeuxB - (s.TieBreak ? 1 : 0)), r.Service[0].Jeux + r.Service[1].Jeux);
        }
    }

    [Theory]
    [InlineData(0.60)]
    [InlineData(0.645)]
    [InlineData(0.69)]
    public void PointsIndependants_HoldRetrouveLaFormuleExacte(double p)
    {
        var m = Moteur(ModelePointTests.ConfigPointFixe(p));
        var rng = new Random(5);
        int jeux = 0, tenus = 0;
        for (int i = 0; i < 4_000; i++)
        {
            var r = m.Jouer(new Joueur("A", 80), new Joueur("B", 80), FormatMatch.Bo3, Surface.Dur, rng);
            jeux += r.Service[0].Jeux + r.Service[1].Jeux;
            tenus += r.Service[0].JeuxTenus + r.Service[1].JeuxTenus;
        }
        double attendu = FormulesExactes.ProbaTenirJeu(p);
        double et = Math.Sqrt(attendu * (1 - attendu) / jeux);
        Assert.InRange((double)tenus / jeux, attendu - 4 * et, attendu + 4 * et);
    }

    [Fact]
    public void StatsDuMatch_SontCoherentes()
    {
        var m = MoteurDuDepot();
        var rng = new Random(12);
        for (int i = 0; i < 300; i++)
        {
            var r = m.Jouer(new Joueur("A", 70 + rng.Next(25)), new Joueur("B", 70 + rng.Next(25)), FormatMatch.Bo3, Surface.Dur, rng);
            int points = r.Service[0].PointsJoues + r.Service[1].PointsJoues;
            Assert.Equal(points, r.Longueurs.Points.Sum());
            int fins = r.Longueurs.Fins.Sum(f => f.Sum()) + r.Service[0].DoublesFautes + r.Service[1].DoublesFautes;
            Assert.Equal(points, fins); // chaque point finit par un gagnant (aces compris), une faute ou une double faute
            for (int j = 0; j < 2; j++)
            {
                var s = r.Service[j];
                Assert.Equal(s.PointsGagnes, s.GagnesPremiere + s.GagnesSeconde);
                Assert.Equal(s.PremieresRentrees, s.Issues[0].Sum());
                Assert.Equal(s.PointsJoues - s.PremieresRentrees, s.Issues[1].Sum());
                Assert.Equal(r.Echange[j].Attaques, r.Echange[1 - j].Defenses);
                Assert.Equal(r.Echange[j].Attaques - r.Echange[j].AttaquesGagnees, r.Echange[1 - j].DefensesGagnees);
            }
        }
    }

    [Fact]
    public void FormuleExacte_ValeursConnues()
    {
        Assert.Equal(0.5, FormulesExactes.ProbaTenirJeu(0.5), 12);
        Assert.Equal(1.0, FormulesExactes.ProbaTenirJeu(1.0), 12);
        Assert.Equal(0.73573, FormulesExactes.ProbaTenirJeu(0.6), 5);
    }
}
