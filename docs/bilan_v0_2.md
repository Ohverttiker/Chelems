# Bilan du premier moteur (V0.2) et plan d'action — 2026-11-19

Ce document résume les sessions des 18 et 19 novembre 2026. Le détail est dans `docs/mesure_rivalites.md` (mesures sur les données ATP) et dans `docs/moteur_v0_2.md` (le moteur). Le code est sur la branche `moteur-v0.2`.

---

## 1. Les chiffres visés

**Source unique** : les données ATP de Jeff Sackmann, figées dans `objectifs_equilibrage/` (dur à la racine, `terre/` et `gazon/`).
- **Périmètre** : 2015-2025, circuit principal, Coupe Davis et abandons exclus, au moins un joueur du top 150.
- **Volume** : 15 754 matchs sur dur, 8 149 sur terre, 3 036 sur gazon.

| Famille | Ce qu'elle mesure | Exemple (dur) |
|---|---|---|
| Moyennes du top 150 | jeux de service tenus (hold), points gagnés au service, balles de break sauvées | 80,7 % ; 64,5 % ; 61,7 % |
| Hold et points au service par tranche de rang | le niveau se voit-il au service ? | n°1 87,4 % → rangs 101-150 77,9 % |
| Retour par tranche (`retour_par_rang.csv`) | le niveau se voit-il au retour ? | n°1 29,5 % de jeux de retour gagnés → rangs 101-150 18,3 % |
| Victoires par tranche (`gagne`) | le talent pèse-t-il assez à chaque niveau ? | n°1 83,5 % en Bo3, 92,8 % en Bo5 |
| Victoire du mieux classé par écart de places | les surprises | 0-5 places 55 % → 161 places et plus 75 % |
| Scores | sets par match, part de sets au tie-break | Bo3 : 63,5 % en 2 sets ; 20 % de sets au tie-break |
| Dépendance des sets | même vainqueur aux sets 1 et 2 | 61,8 % entre joueurs proches |
| Dispersion par match | écart-type de l'avantage au service | environ 13 points |

**Cibles qui ne sont pas encore visées**, faute de mécanique dans le moteur : premières balles (61,4 %), aces (8,9 %), doubles fautes (3,8 %), points gagnés sur 1re et 2e balle (73 % / 51 %), longueur des échanges, coups gagnants et fautes, shot quality. Les valeurs sont déjà dans `service_moyen_top150.csv`.

## 2. Ce qui est atteint

Simulation de masse : environ 100 000 matchs par surface, graine 1, adversaires tirés dans la répartition ATP réelle. Les statuts comptent 102 à 105 « ok », 20 à 23 « limite » et 25 à 30 « écart » sur environ 150 mesures par surface.

| Mesure | Dur, ATP → simulé | Terre | Gazon |
|---|---|---|---|
| Hold moyen | 80,7 → 79,7 | 76,7 → 76,3 | 83,9 → 83,1 |
| Points au service | 64,5 → 63,7 | 62,2 → 61,8 | 66,3 → 65,8 |
| Balles de break sauvées | 61,7 → 61,4 | 60,2 → 59,7 | 64,1 → 63,3 |
| Hold n°1 / rangs 101-150, Bo3 | 87,4 / 77,9 → 87,4 / 77,7 | bon de 2-5 à 101-150 | bon partout |
| Victoires des rangs 2-5, Bo3 / Bo5 | 72,9 / 82,2 → 74,5 / 82,6 | 76,8 → 78,2 en Bo3 | bon |
| Victoire du mieux classé, 11 à 160 places | à moins de 2 points | à moins de 2,5 points | bon |
| Matchs en 2 sets, Bo3 | 63,5 → 64,8 | 62,4 → 65,1 | 62,0 → 65,3 |
| Bo5 en 3 / 4 / 5 sets | 45,9 / 34,0 / 20,1 → 46,6 / 31,2 / 22,2 | proche | 46,9 / 32,4 / 20,7 → 46,4 / 31,5 / 22,1 |
| Même vainqueur aux sets 1 et 2, 0-5 places | 61,8 → 59,9 | 63,6 → 62,2 | bon |
| Dispersion par match, 0 à 80 places | 12,9-13,2 → 13,4-13,9 | 13,1-13,6 → 14,1-14,4 | à peu près |

