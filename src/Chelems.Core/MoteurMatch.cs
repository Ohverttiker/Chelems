namespace Chelems.Core;

/// <summary>
/// Simule un match point par point (V0.3). Chaque point est joué par <see cref="ModelePoint"/> : duel au service, puis échange.
/// Forme, pression et élan sont retirés en V0.3 (ils reviendront une fois le point stabilisé, voir docs/moteur_v0_3.md) :
/// les lois d'un point sont donc fixes pendant tout le match.
/// Le moteur est sans état : utilisable depuis plusieurs threads, avec un <see cref="Random"/> par thread.
/// </summary>
public sealed class MoteurMatch(ModelePoint modele)
{
    public ModelePoint Modele { get; } = modele;

    /// <param name="premierServeur">0 = A, 1 = B ; tiré au sort si null.</param>
    /// <param name="observateur">Appelé après chaque point (facultatif).</param>
    public ResultatMatch Jouer(Joueur a, Joueur b, FormatMatch format, Surface surface, Random rng,
                               int? premierServeur = null, Action<PointJoue>? observateur = null)
    {
        ArgumentNullException.ThrowIfNull(rng);
        int premier = premierServeur ?? (rng.NextDouble() < 0.5 ? 0 : 1);
        if (premier is not (0 or 1)) throw new ArgumentOutOfRangeException(nameof(premierServeur));
        var partie = new Partie([Modele.Preparer(a, b, surface), Modele.Preparer(b, a, surface)], format, rng, observateur);
        var sets = partie.JouerMatch(premier);
        return new ResultatMatch(a, b, format, surface, premier, sets, partie.Service, partie.Echange, partie.Longueurs);
    }

    /// <summary>État d'un match en cours (une instance par match). duels[i] : lois du joueur i au service.</summary>
    private sealed class Partie(DuelPoint[] duels, FormatMatch format, Random rng, Action<PointJoue>? observateur)
    {
        public readonly StatsService[] Service = [new(), new()];
        public readonly StatsEchange[] Echange = [new(), new()];
        public readonly StatsLongueur Longueurs = new();
        private readonly int[] _jeux = new int[2];
        private int _set;

        public List<ScoreSet> JouerMatch(int premierServeur)
        {
            var sets = new List<ScoreSet>(format.BestOf);
            int serveur = premierServeur;
            int[] setsGagnes = [0, 0];
            while (setsGagnes[0] < format.SetsGagnants && setsGagnes[1] < format.SetsGagnants)
            {
                bool dernierSet = setsGagnes[0] == format.SetsGagnants - 1 && setsGagnes[1] == format.SetsGagnants - 1;
                var score = JouerSet(ref serveur, dernierSet ? format.PointsTieBreakDernierSet : format.PointsTieBreak);
                sets.Add(score);
                setsGagnes[score.Gagnant]++;
                _set++;
            }
            return sets;
        }

        /// <param name="serveur">Serveur du premier jeu ; en sortie, serveur du premier jeu du set suivant.</param>
        private ScoreSet JouerSet(ref int serveur, int pointsTieBreak)
        {
            _jeux[0] = _jeux[1] = 0;
            while (true)
            {
                if (_jeux[0] == 6 && _jeux[1] == 6)
                {
                    var (tbA, tbB) = JouerTieBreak(serveur, pointsTieBreak);
                    serveur = 1 - serveur; // le relanceur du 1er point du tie-break sert le set suivant
                    return tbA > tbB ? new ScoreSet(7, 6, tbA, tbB) : new ScoreSet(6, 7, tbA, tbB);
                }
                bool tenu = JouerJeu(serveur);
                _jeux[tenu ? serveur : 1 - serveur]++;
                serveur = 1 - serveur;
                if ((_jeux[0] >= 6 || _jeux[1] >= 6) && Math.Abs(_jeux[0] - _jeux[1]) >= 2)
                    return new ScoreSet(_jeux[0], _jeux[1]);
            }
        }

