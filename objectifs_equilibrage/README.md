# Objectifs d'équilibrage (figés)

Cibles de référence du jeu Chelem, mesurées sur les données ATP réelles. Elles ne se recalculent pas à chaque test : un test de simulation compare ses résultats à ces fichiers. On ne les modifie que sur décision du créateur, en régénérant les fichiers avec le script ci-dessous.

## Périmètre

- **Source :** Jeff Sackmann, dépôt `tennis_atp`, fichiers `atp_matches_2015.csv` à `atp_matches_2025.csv`. Extraction du 2026-11-17.
- **Matchs retenus :** simple messieurs, circuit ATP principal (Grand Chelem, Masters 1000, ATP 500/250, Finals, Jeux olympiques), uniquement sur **dur**. Tout se joue sur dur tant que les autres surfaces ne sont pas modélisées.
- **Exclusions :** Coupe Davis (2 403 matchs) et matchs incomplets, c'est-à-dire abandons, forfaits et disqualifications (622 matchs).
- **Joueurs :** au moins un joueur du **top 150** au moment du match. Le rang utilisé est le rang ATP du jour du match. Il reste 15 754 matchs.
- **Régénération :** `python construire_objectifs.py <dossier tennis_atp> <ce dossier>`.

## Fichiers

| Fichier | Contenu | Unité de comptage |
|---|---|---|
| `service_moyen_top150.csv` | Statistiques de service moyennes : Hold%, points gagnés au service, premières balles, points gagnés sur 1re et sur 2e, aces, doubles fautes, balles de break sauvées | Une ligne par joueur du top 150 et par match |
| `hold_et_service_par_rang.csv` | Hold% et % de points gagnés au service, par tranche de rang et par format (Bo3, Bo5) | Une ligne par joueur du top 150 et par match ; un match entre deux joueurs du top 150 compte deux fois |
| `resultats_par_ecart_de_rang.csv` | Victoire du mieux classé, Hold% du mieux et du moins bien classé, par écart de places et par format | Une ligne par match, quand le mieux classé est dans le top 150 ; l'adversaire peut avoir n'importe quel rang, un joueur non classé compte comme rang 99 999 |
| `scores_sets_et_tiebreaks.csv` | Répartition du nombre de sets par format et part des sets joués au tie-break | Une ligne par match |
| `dependance_sets_bo3.csv` | Bo3 : % de matchs en 2 sets et % de matchs où le vainqueur du 1er set gagne aussi le 2e, par écart de places | Une ligne par match (mieux classé dans le top 150) |
| `controle_extraction.csv` | Effectifs bruts et motifs d'exclusion | — |
| `adversaires_dur.csv` | Rang des adversaires réels de chaque joueur du top 150, avec le format | Sert à tirer les adversaires des saisons simulées |

## Règles d'usage pour les tests

1. **Bo3 et Bo5 séparés.** Les cibles diffèrent selon le format, puisque plus de sets réduit la variance du résultat.
2. **Adversaires réels.** Tout test par tranche de rang tire ses adversaires dans `adversaires_dur.csv`. Sinon, la comparaison est biaisée : en ATP, le n°1 affronte en médiane un joueur classé autour de la 28e place.
3. **Classement simulé aux points.** Les rangs ATP sont bruités par rapport au niveau réel. Un test qui classerait les joueurs simulés par talent pur ne serait pas comparable.
4. **Même unité de comptage.** Chaque test compte comme indiqué dans le tableau des fichiers : par joueur ou par match.

## Cohérence entre les données (constatée le 2026-11-17)

En rejouant des matchs au jeu, avec des points et des jeux indépendants, à partir des Hold% réels du mieux et du moins bien classé, on prédit **plus** de victoires du favori que l'ATP n'en observe :

| Format | Écart du favori, prédit − observé |
|---|---|
| Bo3 | +1 à +5 points |
| Bo5 | +3 à +9 points, surtout au-delà de 20 places d'écart |

Les deux séries de données ne sont donc pas incompatibles. Elles indiquent qu'une même moyenne de Hold% cache des matchs très différents. Les sources possibles sont détaillées plus bas, dans l'ordre où il faut les tester.

1. **Hétérogénéité des oppositions.** Deux joueurs séparés du même écart de classement n'ont pas toujours le même écart de Hold% : les styles s'affrontent plus ou moins bien.
   - La moyenne des Hold% sur beaucoup de matchs écrase cette dispersion.
   - Or la probabilité de victoire n'est pas linéaire en écart de Hold%. Pour un même écart moyen, des matchs dispersés donnent moins de victoires au favori que des matchs identiques.
   - Le passage aux statistiques détaillées (profils, archétypes) doit produire cette dispersion **sans variable dynamique**. C'est le premier mécanisme à tester avant de rouvrir le principe 6.
