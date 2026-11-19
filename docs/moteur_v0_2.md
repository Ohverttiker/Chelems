# Moteur de match V0.2

Corrige la V0.1 à partir des mesures de `docs/mesure_rivalites.md`. La V0.1 attachait toute la dispersion au duel (rivalités figées) ; la V0.2 la répartit comme les données ATP : surtout la forme du match, un peu les sets, les surfaces, la pression et l'élan.

## Probabilité d'un point au service

```
p = borne( centre(surface) + pente × (talent serveur − talent relanceur)
         + affinité(serveur, surface) − affinité(relanceur, surface)
         + contre(surface) × opposition des styles / √2
         + niveau du serveur − niveau du relanceur      ← forme du match + forme du set + élan de set
         + pression(enjeu du point)
         + élan(point précédent du jeu, jeu de service précédent du set) )
```

| Mécanisme | Mise en œuvre | Valeur (config/moteur.json) | Origine |
|---|---|---|---|
| Niveau | talent × pente | pente 0,0034 par point de talent | calibrée |
| Surface | centre par surface | dur 0,641 ; terre 0,620 ; gazon 0,661 | calibrés |
| Affinité de surface | style caché 2D projeté sur une direction propre à la surface (principe 3 : la surface agit, pas les stats). Terre et gazon opposés, dur orthogonal | écarts-types 0,7 / 0,9 / 1,1 pt | mesurée (Sackmann, Elo toutes surfaces) |
| Contre de styles | produit vectoriel des styles, antisymétrique, structure de roue | 0,8 / 1,1 / 1,1 pt | mesuré |
| Forme du match | phase lente (20 % de la variance, persistance 0,9 par match) + aléa du jour | écart-type 2,1 pts | mesurée 2,8 pts, calibrée |
| Forme du set | tirage par set | 2,1 pts | mesurée 2,4 pts, calibrée |
| Constance (stat 1-99) | multiplie la variance de forme : ×1,5 (Constance 1) à ×0,5 (Constance 99) | amplitude 0,5 | mesurée |
| Pression | décalage du serveur selon l'enjeu | de −1,1 (balle de set pour lui) à +0,8 pt (balle de jeu) | mesurée (Match Charting Project) |
| Élan | point précédent du jeu ±0,48 ; jeu de service précédent ±0,23 ; set précédent ±0,43 de niveau | — | mesuré |

**Forme et principe 6.** La forme est la seule variable dynamique stockée : `EtatForme.Lente`, bornée par construction (processus autorégressif qui revient vers 0). Le simulateur de circuit la fait évoluer avec `ModeleForme.Avancer` après chaque match. Elle est justifiée par la mesure (environ 80 % de la dispersion d'un match à l'autre ne s'explique ni par le niveau, ni par le duel, ni par la surface, ni par l'élan ou la pression). La **confiance alimentée par les résultats** n'est pas codée : les données ne la confirment pas.

