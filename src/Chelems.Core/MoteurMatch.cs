namespace Chelems.Core;

/// <summary>
/// Simule un match point par point (V0.2). Pour chaque point, la probabilité du serveur est :
/// base (talent, surface, styles) + écart de forme des deux joueurs (match + set + élan de set)
/// + pression (enjeu du point) + élan (point précédent du jeu, jeu de service précédent du set), bornée.
/// Le moteur est sans état : utilisable depuis plusieurs threads, avec un <see cref="Random"/> par thread.
/// </summary>
public sealed class MoteurMatch(ModelePoint modele)
{
    public ModelePoint Modele { get; } = modele;
    public ModeleForme Forme { get; } = new(modele.Config);

    /// <param name="premierServeur">0 = A, 1 = B ; tiré au sort si null.</param>
    /// <param name="lenteA">Phase de forme lente de A (simulateur de circuit) ; tirée dans sa loi stationnaire si null.</param>
    /// <param name="enjeu">Enjeu du tournoi, de 0 à 1 (voir ConfigEnjeu) ; 0 par défaut.</param>
    /// <param name="observateur">Appelé après chaque point (facultatif).</param>
    public ResultatMatch Jouer(Joueur a, Joueur b, FormatMatch format, Surface surface, Random rng,
                               int? premierServeur = null, double? lenteA = null, double? lenteB = null,
                               Action<PointJoue>? observateur = null, double enjeu = 0)
    {
        ArgumentNullException.ThrowIfNull(rng);
        int premier = premierServeur ?? (rng.NextDouble() < 0.5 ? 0 : 1);
        if (premier is not (0 or 1)) throw new ArgumentOutOfRangeException(nameof(premierServeur));
        Joueur[] j = [a, b];
        double[] formeMatch =
        [
            Forme.FormeDuMatch(a, lenteA ?? Forme.TirerLente(a, rng), rng, enjeu),
            Forme.FormeDuMatch(b, lenteB ?? Forme.TirerLente(b, rng), rng, enjeu),
        ];
        var partie = new Partie(this, j, [Modele.ProbaBase(a, b, surface), Modele.ProbaBase(b, a, surface)],
                                formeMatch, format, rng, observateur);
        var sets = partie.JouerMatch(premier);
        return new ResultatMatch(a, b, format, surface, premier, sets, partie.Service, formeMatch);
    }