2. **Classement en retard sur le niveau.** Le rang ATP reflète les 52 dernières semaines.
   - Il sous-estime les jeunes en progression, les joueurs revenant de blessure avec un classement protégé, les qualifiés et les invités.
   - Il surestime les joueurs en déclin ou diminués physiquement.
   - Cet effet joue surtout dans les grands écarts et dans les tournois en Bo3 : qualifications, invitations.
3. **Absence de dépendance entre les points.** Le calcul suppose des jeux indépendants. L'ATP en Bo3 a plus de matchs en 2 sets que ce que le modèle au jeu produit.
   - La cause est peut-être un tie-break qui n'est pas à 50/50 : il dépend du service et du niveau.
   - Elle est peut-être aussi une corrélation d'un set à l'autre, d'origine physique ou mentale.
   - Ce point sera tranché au passage au point par point.
4. **Biais propres au Bo5.**
   - Le Bo5 sur dur, c'est uniquement l'Open d'Australie et l'US Open : chaleur, fatigue sur des matchs longs, conditions plus lentes.
   - Le Hold% du moins bien classé y est mesuré sur des matchs souvent longs.
   - L'échantillon est plus petit : 2 700 matchs, contre 13 054 en Bo3.
5. **Limites de mesure (mineures).**
   - Le nombre de jeux de service (SvGms) diffère du score dans environ 3 % des matchs, d'un jeu en général.
   - Les tie-breaks ne comptent pas comme jeux de service.
   - Les matchs incomplets sont exclus, ce qui retire surtout des défaites de favoris blessés.

## Diagnostic du simulateur de départ (pour mémoire)

Formule actuelle : `76 % ± 0,75 × écart de talent`. Centre trop bas, de 3 à 5 points.

Formule proposée par l'agent d'équilibrage, en attente de validation :

```
hold = 80 % + 0,75 × écart de talent, bornée entre 72 et 88 %
```

Fichiers : `HoldFormule.cs` et `hold_config.json`. Avec cette formule, le Hold% par rang colle à l'ATP, mais le favori gagne encore trop souvent aux grands écarts. C'est cohérent avec les points 1 à 3 ci-dessus.

## Dépendance entre les sets (constatée le 2026-11-17)

En Bo3, même entre joueurs à 0-5 places d'écart, 61,7 % des matchs se jouent en 2 sets et le vainqueur du 1er set gagne aussi le 2e dans 61,8 % des cas. Si les points étaient indépendants avec un niveau fixe sur le match, ces deux chiffres seraient proches de 50 % pour des joueurs égaux. Avec le modèle au point ajusté (voir `ajuster_point.py`), le simulateur ne produit que 57 % de matchs en 2 sets tous écarts confondus, contre 63,5 % en ATP. Cet écart ne peut pas venir de la formule de point : c'est le test chiffré du principe 6 (verdict en cours).

## Terre et gazon (ajoutés le 2026-11-18)

Les sous-dossiers `terre/` et `gazon/` contiennent les mêmes fichiers pour ces surfaces, avec le même périmètre (2015-2025, circuit principal, Coupe Davis et matchs incomplets exclus, au moins un joueur du top 150). Ils sont construits par le portage C# du script : `dotnet run --project outils/Chelems.Donnees -- objectifs Clay|Grass`. Le fichier des adversaires s'y appelle `adversaires_terre.csv` et `adversaires_gazon.csv`.

Le portage, relancé sur le dur, reproduit à l'identique `hold_et_service_par_rang`, `resultats_par_ecart_de_rang`, `scores_sets_et_tiebreaks`, `service_moyen_top150` et `adversaires_dur`. Pour `dependance_sets_bo3` et `dispersion_service_par_match`, dont le script d'origine n'est pas dans le dépôt, il retient quelques matchs de plus, avec au plus 0,3 point d'écart sur les valeurs. Les fichiers sur dur de ce dossier n'ont pas été modifiés.

## Retour par rang (ajouté le 2026-11-18)

`retour_par_rang.csv`, présent dans les trois dossiers : jeux et points de retour gagnés par un joueur du top 150 selon sa tranche de rang, par format. On compte une ligne par joueur du top 150 et par match, comme pour le hold. Ce fichier est construit par le même outil (`objectifs`) et ne modifie aucun autre fichier. Il sert à vérifier que le niveau pèse au retour comme à l'ATP : les meilleurs joueurs se distinguent surtout au retour, en particulier en Bo5.