**Population de test** (`config/population.json`) : Constance tirée autour de 50, liée au talent (corrélation d'environ 0,35), écart-type 28. Ce sont les mêmes joueurs, avec les mêmes styles, sur les trois surfaces.

## Cibles par surface

`objectifs_equilibrage/` (dur, fichiers historiques), `objectifs_equilibrage/terre/` et `objectifs_equilibrage/gazon/` (nouveaux). Les trois sont construits par `dotnet run --project outils/Chelems.Donnees -- objectifs Hard|Clay|Grass`. Le portage C# reproduit **à l'identique** les fichiers sur dur hold/service par rang, résultats par écart, sets et tie-breaks, service moyen et adversaires. Pour la dépendance entre sets et la dispersion, il compte quelques matchs de plus que les fichiers sur dur d'origine (au plus 0,3 point d'écart sur les valeurs) ; le script d'origine de ces deux fichiers n'est pas dans le dépôt.

## Mesure de base (2026-11-18, graine 1, 40 tirages par joueur, environ 101 000 matchs par surface)

Fichiers : `resultats/fidelite_v0.2_{dur,terre,gazon}_graine1.csv`.

| | V0.1 dur | V0.2 dur | V0.2 terre | V0.2 gazon |
|---|---|---|---|---|
| chi² ajusté (hold, points au service, victoires) | 739 | 581 | 244 | 252 |
| chi² contrôles (sets, tie-breaks, dépendance) | 64* | 261 | 188 | 85 |
| Statuts ok / limite / écart | 78 / 15 / 17 | 76 / 13 / 21 | 72 / 19 / 19 | 78 / 15 / 17 |

\* La V0.1 obtenait ses contrôles avec des rivalités figées que les données réfutent (étape 0).

**Ce qui tient sur les trois surfaces** : hold moyen, points gagnés au service et balles de break sauvées du top 150 ; hold par tranche de rang ; répartition des sets en Bo3 et en Bo5 ; tie-breaks (sauf gazon en Bo3) ; même vainqueur aux sets 1 et 2 ; victoires du mieux classé entre 11 et 160 places.

## Écarts connus et questions pour le créateur

1. **Victoire du mieux classé selon l'écart de rang.** Le favori gagne trop à 161 places et plus (Bo3 dur 86 contre 74,6 %) et pas assez à 0-5 places (51 contre 55 %) et à 21-80 places en Bo5 (62 contre 70 %). Le calibrage ne peut pas corriger les deux à la fois. La cause probable est la population de test : le lien rang ↔ talent du simulateur de circuit, et les adversaires non classés ramenés au dernier rang simulé (qualifiés, invités, classements protégés). Le calibrage s'est fait sans la catégorie « 161+ » (option `--sans-161`).
2. **Dispersion de l'avantage au service aux grands écarts** : trop forte (15,2 à 15,5 contre 12,8 à 13,2). Cela vient du même problème de population.
3. **Tie-breaks sur gazon en Bo3** : 21 % contre 24,7 %. Cela pourrait venir de la surface (vitesse, retour) ; à revoir avec le court pace index.
4. **Dur, tranche 6-10** : hold de 84,0 contre 85,6 %, alors que les tranches voisines collent. Ce sont les talents de la population sur cette tranche.
5. **Court pace index par tournoi** : non intégré, faute de données. Il faut une source.
6. **CLAUDE.md** n'est pas encore mis à jour : forme du jour et Constance réactivées, confiance non confirmée (conflit avec « [Décidé pour le principe] »).

## Commandes

```
dotnet run --project src/Chelems.Console -- match --a 85 --b 78 --surface terre --constance-a 80 --constance-b 20 --detail
dotnet run --project src/Chelems.Console -- fidelite [--surface dur|terre|gazon|toutes] [--csv]
dotnet run --project src/Chelems.Console -- calibrer --parametres centres,pente,forme_match,forme_set --controles --sans-161 [--ecrire]
dotnet test
```

## Le talent pèse-t-il assez ? (barème, 2026-11-18)

`dotnet run --project src/Chelems.Console -- bareme [--bo 5] [--surface terre] [--constance-fixe]`

Victoire du meilleur selon l'écart de talent, sur dur. Les styles sont tirés au hasard, la Constance comme dans la population de test.

| Écart de talent | Bo3 | Bo5 | Jeux de service tenus, meilleur / autre (Bo3) |
|---|---|---|---|
| 0 | 50 % | 50 % | 81 / 81 |
| 2 | 55 % | 56 % | 82 / 80 |
| 5 | 62 % | 64 % | 83,5 / 78 |
| 10 | 73 % | 76-77 % | 86 / 75 |
| 15 | 81-83 % | 84-87 % | 88 / 71,5 |
| 20 | 88-90 % | 90-93 % | 90 / 68 |

- **Le même écart produit le même effet à tous les niveaux**, puisque le moteur ne voit que l'écart. Par exemple, 95 contre 90 donne 63,1 % et 70 contre 65 donne 61,9 %. Le petit surplus en haut vient de la Constance, liée au talent : les meilleurs sont plus réguliers. Avec une Constance fixée à 50, l'effet est identique à tous les niveaux.
- **Repères de la population** : talent moyen 93,8 pour le n°1, 82,2 pour le n°10, 74,2 pour le n°50, 69,9 pour le n°100, 67,0 pour le n°150.

**Contrôle contre l'ATP** : deux nouvelles familles de cibles, `gagne` (taux de victoire par tranche de rang) et `retour` / `pts_retour` (jeux et points de retour gagnés par tranche, fichier `retour_par_rang.csv`).
- Du rang 2 au rang 150, la victoire, le hold et le retour par tranche collent sur les trois surfaces.
- **Écart à la toute fin du classement, en haut : le n°1 en Bo5.** Il gagne 87 % de ses matchs simulés contre 92,8 % à l'ATP (dur), et il breake 29 % contre 33,7 %. À l'ATP, le n°1 breake nettement plus en Bo5 qu'en Bo3 (33,7 contre 29,5 % sur dur, 38,1 contre 34,7 % sur terre). Le moteur ne le reproduit pas.
- À l'inverse, sur gazon en Bo3 (surtout des tournois de préparation), les meilleurs breakent beaucoup moins que le moteur ne le prévoit (15 à 20 % contre 26 à 29 %). L'échantillon est petit.
- **Hypothèse** : un effet d'enjeu du tournoi. Les meilleurs élèvent leur niveau en Grand Chelem et se ménagent avant (c'est aussi la limite n°1 du simulateur de circuit). Ce n'est pas un défaut de la pente du talent. C'est à mesurer : performance au-delà de l'Elo selon le niveau du tournoi et la tranche du joueur.

