namespace Chelems.Core.Fidelite;

/// <summary>Une valeur ATP de référence. Valeur en proportion (0-1), sauf pour la dispersion (en points de %).</summary>
public readonly record struct Cible(double Valeur, double Effectif);

/// <summary>
/// Cibles figées de <c>objectifs_equilibrage/</c> (dur, top 150, ATP 2015-2025, Jeff Sackmann).
/// Lues telles quelles : aucune valeur n'est recalculée ni inventée ici.
/// </summary>
public sealed class CiblesAtp
{
    public IReadOnlyDictionary<CleMesure, Cible> Valeurs { get; }

    private CiblesAtp(Dictionary<CleMesure, Cible> valeurs) => Valeurs = valeurs;

    public static CiblesAtp Charger(string dossierObjectifs)
    {
        var c = new Dictionary<CleMesure, Cible>();
        string F(string nom) => Path.Combine(dossierObjectifs, nom);

        foreach (var l in Csv.Lire(F("hold_et_service_par_rang.csv")))
        {
            int bo = l.Entier("best_of");
            string t = l["tranche_rang"];
            c[new("hold", bo, t)] = new(l.Reel("hold_pct") / 100, l.Reel("jeux_service"));
            c[new("pts", bo, t)] = new(l.Reel("points_gagnes_service_pct") / 100, l.Reel("points_service"));
        }

        foreach (var l in Csv.Lire(F("resultats_par_ecart_de_rang.csv")))
        {
            int bo = l.Entier("best_of");
            string e = l["ecart_places"];
            c[new("vict", bo, e)] = new(l.Reel("victoire_mieux_classe_pct") / 100, l.Reel("matchs"));
            c[new("hold_mieux", bo, e)] = new(l.Reel("hold_mieux_classe_pct") / 100, l.Reel("jeux_mieux_classe"));
            c[new("hold_moins", bo, e)] = new(l.Reel("hold_moins_bien_classe_pct") / 100, l.Reel("jeux_moins_bien_classe"));
        }

        foreach (var l in Csv.Lire(F("scores_sets_et_tiebreaks.csv")))
            c[new("score", l.Entier("best_of"), l["mesure"])] = new(l.Reel("valeur_pct") / 100, l.Reel("effectif"));

        foreach (var l in Csv.Lire(F("dependance_sets_bo3.csv")))
        {
            string e = l["ecart_places"];
            c[new("deux_sets", 3, e)] = new(l.Reel("matchs_en_2_sets_pct") / 100, l.Reel("matchs"));
            c[new("meme12", 3, e)] = new(l.Reel("meme_vainqueur_sets_1_et_2_pct") / 100, l.Reel("matchs"));
        }

        foreach (var l in Csv.Lire(F("dispersion_service_par_match.csv")))
        {
            int bo = l.Entier("best_of");
            string e = l["ecart_places"];
            c[new("disp_moy", bo, e)] = new(l.Reel("avantage_moyen_mieux_classe_pts"), l.Reel("matchs"));
            c[new("disp_et", bo, e)] = new(l.Reel("ecart_type_observe_pts"), l.Reel("matchs"));
        }

        // Jeux et points de retour gagnés par tranche de rang (fichier ajouté le 2026-11-18, même périmètre).
        if (File.Exists(F("retour_par_rang.csv")))
            foreach (var l in Csv.Lire(F("retour_par_rang.csv")))
            {
                int bo = l.Entier("best_of");
                string t = l["tranche_rang"];
                c[new("retour", bo, t)] = new(l.Reel("jeux_retour_gagnes_pct") / 100, l.Reel("jeux_retour"));
                c[new("pts_retour", bo, t)] = new(l.Reel("points_retour_gagnes_pct") / 100, l.Reel("points_retour"));
            }

        // Taux de victoire d'un joueur du top 150 selon sa tranche de rang (fichier des adversaires figé, une ligne par joueur et par match).
        var fichierAdv = Directory.GetFiles(dossierObjectifs, "adversaires_*.csv").FirstOrDefault();
        if (fichierAdv is not null)
        {
            var acc = new Dictionary<CleMesure, (double G, double N)>();
            foreach (var l in Csv.Lire(fichierAdv))
            {
                var cle = new CleMesure("gagne", l.Entier("best_of"), Categories.Dans(Categories.TranchesRang, l.Entier("rang_joueur"))!);
                acc.TryGetValue(cle, out var v);
                acc[cle] = (v.G + l.Entier("gagne"), v.N + 1);
            }
            foreach (var (cle, (g, n)) in acc) c[cle] = new(g / n, n);
        }

        // Moyennes du top 150 (V0.3 : premières balles, aces, doubles fautes et points sur 1re et 2e comprises).
        foreach (var l in Csv.Lire(F("service_moyen_top150.csv")))
            c[new("moyen", 0, l["mesure"])] = new(l.Reel("valeur_pct") / 100, double.NaN);

        // Échanges (Match Charting Project, tous les points des matchs « chartés » de la surface, 2015-2025).
        if (File.Exists(F("echanges_mcp.csv")))
            foreach (var l in Csv.Lire(F("echanges_mcp.csv")))
                c[new("mcp", 0, l["mesure"])] = new(l.Reel("valeur_pct") / 100, l.Reel("effectif"));

        // Conversion% et Steal% : moyennes du tour fournies par le créateur (sans effectif), cibles de la seule surface de référence (dur).
        // Le détail de l'efficacité du service (2023) n'est plus une cible : c'est le point de départ des lois du service de chaque
        // surface, ajustées sur les points au service de l'ATP (config/moteur.json, rappel du calage).
        if (File.Exists(F("efficacite_service.csv")))
            foreach (var l in Csv.Lire(F("efficacite_service.csv")).Where(l => l["mesure"] is "conversion" or "steal"))
                c[new("efficacite", 0, l["mesure"])] = new(l.Reel("valeur_pct") / 100, double.NaN);

        return new CiblesAtp(c);
    }
}