    /// <summary>État d'un match en cours (une instance par match).</summary>
    private sealed class Partie(MoteurMatch moteur, Joueur[] joueurs, double[] base_, double[] formeMatch,
                                FormatMatch format, Random rng, Action<PointJoue>? observateur)
    {
        public readonly StatsService[] Service = [new(), new()];
        private readonly ModelePoint _m = moteur.Modele;
        private readonly double[] _niveauSet = new double[2]; // forme du match + forme du set + élan de set
        private readonly int[] _jeux = new int[2];
        private int _set;

        public List<ScoreSet> JouerMatch(int premierServeur)
        {
            var sets = new List<ScoreSet>(format.BestOf);
            int serveur = premierServeur;
            int[] setsGagnes = [0, 0];
            int? gagnantPrecedent = null;
            while (setsGagnes[0] < format.SetsGagnants && setsGagnes[1] < format.SetsGagnants)
            {
                for (int i = 0; i < 2; i++)
                {
                    double elan = gagnantPrecedent is null ? 0 : (gagnantPrecedent == i ? 1 : -1) * _m.Config.Elan.Set;
                    _niveauSet[i] = formeMatch[i] + moteur.Forme.FormeDuSet(joueurs[i], rng) + elan;
                }
                bool dernierSet = setsGagnes[0] == format.SetsGagnants - 1 && setsGagnes[1] == format.SetsGagnants - 1;
                var score = JouerSet(ref serveur, dernierSet ? format.PointsTieBreakDernierSet : format.PointsTieBreak);
                sets.Add(score);
                setsGagnes[score.Gagnant]++;
                gagnantPrecedent = score.Gagnant;
                _set++;
            }
            return sets;
        }

        /// <param name="serveur">Serveur du premier jeu ; en sortie, serveur du premier jeu du set suivant.</param>
        private ScoreSet JouerSet(ref int serveur, int pointsTieBreak)
        {
            _jeux[0] = _jeux[1] = 0;
            int[] jeuPrecedent = [0, 0]; // +1 tenu, −1 perdu, 0 pas encore servi dans ce set
            while (true)
            {
                if (_jeux[0] == 6 && _jeux[1] == 6)
                {
                    var (tbA, tbB) = JouerTieBreak(serveur, pointsTieBreak);
                    serveur = 1 - serveur; // le relanceur du 1er point du tie-break sert le set suivant
                    return tbA > tbB ? new ScoreSet(7, 6, tbA, tbB) : new ScoreSet(6, 7, tbA, tbB);
                }
                bool tenu = JouerJeu(serveur, jeuPrecedent[serveur]);
                jeuPrecedent[serveur] = tenu ? 1 : -1;
                _jeux[tenu ? serveur : 1 - serveur]++;
                serveur = 1 - serveur;
                if ((_jeux[0] >= 6 || _jeux[1] >= 6) && Math.Abs(_jeux[0] - _jeux[1]) >= 2)
                    return new ScoreSet(_jeux[0], _jeux[1]);
            }
        }

        private static bool GagneLeSet(int jeux, int adverses) => jeux + 1 >= 6 && jeux + 1 - adverses >= 2;

        /// <returns>true si le serveur tient son jeu.</returns>
        private bool JouerJeu(int serveur, int jeuPrecedent)
        {
            var stats = Service[serveur];
            stats.Jeux++;
            int ps = 0, pr = 0, pointPrecedent = 0;
            double elanJeu = jeuPrecedent * _m.Config.Elan.Jeu;
            while (true)
            {
                bool balleDeBreak = pr >= 3 && pr > ps, balleDeJeu = ps >= 3 && ps > pr;
                var type = balleDeBreak && GagneLeSet(_jeux[1 - serveur], _jeux[serveur]) ? TypePoint.BalleDeSetContreServeur
                         : balleDeJeu && GagneLeSet(_jeux[serveur], _jeux[1 - serveur]) ? TypePoint.BalleDeSetPourServeur
                         : balleDeBreak ? TypePoint.BalleDeBreak
                         : balleDeJeu ? TypePoint.BalleDeJeu
                         : ps == pr && ps >= 2 ? TypePoint.Egalite
                         : TypePoint.Autre;
                double extra = elanJeu + pointPrecedent * _m.Config.Elan.Point;
                bool gagne = JouerPoint(serveur, type, extra, false, balleDeBreak, ps, pr);
                if (balleDeBreak)
                {
                    stats.BallesDeBreakSubies++;
                    if (gagne) stats.BallesDeBreakSauvees++;
                }
                pointPrecedent = gagne ? 1 : -1;
                if (gagne) ps++; else pr++;
                if (ps >= 4 && ps - pr >= 2) { stats.JeuxTenus++; return true; }
                if (pr >= 4 && pr - ps >= 2) return false;
            }
        }

        /// <summary>Le joueur dont c'est le tour sert le 1er point, puis on alterne tous les deux points.</summary>
        private (int A, int B) JouerTieBreak(int premierServeur, int cible)
        {
            int[] pts = [0, 0];
            for (int n = 0; ; n++)
            {
                int serveur = n % 4 is 0 or 3 ? premierServeur : 1 - premierServeur;
                int s = pts[serveur], r = pts[1 - serveur];
                var type = r + 1 >= cible && r + 1 - s >= 2 ? TypePoint.BalleDeSetContreServeur
                         : s + 1 >= cible && s + 1 - r >= 2 ? TypePoint.BalleDeSetPourServeur
                         : TypePoint.TieBreak;
                bool gagne = JouerPoint(serveur, type, 0, true, false, s, r);
                pts[gagne ? serveur : 1 - serveur]++;
                for (int j = 0; j < 2; j++)
                    if (pts[j] >= cible && pts[j] - pts[1 - j] >= 2) return (pts[0], pts[1]);
            }
        }

        private bool JouerPoint(int serveur, TypePoint type, double elan, bool tieBreak, bool balleDeBreak, int ps, int pr)
        {
            double p = _m.Borner(base_[serveur] + _niveauSet[serveur] - _niveauSet[1 - serveur] + _m.Pression(type) + elan);
            bool gagne = rng.NextDouble() < p;
            var stats = Service[serveur];
            stats.PointsJoues++;
            if (gagne) stats.PointsGagnes++;
            observateur?.Invoke(new PointJoue(_set, serveur, gagne ? serveur : 1 - serveur, tieBreak, balleDeBreak,
                                              _jeux[0], _jeux[1], ps, pr, type, p));
            return gagne;
        }
    }
}