## Enjeu du tournoi (ajouté le 2026-11-18)

**Mécanisme** (`config/moteur.json`, section `enjeu`) :
- chaque niveau de tournoi a un enjeu e entre 0 et 1 : Grand Chelem 1, les autres 0 ;
- la variance de l'aléa du jour d'un joueur est multipliée par 1 − réduction × e × (Constance − 1) / 98, avec une réduction de 0,5 ;
- la phase lente de forme n'est pas touchée, puisque la mesure montre que la forme d'avant pèse autant en Grand Chelem.

La valeur 0,5 vient de la mesure : la variance des joueurs du top 10 baisse d'environ 26 % en Grand Chelem au-delà de l'effet du format, et leur Constance est d'environ 83. La simulation de masse lit désormais le niveau du tournoi dans les fichiers d'adversaires : c'est une nouvelle dernière colonne `niveau_tournoi`, et les six autres colonnes restent identiques.

**Effet mesuré dans la V0.2 : faible.** Même avec une réduction de 1, le n°1 en Bo5 ne gagne que 87 % de ses matchs, contre 92,8 % à l'ATP. **Réduire les mauvais jours ne suffit pas à expliquer la domination du n°1.** C'est une correction de l'hypothèse de docs/mesure_rivalites.md (enjeu, verdict 1).

**Ce qui distingue vraiment le n°1** (cibles par tranche, sur dur) :

| | ATP | V0.2 |
|---|---|---|
| Jeux de service tenus, Bo5 | 89,2 % | 88,7 % |
| Points au service, Bo5 | 69,7 % | 69,8 % |
| Points au retour, Bo5 | 42,8 % | 41,4 % |
| **Jeux de retour gagnés, Bo5** | **33,7 %** | **29,4 %** |
| **Victoires, Bo5** | **92,8 %** | **86,4 %** |
| **Victoires, Bo3** | **83,5 %** | **77,7 %** |

- Le service du n°1 est bien reproduit.
- Le retour l'est un peu moins (−1,4 point).
- Surtout, le n°1 réel **convertit** ses points en jeux de retour et en victoires bien mieux que des points indépendants ne le prévoient. Ça vaut pour les deux formats.
- La tranche 2-5 colle partout.

C'est le profil d'individus exceptionnels sur la période (surtout Djokovic), pas une loi générale de l'enjeu. Ça relève des statistiques détaillées : Retour, et Résilience sur les points importants. La population de test, qui n'a qu'un talent par joueur, ne contient pas de tels profils.
