# Mesure des rivalités (étape 0), 2026-11-18

**Question :** la dispersion d'un match à l'autre est-elle attachée au duel (styles), au joueur, à la semaine, ou change-t-elle à chaque match ?

**Source :** Jeff Sackmann `tennis_atp`, dur, circuit principal hors Coupe Davis, 2015-2025, matchs complets (15 788 matchs).
**Outil :** `dotnet run --project outils/Chelems.Donnees -- rivalites`. Sorties : `resultats/rivalites/` (rapport, décomposition, rivalités d'au moins 6 matchs).

## Méthode

1. **Niveau du moment** : Elo match par match (méthode FiveThirtyEight, moyenne global/dur), calculé depuis 2005 avec les Challengers et les qualifications. Il suit la progression des jeunes et le déclin des vétérans.
2. **Mesure** : avantage au service = % de points gagnés au service de A − % de B, moins ce que prédit l'écart d'Elo.
3. **Décomposition**, au-delà du hasard des points (variance binomiale retirée) :
   - **duel** : covariance du résidu entre deux matchs de la même paire ;
   - **joueur** : covariance entre deux matchs d'un même joueur contre deux adversaires différents. Une paire en porte deux, d'où l'interaction des styles = duel − 2 × joueur ;
   - **par match** : le reste.

## Résultats

| Bo3, 13 073 matchs | Variance (pts²) | Part |
|---|---|---|
| Au-delà du hasard des points | 88,9 (écart-type 9,4 pts) | 100 % |
| Propre au duel | 3,5 (écart-type 1,9 pt) | 4 % |
| dont joueurs | 2,4 | 3 % |
| dont interaction des styles | 1,1 | 1 % |
| Change à chaque match | 85,4 (écart-type 9,2 pts) | 96 % |

En Bo5, le duel compte pour 7 %. Tous formats confondus, il compte pour 6 %, dont 3 % pour l'interaction des styles.

- **Rivalités d'au moins 6 matchs sur dur (95 paires)** : 24,2 % sont équilibrées (le moins victorieux gagne au moins 40 % des matchs). Si les victoires suivaient seulement l'Elo, on en attendrait 27,3 %. La dispersion des bilans autour de l'Elo est Var(z) = 1,06, où 1 signifie que l'Elo explique tout.
- **Temps** : la covariance du duel vaut 8,6 pts² entre deux matchs à moins d'un an d'écart et 1,8 à 4,3 au-delà. La part proche dans le temps est surtout un niveau que l'Elo capte en retard. L'effet de style durable est donc encore plus petit que 4 %.
- **Même tournoi** : 5,2 pts² dans le même tournoi, contre 3,9 dans deux tournois différents à moins de 60 jours. Les conditions et la forme de la semaine pèsent peu.
- **Styles opposés** (style approché par le % de points gagnés au service − le % au retour) : la variance par match est la même (77 contre 75 pts²), et l'effet du duel est plus faible chez les styles opposés. Sur dur et avec ce découpage grossier, l'hypothèse n'est pas confirmée. L'effet de surface (Nadal-Federer) n'est pas testable sur dur seul.
- **Medvedev-Sinner sur dur** : 6-8, pour 6,9-7,1 attendu par l'Elo. Medvedev est favori à 76 % en 2020 et à 23 % fin 2024. C'est le niveau qui explique la bascule, pas le style.

## Conséquences

1. La V0.1 attache 100 % de la dispersion au duel (σ = 6 points de talent, fixe par paire). Les données en attachent environ 5 %. **La V0.1 est fausse sur ce point**, comme le pressentait le créateur. Un effet de style réaliste vaut environ 2 points d'avantage au service, dont à peine 1 pour les contres purs.
2. Environ 85 à 90 % de la dispersion change à chaque match et ne vient ni du joueur, ni du duel, ni de la semaine. Les styles ne peuvent pas la produire. C'est le test chiffré du principe 6. Deux mécanismes restent possibles, et le choix revient au créateur :
   - **dépendance entre les points à l'intérieur du match** (score, pression, élan) : pas une variable stockée d'un match à l'autre ;
   - **niveau propre au match** (forme du jour, actuellement suspendue).
3. **Test suivant proposé** : le Match Charting Project (point par point) dira si le % au service varie d'un set à l'autre du même match plus que le hasard. Si oui, c'est une dynamique à l'intérieur du match. Si non, c'est un décalage fixe pour tout le match.

Limite : une partie de la part « par match » est l'erreur de l'Elo sur le niveau du jour. Elle ne peut pas venir des styles, donc la conclusion 1 tient.

# Styles et surfaces (étape 0 bis), 2026-11-18

**Question :** les oppositions de style donnent-elles une domination qui dépend de la surface ?
**Outil :** `dotnet run --project outils/Chelems.Donnees -- surfaces [2015|2005]`. Sorties : `resultats/rivalites_surfaces_<année>/`.
**Niveau du moment :** Elo **toutes surfaces**, sans aucune information de surface. L'affinité de surface reste donc dans les résidus, comme elle devra émerger des styles dans le jeu (principe 3).

## Part attachée au duel, par surface

Part de la dispersion au-delà du hasard des points ; la première valeur porte sur 2015-2025, la seconde sur 2005-2025.

| Surface | Matchs | Propre au duel | dont joueurs (affinité) | dont interaction des styles | Change à chaque match |
|---|---|---|---|---|---|
| Dur | 15 788 / 30 154 | 7 % / 6 % | 4 % / 3 % | 3 % / 2 % | 93 % / 94 % |
| Terre | 8 242 / 16 803 | 13 % / 12 % | 7 % / 8 % | 5 % / 4 % | 87 % / 88 % |
| Gazon | 3 077 / 6 033 | 18 % / 16 % | 12 % / 13 % | 6 % / 3 % | 82 % / 84 % |

## Le duel change-t-il de surface en surface ?

- **Paire** : covariance de 7,0 à 7,5 pts² entre deux matchs sur la même surface, de −0,5 à 2,3 pts² sur deux surfaces différentes. L'avantage d'un joueur sur un autre **ne se transporte pas** d'une surface à l'autre.
- **Joueur** : 2,2 pts² sur la même surface, de −0,6 à −0,2 pts² sur deux surfaces différentes. **L'affinité de surface d'un joueur** a un écart-type d'environ 1,6 point d'avantage au service.
- **Interaction des styles propre à une surface**, une fois retirées les deux affinités : de 0,4 à 1,8 pts², 2 % au plus. Ce qui dépend de la surface dans un duel, c'est presque entièrement **l'affinité de surface de chacun des deux joueurs**, pas un contre propre à la paire.

## Style serveur ou relanceur

Le style est approché par le % de points gagnés au service moins le % au retour, centrés par surface. Il est mesuré sur les autres matchs du joueur, pour éviter la circularité.

| Surface | Pente de l'avantage, par point d'écart de style | Pente de la victoire, par point d'écart de style |
|---|---|---|
| Gazon | +0,14 à +0,19 (± 0,03) | +0,5 à +0,6 point de % (± 0,1) |
| Dur | +0,07 à +0,09 (± 0,02) | +0,1 à +0,2 |
| Terre | −0,03 à −0,04 (± 0,03) | −0,1 |

Le sens est celui attendu : le serveur gagne sur gazon, le relanceur sur terre. À un écart-type de style (6 points), c'est environ 3,5 points de % de victoire sur gazon. Ce découpage n'explique qu'environ 12 % de l'affinité sur gazon : le reste doit venir d'autres traits (effet, déplacements, taille…).

## Rivalités qui basculent

Paires vues au moins 3 fois sur deux surfaces : 97 comparaisons sur 2015-2025, 366 sur 2005-2025.
- **Dispersion** : (z₁ − z₂)²/2 vaut en moyenne 1,24 à 1,25, alors que 1 signifierait aucun effet de surface. 8 à 9 % des comparaisons sont significatives au seuil de 5 %.
- Les bascules existent mais restent rares, et les plus fortes sont portées par une affinité extrême :
  - Nadal–Djokovic (2005-2025) : terre 18-9 (12,8-14,2 attendu), dur 7-20 (13,7-13,3 attendu) ;
  - Nadal–Federer : terre 14-2 (7,0-9,0 attendu), dur 8-11 (8,6-10,4 attendu).

## Conséquences pour le jeu

1. **Les styles doivent agir surtout à travers la surface** (affinité joueur × surface, d'un écart-type d'environ 1,6 pt d'avantage au service, plus forte sur gazon et sur terre). La part de contre pur entre deux styles reste petite : d'environ 1,5 à 2,2 points d'écart-type d'avantage au service.
2. **Cohérence avec la roue de contres** (CLAUDE.md §7, un contre bat sa cible dans 55 à 62 % des matchs) : 2 points d'avantage au service valent environ 60 points d'Elo, soit environ 57 % de victoires. La cible de la roue correspond donc à un contre d'un à deux écarts-types de l'interaction mesurée. Elle est compatible avec les données, à condition que les contres soient des oppositions marquées et pas la règle.
3. **Les bascules à la Nadal–Djokovic** sortent naturellement d'une forte affinité de surface. Il n'y a pas besoin de règle par paire (principe 1).
4. De 82 à 94 % de la dispersion change toujours à chaque match, sur toutes les surfaces. Le test point par point reste nécessaire.

**Limites :**
- Le gazon reste un petit échantillon.
- L'approximation du style est grossière.
- L'Elo toutes surfaces d'un joueur dépend de son calendrier : Nadal, qui jouait beaucoup sur terre, a un Elo global gonflé, donc un résidu négatif sur dur.

# Test point par point : élan et pression (étape 0 ter), 2026-11-18

**Question :** la part de la dispersion qui change à chaque match (85 à 90 %) vient-elle d'une dynamique à l'intérieur du match (élan, pression) ou d'un niveau fixe pour tout le match ?
**Source :** Jeff Sackmann, Match Charting Project, hommes. 20 695 sets complets (dur 12 948, terre 5 082, gazon 2 665), dont 7 341 matchs entièrement notés.
**Outil :** `dotnet run --project outils/Chelems.Donnees -- points`. Sorties : `resultats/point_par_point/`.

**Méthode.** Chaque mesure est comparée à une référence où les points sont indépendants, rejouée 20 fois avec les vraies règles :
- **A** (variation d'un set à l'autre) : le match est rejoué avec un % au service constant sur tout le match ;
- **B4** (élan entre sets) : le match est rejoué avec les niveaux réels de ses sets, dans un ordre tiré au hasard ;
- **B1-B3 et C** : chaque set est rejoué avec le % au service des deux joueurs dans ce set, et les statistiques sont centrées sur le joueur dans son set.

La référence absorbe ainsi les effets mécaniques : règles d'arrêt des jeux, sélection par le score (un match en 3 sets a forcément deux premiers sets partagés), joueur en forme qui enchaîne les points. Un premier passage sans ces corrections donnait des valeurs fausses (« élan » de −13 points ou de +6,5 points).

## A. Le niveau change-t-il pendant le match ?

| Mesure | Valeur |
|---|---|
| Variance de l'avantage au service d'un set à l'autre | observé 96,7 pts², référence 53,2 ± 2,2 |
| Variation réelle du niveau d'un set à l'autre | 43,6 pts², soit un écart-type de 6,6 pts d'avantage au service ou de 3,8 pts de % au service par joueur |
| Contribution à la variance d'un match entier | 17 pts², contre environ 85 pts² par match après Elo (ATP, dur) |

**Environ 80 % de la dispersion d'un match à l'autre est un niveau fixe pour tout le match. Environ 20 % varie à l'intérieur du match.**

## B. Élan (écart à la référence d'indépendance)

| Test | Écart | Lecture |
|---|---|---|
| B1. Point gagné après point gagné − après point perdu (même jeu) | **+0,96 ± 0,10** pt de % | élan réel, faible |
| B2. Jeu tenu après jeu de service tenu − après break subi | **+1,2 ± 0,3** pt de % | élan réel, faible |
| B3. Jeu tenu juste après avoir breaké | −0,1 ± 0,2 | aucun effet de « confirmation du break » |
| B4. Vainqueur d'un set : avantage au set suivant − au set précédent | **+1,7 ± 0,2** pt d'avantage | élan entre sets, ou tendance sur le match (fatigue, adaptation) : la mesure ne sépare pas les deux |

## C. Pression : % de points gagnés par le serveur, écart à son niveau dans le set

| Situation | Écart |
|---|---|
| Balle de set ou de match pour le serveur | **−1,1 ± 0,4** |
| Points de tie-break | **−1,0 ± 0,2** |
| Balle de break | **−0,5 ± 0,2** |
| Balle de set ou de match contre le serveur | +0,3 ± 0,4 (non significatif) |
| Balle de jeu | +0,8 ± 0,1 |
| 30-30 et égalité | +0,6 ± 0,1 |
| Autres points | −0,2 |

Le serveur perd environ 1 point de % quand il peut conclure un set ou jouer un tie-break, et un demi-point sur balle de break. Ce sont des effets moyens sur tout le circuit, qui mêlent le serveur et le relanceur. La Résilience de CLAUDE.md fera varier ces effets d'un joueur à l'autre.

## Verdict

1. **L'élan et la pression existent mais pèsent peu** : de l'ordre de 1 point de % au service chacun. Ils façonnent des statistiques fines (balles de break sauvées, tie-breaks, séries), mais ils ne peuvent pas produire la dispersion observée entre deux matchs.
2. **La dispersion vient pour l'essentiel d'un niveau propre au match** : environ 80 % de la part qui change à chaque match. Ce niveau n'est attaché ni à la paire (environ 5 %, étape 0), ni à la semaine (étape 0), ni aux sets (A). C'est le test chiffré du principe 6 : **la variance émergente des points ne suffit pas, et aucun mécanisme déjà présent ne l'explique.** La décision revient au créateur : une « forme du jour » bornée, d'un écart-type d'environ 8 points d'avantage au service sur le duel, ou une autre source par match encore à identifier (conditions du match, balles, heure, chaleur…).
3. **Ordre de grandeur pour le moteur** :
   - **niveau du match** : environ 68 pts² de variance d'avantage au service sur le duel ;
   - **variation d'un set à l'autre** : 3,8 pts de % par joueur ;
   - **élan** : environ +1 point de % au point et au jeu, +1,7 pt d'avantage entre sets ;
   - **pression** : de −0,5 à −1,1 point de % pour le serveur sur les points décisifs.

**Limites :**
- Le Match Charting Project surreprésente les grands matchs et les meilleurs joueurs, et les bénévoles choisissent peut-être plus souvent des matchs à rebondissements.
- La comparaison avec les 85 pts² mélange deux sources : l'ATP sur dur pour les 85 pts², toutes les surfaces du Match Charting Project pour les 17 pts².
- La référence de B4 rétrécit le niveau de chaque set vers celui du match (méthode bayésienne). C'est une approximation.

# Forme, confiance et constance (étape 0 quater), 2026-11-18

**Question :** le niveau propre au match est-il une forme qui dure (Tiafoe), une confiance alimentée par les résultats (Ruud), et certains joueurs sont-ils plus irréguliers que d'autres (stat Constance) ?
**Source :** Jeff Sackmann `tennis_atp`, circuit principal + Challengers + qualifications, hors Coupe Davis, dur/terre/gazon, 2010 à mai 2026. 168 908 matchs complets avec statistiques de service.
**Outil :** `dotnet run --project outils/Chelems.Donnees -- forme`. Sorties : `resultats/forme/`.

**Méthode.**
- **Performance** d'un joueur dans un match : son avantage au service moins ce que prédit le niveau du moment, orienté vers lui.
- **Deux niveaux de référence** :
  - R1 = Elo du moment (global + surface). Il suit les séries, donc il en absorbe une partie ;
  - R2 = le même Elo, figé au début de la saison. Il n'absorbe rien, mais il laisse dans la performance la progression réelle pendant la saison.
- La vérité se situe entre R1 et R2.

## 1. Persistance de la forme (corrélation de la performance d'un joueur entre deux de ses matchs)

| Écart | ρ (R1) | ρ (R2) |
|---|---|---|
| 1 match | 0,13 | 0,17 |
| 2-3 matchs | 0,11-0,12 | 0,22 |
| 5 matchs | 0,08 | 0,19 |
| 10 matchs | 0,04 | 0,12 |
| 20 matchs | 0,03 | 0,08 |
| 50 matchs | 0,03 | 0,04 |
| 100 matchs | 0,01 | 0,02 |
| Même tournoi | 0,12 | 0,17 |
| Autre tournoi, moins de 15 jours | 0,10 | 0,22 |
| 45 à 120 jours | 0,04 | 0,14 |
| 1 à 2 ans | 0,02 | 0,03 |

- **La forme a deux étages.** Environ 80 % est tiré à neuf à chaque match. Environ 15 à 20 % est une phase qui dure : elle s'efface en 10 à 20 matchs, soit 1 à 3 mois, avec une persistance de 0,88 à 0,94 d'un match au suivant.
- **Ce sont les phases à la Tiafoe**, mais elles pèsent moins que l'aléa du jour.
- **Le tournoi en cours n'a rien de particulier** : il ressemble autant au précédent que deux tournois proches. Le « déclic » de Ruud n'est donc pas un mécanisme général : c'est une variation de la phase en cours.

## 2. Confiance alimentée par les résultats

On prend des matchs serrés, avec un set décisif joué. On compare le vainqueur et le perdant à qualité de jeu égale dans ce match. Contrôle placebo : on fait la même comparaison sur le tournoi d'**avant**. Une forme qui dure donnerait alors le même écart avant et après ; seul l'effet du résultat fait la différence.

| Effet d'avoir gagné le match serré | Performance (pts d'avantage) | Victoire (pts de %) |
|---|---|---|
| Tournoi suivant | −0,86 ± 0,10 | −1,6 ± 0,3 |
| Tournoi précédent (placebo) | −0,69 ± 0,07 | −2,5 ± 0,2 |
| **Effet propre du résultat** | **−0,17 ± 0,12** | **+0,8 ± 0,4** |

**Aucun effet mesurable sur le jeu**, et à peine un effet sur les victoires, à la limite du significatif. La forme évolue d'elle-même : gagner ne rend pas meilleur au tournoi suivant.

## 3. Constance comme trait du joueur

- **Variance de forme propre à un joueur** (adversaires et hasard des points retirés, calcul itératif) : 55 pts² en moyenne, soit un écart-type d'environ 7,4 pts d'avantage au service.
- **Fiabilité matchs pairs / impairs** : r = 0,35 ± 0,04, et r = 0,29 à niveau égal. **C'est un vrai trait, stable, de force moyenne.** L'écart-type entre joueurs est d'environ 16 pts² autour de 55 : les joueurs vont d'environ 25 à 85 pts², soit une forme d'environ 5 à 9 pts d'écart-type.
- **Les meilleurs sont plus réguliers** : la corrélation entre la variance et l'Elo est de −0,35, comme le pensait le créateur.
- **Asymétrie** (très mauvais jours contre très bons jours) : fiabilité de 0,18 ± 0,04. Le trait est faible, et la plupart des joueurs sont presque symétriques.

Les joueurs cités, rangés de 1 (le plus régulier) à 707. Chaque estimation individuelle a une erreur-type de 6 à 14 pts² :

| Joueur | Variance propre | Rang |
|---|---|---|
| Federer | 11 | 7 |
| Tiafoe | 12 | 8 |
| Alcaraz | 14 | 12 |
| Sinner | 14 | 13 |
| Rublev | 15 | 15 |
| Djokovic | 17 | 18 |
| Nadal | 23 | 35 |
| Tsitsipas | 28 | 64 |
| Halys | 35 | 132 |
| Paul | 36 | 138 |
| Zverev | 37 | 141 |
| Bublik | 37 | 151 |
| Rune | 42 | 195 |
| Medvedev | 44 | 223 |
| Kyrgios | 54 | 349 |
| Ruud | 61 | 431 |

**Attention à la lecture individuelle :**
- Avec une fiabilité de 0,35, un classement individuel est surtout du bruit, sauf aux extrêmes.
- **Biais de style** : la mesure repose sur le service. Les gros serveurs (Karlovic, Mpetshi Perricard) paraissent réguliers parce que leur service varie peu.
- **Tiafoe** paraît très régulier d'un match à l'autre. Ses phases relèvent de la persistance (section 1), que cette mesure ne capte pas. Son été 2026 n'est pas encore dans les données, qui s'arrêtent à fin mai 2026.
- **Medvedev** à Monte-Carlo 2026 (0-6 0-6 contre Berrettini) : performance de −51 pts, la plus mauvaise de ses 14 derniers matchs, de loin.

## Verdict (principe 6)

1. **Une forme propre au match est nécessaire**, et elle est mesurée.
   - **Phase qui dure** : environ 15 à 20 % de la forme. Elle persiste d'un match à l'autre (0,9 environ par match) et s'efface en 1 à 3 mois.
   - **Aléa du jour** : environ 80 % de la forme, tiré à neuf à chaque match.
   - Ensemble, elles valent environ 55 à 60 pts² d'avantage au service par joueur.
2. **La stat Constance a une base chiffrée** : elle multiplie la variance de forme d'un joueur d'environ 0,5 à 1,5. Elle est liée au niveau, mais pas seulement.
3. **La confiance alimentée par les résultats n'est pas confirmée.** CLAUDE.md la décrit comme « [Décidé pour le principe] ». **C'est un conflit avec les données ATP, à trancher par le créateur** (principe 5).
4. **L'asymétrie « rarement négative » est un trait faible** : à laisser de côté au départ (principe 6, sobriété).

**Limites :**
- La performance est mesurée par le service, ce qui crée le biais de style décrit plus haut.
- Les statistiques des Challengers sont moins fiables.
- La vraie persistance se situe entre R1 et R2.
- Les matchs de Roland-Garros 2026 n'ont pas encore de statistiques dans les données.

# Enjeu du tournoi (étape 0 quinquies), 2026-11-18

**Question :** les meilleurs élèvent-ils leur niveau en Grand Chelem ? Si oui, est-ce dû aux adversaires (piste 3), à une forme « effacée » (piste 1) ou à une meilleure résilience à la pression (piste 2) ?
**Source :** Jeff Sackmann `tennis_atp`, circuit principal hors Coupe Davis, 2010 à mai 2026, toutes surfaces, 41 047 matchs complets avec statistiques.
**Outil :** `dotnet run --project outils/Chelems.Donnees -- enjeu`. Sorties : `resultats/enjeu/`.
**Performance** : résultat du match au-delà de ce que prédit l'Elo du moment (global + surface), calibré par format. L'Elo neutralise le niveau de l'adversaire.

## Performance au-delà de l'Elo (victoire, en points de %)

| Tranche (rang du jour) | Grand Chelem | Masters 1000 | ATP 250/500 |
|---|---|---|---|
| n°1 | **+3,7 ± 1,3** | −1,0 ± 1,7 | **−3,2 ± 2,4** |
| 2-5 | +1,6 ± 0,9 | +1,3 ± 1,1 | +0,4 ± 1,1 |
| 6-10 | +1,7 ± 1,1 | −0,2 ± 1,2 | +2,3 ± 0,9 |
| 11-20 et au-delà | entre −1,5 et +0,4 | entre −0,7 et +0,9 | entre −0,4 et +0,2 |

L'effet est **concentré sur le n°1** : environ +7 points de victoire en Grand Chelem par rapport aux ATP 250/500.

## Piste 3 : les adversaires

À écart d'Elo égal (stratification par tranches d'écart), l'écart entre Grand Chelem et Masters 1000 **reste entier** : n°1 **+5,0 ± 2,3** points de victoire, 6-10 +2,1 ± 1,6, 2-5 +0,4 ± 1,4.
- Les adversaires sont bien plus faibles en Grand Chelem : rang médian 32 à 62, contre 25 à 35 en Masters 1000.
- Mais l'Elo en tient compte, et la simulation de masse tire déjà ses adversaires dans la répartition réelle par format.
- **Les adversaires n'expliquent donc pas l'effet.**