**Conclusions validées en cours de route :**
- **Le talent pèse autant à tous les niveaux.** Un écart de 5 points donne 62 % de victoires, 10 points 73 %, 20 points 89 % en Bo3 (64 %, 77 % et 92 % en Bo5). Ces valeurs sont les mêmes à 95-90 qu'à 70-65.
- **Les rivalités ne sont plus figées.** Deux joueurs proches se partagent leurs matchs, comme à l'ATP.
- **Les trois surfaces** sont calibrées avec les mêmes joueurs ; seule la physique de la surface change.

## 3. Ce qui n'est pas atteint

| Écart | Valeur, ATP → simulé | Cause probable | Où le corriger |
|---|---|---|---|
| **Le n°1 ne domine pas assez** | dur : victoires 83,5 → 77,7 % (Bo3), 92,8 → 86,4 % (Bo5) ; jeux de retour gagnés en Bo5 33,7 → 29,4 % | Individus d'exception (surtout Djokovic) : meilleur retour, et surtout points importants mieux convertis. Un seul talent ne peut pas le représenter. | Stats **Retour** et **Résilience** (V0.3) |
| Favori trop fort à 161 places et plus | 74,6 → 86,2 % (dur) | Population de test : les non-classés sont ramenés au dernier rang simulé, alors qu'en vrai ce sont des qualifiés, invités ou classements protégés | Population de test |
| Favori pas assez fort à 0-10 places (Bo3) et à 21-80 places (Bo5) | 55,1 → 50,8 % ; Bo5 69,8 → 61,5 % | Lien rang ↔ talent du simulateur de circuit, plus « plat » que l'ATP | Population de test (créateur) |
| Dispersion et dépendance des sets aux grands écarts | écart-type 13,2 → 15,0 ; même vainqueur 68 → 78 % | Même cause que les 161 places | Population de test |
| Tie-breaks sur gazon (Bo3) | 24,7 → 21,2 % | Physique de la surface (vitesse, retour) | Court pace index |
| Meilleurs joueurs en Bo3 sur gazon | jeux de retour gagnés du n°1 15 → 29 % | Tournois de préparation : les meilleurs se ménagent (petit échantillon) | Priorisation des tournois (simulateur de circuit) |
| Tranche 6-10 sur dur | hold 85,6 → 84,0 % | Talents de la population sur cette tranche | Population de test |

Trois tests de fidélité sont ignorés exprès, parce qu'ils documentent ces écarts. Les 88 autres passent.

## 4. Comment marche le moteur

Un match est simulé **point par point**, avec les règles du tennis : jeux, tie-break à 6-6, sets, Bo3 ou Bo5. Pour chaque point, le serveur gagne avec une probabilité :

```
p = borne( centre(surface)
         + pente × (talent du serveur − talent du relanceur)
         + affinité de surface du serveur − celle du relanceur
         + contre de styles
         + niveau du jour du serveur − celui du relanceur
         + pression selon l'enjeu du point
         + élan )
```

| Élément | Ce qu'il représente | Valeur | Effet sur les résultats |
|---|---|---|---|
| **Talent** (50-99) | le niveau | 0,34 point de % au service par point de talent | Un écart de 10 points fait passer de 50 à 73 % de victoires en Bo3 ; c'est l'effet principal |
| **Surface** | la physique de la balle | centre : dur 64,1 %, terre 62,0 %, gazon 66,1 % | Plus de jeux tenus sur gazon (83 %) que sur terre (76 %) |
| **Style caché** (2D, fixe) | le profil, en attendant les stats | affinité de surface : écart-type 0,7 / 0,9 / 1,1 pt ; contre : 0,8 à 1,1 pt | Bascules d'une surface à l'autre (affinité : terre et gazon opposés) ; petits contres entre styles (2 à 5 % de la dispersion) |
| **Forme du match** | l'aléa du jour + une phase qui dure | écart-type 2,1 pts ; 20 % de la variance persiste d'un match à l'autre (0,9 par match) | **Première source de surprises** : environ 80 % de la dispersion d'un match à l'autre |
| **Forme du set** | les hauts et les bas pendant le match | 2,1 pts par set | Sets moins prévisibles, matchs en 3 ou 5 sets |
| **Constance** (stat 1-99) | la régularité d'un joueur | multiplie la variance de forme de ×1,5 à ×0,5 | Les réguliers (Federer, Sinner) ont moins de mauvais jours |
| **Enjeu du tournoi** | le Grand Chelem | aléa du jour réduit jusqu'à moitié selon la Constance | Moins de mauvais jours en Grand Chelem ; effet faible sur les victoires |
| **Pression** | l'enjeu du point | de −1,1 pt (balle de set pour le serveur) à +0,8 pt (balle de jeu) | Façonne les balles de break sauvées et les tie-breaks |
| **Élan** | les séries | ±0,5 pt après le point précédent, ±0,2 pt après le jeu précédent, ±0,4 de niveau après un set | Effet faible, mesuré ; un peu plus de séries |

