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
        var a = new Joueur("A", 82, new Style(0.3, -1), 70);
        var b = new Joueur("B", 78, new Style(-0.5, 0.2), 30);
        var r1 = m.Jouer(a, b, FormatMatch.Bo5, Surface.Terre, new Random(7));
        var r2 = m.Jouer(a, b, FormatMatch.Bo5, Surface.Terre, new Random(7));
        Assert.Equal(r1.Score, r2.Score);
        Assert.Equal(r1.Service[0].PointsGagnes, r2.Service[0].PointsGagnes);
        Assert.Equal(r1.FormeDuMatch[0], r2.FormeDuMatch[0]);
    }

    [Fact]
    public void JoueurQuiGagneTousLesPoints_GagneSixZeroSixZero()
    {
        var m = Moteur(ModelePointTests.Config(centre: 0.5, pente: 1, borneMin: 1e-9, borneMax: 1 - 1e-9));
        var r = m.Jouer(new Joueur("A", 99), new Joueur("B", 1), FormatMatch.Bo3, Surface.Dur, new Random(1), premierServeur: 0);
        Assert.Equal("6-0 6-0", r.Score);
        Assert.Equal(6, r.Service[0].Jeux);
        Assert.Equal(24, r.Service[0].PointsJoues);
        Assert.Equal(6, r.Service[1].BallesDeBreakSubies); // une balle de break par jeu (0-40), aussitôt convertie
    }

    [Fact]
    public void ServiceAlterne_EtTieBreakSuitLesRegles()
    {
        var m = Moteur(ModelePointTests.Config(centre: 0.95, pente: 0, borneMax: 0.99));
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
    public void ScoresToujoursValides_AvecToutesLesDynamiques()
    {
        var m = MoteurDuDepot();
        var rng = new Random(11);
        Surface[] surfaces = [Surface.Dur, Surface.Terre, Surface.Gazon];
        for (int i = 0; i < 3_000; i++)
        {
            var format = i % 2 == 0 ? FormatMatch.Bo3 : FormatMatch.Bo5;
            var a = new Joueur("A", 70 + rng.Next(25), Style.Tirer(rng), rng.Next(1, 100));
            var b = new Joueur("B", 70 + rng.Next(25), Style.Tirer(rng), rng.Next(1, 100));
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
    public void SansDynamique_HoldRetrouveLaFormuleExacte(double p)
    {
        var m = Moteur(ModelePointTests.Config(centre: p, pente: 0));
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
    public void TypeDePoint_CorrespondAuScore()
    {
        var m = MoteurDuDepot();
        var points = new List<PointJoue>();
        var rng = new Random(8);
        for (int i = 0; i < 200; i++)
            m.Jouer(new Joueur("A", 80), new Joueur("B", 80), FormatMatch.Bo3, Surface.Dur, rng, observateur: points.Add);
        foreach (var p in points.Where(p => !p.TieBreak))
        {
            int js = p.Serveur == 0 ? p.JeuxA : p.JeuxB, jr = p.Serveur == 0 ? p.JeuxB : p.JeuxA;
            bool bb = p.PointsRelanceur >= 3 && p.PointsRelanceur > p.PointsServeur;
            bool bj = p.PointsServeur >= 3 && p.PointsServeur > p.PointsRelanceur;
            var attendu = bb && jr + 1 >= 6 && jr + 1 - js >= 2 ? TypePoint.BalleDeSetContreServeur
                        : bj && js + 1 >= 6 && js + 1 - jr >= 2 ? TypePoint.BalleDeSetPourServeur
                        : bb ? TypePoint.BalleDeBreak : bj ? TypePoint.BalleDeJeu
                        : p.PointsServeur == p.PointsRelanceur && p.PointsServeur >= 2 ? TypePoint.Egalite : TypePoint.Autre;
            Assert.Equal(attendu, p.Type);
        }
        Assert.Contains(points, p => p.Type == TypePoint.BalleDeSetPourServeur);
        Assert.Contains(points, p => p.TieBreak && p.Type == TypePoint.TieBreak);
    }

    [Fact]
    public void ElanDuPoint_ProduitLEcartConfigure()
    {
        // Élan de ±0,05 : P(gagner | point précédent gagné) − P(gagner | perdu) ≈ 0,10 dans un même jeu.
        var m = Moteur(ModelePointTests.Config(pente: 0, elan: new ConfigElan(0.05, 0, 0)));
        var points = new List<PointJoue>();
        var rng = new Random(9);
        for (int i = 0; i < 2_000; i++)
            m.Jouer(new Joueur("A", 80), new Joueur("B", 80), FormatMatch.Bo3, Surface.Dur, rng, observateur: points.Add);
        int[,] t = new int[2, 2];
        for (int i = 1; i < points.Count; i++)
        {
            var a = points[i - 1]; var b = points[i];
            if (a.TieBreak || b.TieBreak || a.Serveur != b.Serveur || a.JeuxA != b.JeuxA || a.JeuxB != b.JeuxB || a.Set != b.Set) continue;
            t[a.Gagnant == a.Serveur ? 1 : 0, b.Gagnant == b.Serveur ? 1 : 0]++;
        }
        double d = (double)t[1, 1] / (t[1, 0] + t[1, 1]) - (double)t[0, 1] / (t[0, 0] + t[0, 1]);
        Assert.InRange(d, 0.085, 0.115);
    }

    [Fact]
    public void FormeDuJour_DisperseLesMatchsAuDelaDuHasard()
    {
        double Dispersion(double forme)
        {
            var m = Moteur(ModelePointTests.Config(pente: 0, formeMatch: forme));
            var rng = new Random(10);
            var x = new List<double>();
            for (int i = 0; i < 3_000; i++)
                x.Add(m.Jouer(new Joueur("A", 80), new Joueur("B", 80), FormatMatch.Bo3, Surface.Dur, rng).Service[0].PctPointsGagnes);
            double moy = x.Average();
            return x.Average(v => (v - moy) * (v - moy));
        }
        // La forme d'un joueur monte son % au service et baisse celui de l'adversaire : variance ajoutée ≈ 2 × σ².
        double ajout = Dispersion(0.03) - Dispersion(0);
        Assert.InRange(ajout, 0.7 * 2 * 0.0009, 1.3 * 2 * 0.0009);
    }

    [Fact]
    public void FormuleExacte_ValeursConnues()
    {
        Assert.Equal(0.5, FormulesExactes.ProbaTenirJeu(0.5), 12);
        Assert.Equal(1.0, FormulesExactes.ProbaTenirJeu(1.0), 12);
        Assert.Equal(0.73573, FormulesExactes.ProbaTenirJeu(0.6), 5);
    }
}