## Piste 1 : la forme effacée

Variance de la performance au-delà du hasard, et part des très mauvais jours (avantage au service inférieur à −12 pts) :

| Tranche | Grand Chelem | Masters 1000 | ATP 250/500 |
|---|---|---|---|
| n°1 | 38 pts², 6,6 % | 58 pts², 14,6 % | 71 pts², 16,5 % |
| 2-5 | 39, 8,4 % | 81, 13,1 % | 69, 14,5 % |
| 6-10 | 52, 8,8 % | 99, 15,5 % | 67, 12,9 % |
| 101+ | 74, 13,3 % | 90, 14,5 % | 100, 16,5 % |

- **Les meilleurs ont deux fois moins de variance et environ moitié moins de très mauvais jours en Grand Chelem.**
- Le format Bo5 n'explique qu'une baisse d'environ 18 %, celle que l'on voit chez les 101e et au-delà.
- En revanche, la forme des 45 jours précédents pèse autant en Grand Chelem qu'en Masters 1000 (pente 0,10 dans les deux cas). Les meilleurs ne repartent donc pas de zéro : c'est **l'aléa du jour** qui se réduit, pas la phase de forme.

## Piste 2 : résilience à la pression

Balles de break sauvées moins le % de points gagnés au service du même match, et l'équivalent au retour.
- À l'ATP, l'écart va de +1,9 pour le n°1 à −2,9 pour les 101e et au-delà.
- **La V0.2 reproduit ce même écart sans aucune résilience** : −0,1 à −2,9 au service, +3,5 à +1,6 au retour, sur dur. Il est mécanique : les balles de break se concentrent dans les sets où le serveur est faible, et le niveau du joueur joue aussi.
- Au plus, le n°1 garde environ 1 point de mieux au service (+1,2 en moyenne contre −0,1). C'est non significatif.
- **Pas de résilience mesurable selon le niveau, ni selon l'enjeu.**

## Verdict

1. **L'enjeu agit en réduisant l'aléa du jour des meilleurs dans les grands tournois**, et non en ajoutant un bonus de niveau. Moins de mauvais jours suffit à faire gagner davantage un favori, d'où le surplus du n°1 en Grand Chelem.
2. **Le n°1 se relâche dans les ATP 250/500** (−3,2 points de victoire, −2,1 pts d'avantage) : c'est la priorisation des tournois, la limite n°1 du simulateur de circuit.
3. **La pression n'explique rien** de cet effet. La Résilience reste une stat individuelle à calibrer plus tard, à partir des écarts entre joueurs, pas entre tranches.