**Ce que le moteur ne prend pas en compte** :
- la confiance alimentée par les résultats (non confirmée par les données) ;
- la résilience individuelle ;
- les stats détaillées (service, retour, coups) ;
- la taille, la latéralité, l'effet ;
- le court pace index par tournoi ;
- la fatigue ;
- la structure du point (service, échange, attaque ou défense).

**Outils** :
- `dotnet run --project src/Chelems.Console -- match | fidelite | calibrer | bareme` ;
- `dotnet run --project outils/Chelems.Donnees -- objectifs | rivalites | surfaces | points | forme | enjeu`.

## 5. Plan d'action

### Court terme (prochaines sessions)

1. **Mettre CLAUDE.md à jour** avec les décisions de la session. C'est au créateur de valider :
   - forme du jour et Constance réactivées (principe 6 satisfait) ;
   - confiance non confirmée par les données (conflit avec « [Décidé pour le principe] ») ;
   - enjeu du tournoi porté par la Constance ;
   - surfaces calibrées.
2. **Relecture par l'agent de relecture** de la branche `moteur-v0.2`, puis fusion dans `main`.
3. **Corriger la population de test** pour lever les écarts aux grands et aux petits écarts de rang :
   - tirer les adversaires non classés dans une loi de talent réaliste (qualifiés, invités) ;
   - comparer le lien rang ↔ talent du simulateur de circuit à l'Elo réel par rang.

   Cela décidera si le problème vient de la population ou du simulateur de circuit.
4. **V0.3 : détacher Retour et Service du talent** (le talent devient la moyenne pondérée des stats, avec un poids visé de 1/N par stat) :
   - cibles : jeux et points au retour par tranche (déjà en place), victoires du n°1 ;
   - premier test : un profil « grand relanceur » doit reproduire le retour du n°1 en Bo5 (33,7 %).
5. **Résilience** : mesurer joueur par joueur les balles de break et les tie-breaks au-delà de ce que prédit la simulation, puis en faire une stat qui module la pression. Seule la mesure individuelle compte : la mesure par tranche de rang est mécanique.

### Moyen terme

6. **Découper le point** : duel service-retour, puis attaque / neutre / défense, puis conversion et vol de point (« steal »). Données nécessaires : l'efficacité au service de l'ATP, à fournir par le créateur ou par l'agent données ATP. Le Match Charting Project permettra de recouper.
7. **Service détaillé** : premières balles, aces, doubles fautes, points sur 1re et 2e balle. Les cibles sont déjà disponibles, avec la capacité et le choix de jauge décrits dans CLAUDE.md §6.3.
8. **Attributs neutres et surfaces** : taille, latéralité, revers à une main, puis l'Effet avec le **court pace index** (il faut trouver une source de données par tournoi). Le style caché disparaît au profit de ces attributs.
9. **Simulateur de circuit** :
   - brancher le moteur (une phase de forme lente par joueur) ;
   - ajouter la priorisation des tournois (les meilleurs se ménagent en 250/500 et sur gazon avant Wimbledon) et la fatigue ;
   - c'est la limite n°1 du simulateur actuel, désormais chiffrée (n°1 : −3,2 points de victoire en 250/500, +3,7 en Grand Chelem).
10. **Roadmap par jalons** (décision ouverte n°13) : chaque étape ajoute une stat ou une phase et doit garder la fidélité ATP sur les trois surfaces (principe 5).
