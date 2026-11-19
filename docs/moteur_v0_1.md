# Moteur de match V0.1

Étape 2-3 de l'échelle de l'agent données ATP : passage au point, avec tie-breaks, sets et scores. Surface : dur.

## Ce que fait le moteur

- **Entrée :** deux `Joueur` = un talent (échelle du simulateur de circuit) + un **style caché fixe** (point du plan tiré une fois par joueur).
- **Point :** `p = clamp(centre + pente × x, centre ± demi_amplitude)`, avec `x = talent servant − talent relanceur + écart de style`.
- **Écart de style :** `σ/√2 × (produit vectoriel des deux styles)`. Il est antisymétrique, nul entre deux styles identiques et fixe pour une paire : rien n'est tiré pendant ni avant un match, donc ce n'est pas une variable dynamique au sens du principe 6. Il a une structure de roue, où chaque style a un contre et aucun ne domine en moyenne. Son écart-type sur des paires au hasard vaut `σ`.
- **Match :** point par point, jeux avec égalité et avantage, tie-break en 7 points à 6-6 dans tous les sets, Bo3 ou Bo5, premier serveur tiré au sort. Un observateur facultatif reçoit chaque point.
- **Constantes :** `config/moteur.json`. Aucune constante de calibrage n'est écrite dans le code.

## Commandes

```
dotnet run --project src/Chelems.Console -- match --a 85 --b 78 --bo 5 --graine 3 [--detail]
dotnet run --project src/Chelems.Console -- fidelite [--tirages 40] [--graine 1] [--csv]
dotnet run --project src/Chelems.Console -- calibrer [--sigma 6] [--ecrire]
dotnet test
```

## Mesure de base (2026-11-18, graine 1, 40 tirages par joueur, 101 413 matchs)

Config : centre 0,6405, pente 0,02338, demi-amplitude 0,0455, σ = 6 (valeurs de l'agent d'équilibrage, `prototype_python/dispersion_resultats.csv`). Détail : `resultats/fidelite_v0.1_graine1.csv`.

| Mesure | ATP | V0.1 | Statut |
|---|---|---|---|
| Hold moyen top 150 | 80,7 | 79,6 | limite |
| Points gagnés au service | 64,5 | 63,4 | limite |
| Balles de break sauvées | 61,7 | 62,4 | ok |
| Hold Bo3 par tranche (7) | 77,9 à 87,4 | à 0,8 pt près | ok |
| Matchs en 2 sets (Bo3) | 63,5 | 63,7 | ok |
| Bo5 en 3 / 4 / 5 sets | 45,9 / 34,0 / 20,1 | 46,0 / 34,1 / 19,9 | ok |
| Sets au tie-break Bo3 / Bo5 | 20,0 / 17,0 | 18,8 / 18,5 | ok |
| Même vainqueur sets 1 et 2, 0-5 places | 61,8 | 63,0 | ok |
| Victoire du mieux classé Bo3, 41-80 / 81-160 / 161+ | 66,1 / 70,1 / 74,6 | 72,4 / 78,2 / 85,0 | **écart** |

Contrôles indépendants (sets, tie-breaks, dépendance des sets) : chi² = 64. Sans styles (σ = 0), on retombe à 57,5 % de matchs en 2 sets : la dispersion par les styles est donc nécessaire, et elle suffit.

## Écarts connus et questions pour le créateur

1. **Le favori gagne trop aux grands écarts** (Bo3 au-delà de 20 places : de +3 à +10 points). Le balayage de σ (0, 3, 6, 9, 12) et le recalibrage ne le corrigent pas sans casser les sets. Piste principale : hypothèse 2 du README des objectifs (classement en retard sur le niveau : qualifiés, invités, classements protégés). C'est la population qu'il faut revoir, pas le moteur.
2. **La probabilité de point est presque « tout ou rien ».** Avec une pente de 0,023, la borne est atteinte dès 2 points d'écart effectif. Dans la plupart des matchs, un joueur sert donc à 68,6 % et l'autre à 59,5 %, et le talent ne décide que du joueur qui a le bon côté. Les cibles agrégées sont respectées, mais il faudra vérifier que ça reste crédible quand les stats détaillées remplaceront le talent.
3. **Tie-break du set décisif :** il est fixé à 7 points. Les règles de l'Open d'Australie et de l'US Open ont changé entre 2015 et 2025, l'effet est probablement mineur.