        /// <returns>true si le serveur tient son jeu.</returns>
        private bool JouerJeu(int serveur)
        {
            var stats = Service[serveur];
            stats.Jeux++;
            int ps = 0, pr = 0;
            while (true)
            {
                bool balleDeBreak = pr >= 3 && pr > ps;
                bool gagne = JouerPoint(serveur, false, balleDeBreak, ps, pr);
                if (balleDeBreak)
                {
                    stats.BallesDeBreakSubies++;
                    if (gagne) stats.BallesDeBreakSauvees++;
                }
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
                bool gagne = JouerPoint(serveur, true, false, pts[serveur], pts[1 - serveur]);
                pts[gagne ? serveur : 1 - serveur]++;
                for (int j = 0; j < 2; j++)
                    if (pts[j] >= cible && pts[j] - pts[1 - j] >= 2) return (pts[0], pts[1]);
            }
        }

        private bool JouerPoint(int serveur, bool tieBreak, bool balleDeBreak, int ps, int pr)
        {
            var d = ModelePoint.Jouer(duels[serveur], rng);
            Compter(serveur, d);
            observateur?.Invoke(new PointJoue(_set, serveur, d.ServeurGagne ? serveur : 1 - serveur, tieBreak, balleDeBreak,
                                              _jeux[0], _jeux[1], ps, pr, d));
            return d.ServeurGagne;
        }

        private void Compter(int serveur, DeroulementPoint d)
        {
            var s = Service[serveur];
            s.PointsJoues++;
            if (d.ServeurGagne) s.PointsGagnes++;
            if (d.PremiereRentree)
            {
                s.PremieresRentrees++;
                if (d.ServeurGagne) s.GagnesPremiere++;
            }
            else if (d.ServeurGagne) s.GagnesSeconde++;

            int auteur = d.Auteur == 0 ? serveur : 1 - serveur;
            int type = -1; // 0 gagnant, 1 faute provoquée, 2 faute directe (comptage du Match Charting Project)
            switch (d.Fin)
            {
                case FinPoint.Ace: s.Aces++; type = 0; break;
                case FinPoint.DoubleFaute: s.DoublesFautes++; break;
                case FinPoint.ServiceGagnant: Echange[auteur].FautesProvoquees++; type = 1; break;
                case FinPoint.RetourGagnant:
                case FinPoint.Gagnant: Echange[auteur].Gagnants++; type = 0; break;
                case FinPoint.FauteProvoquee: Echange[auteur].FautesProvoquees++; type = 1; break;
                case FinPoint.FauteDirecte: Echange[auteur].FautesDirectes++; type = 2; break;
                case FinPoint.FauteRetour: Echange[auteur].FautesDirectes++; Echange[auteur].FautesDirectesRetour++; type = 2; break;
            }
            s.Issues[d.PremiereRentree ? 0 : 1][(int)IssueDuService(d)]++;

            if (d.Attaquant >= 0)
            {
                int att = d.Attaquant == 0 ? serveur : 1 - serveur;
                bool attGagne = (d.ServeurGagne ? serveur : 1 - serveur) == att;
                Echange[att].Attaques++;
                if (attGagne) Echange[att].AttaquesGagnees++;
                Echange[1 - att].Defenses++;
                if (!attGagne) Echange[1 - att].DefensesGagnees++;
            }

            int t = AnalysePoint.Tranche(d.Coups);
            Longueurs.Points[t]++;
            Longueurs.Coups += d.Coups;
            if (d.ServeurGagne) Longueurs.ServeurGagne[t]++;
            if (type >= 0) Longueurs.Fins[t][type]++;
        }

        /// <summary>Issue du duel au service, retrouvée depuis le déroulement (le moteur de point ne la stocke pas à part).</summary>
        private static IssueService IssueDuService(DeroulementPoint d) => d.Fin switch
        {
            FinPoint.Ace => IssueService.Ace,
            FinPoint.ServiceGagnant or FinPoint.FauteRetour => IssueService.Gagnant,
            FinPoint.RetourGagnant => IssueService.RetourGagnant,
            FinPoint.DoubleFaute => IssueService.DoubleFaute,
            _ => d.EtatApresService switch
            {
                EtatEchange.Attaque => IssueService.Attaque,
                EtatEchange.Neutre => IssueService.Neutre,
                _ => IssueService.Defense,
            },
        };
    }
}
