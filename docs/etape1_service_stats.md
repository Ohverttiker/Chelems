# Étape 1 de la roadmap : le service par les stats — 2026-11-25

But (CLAUDE.md §12) : relier les stats au détail de l'efficacité au service (ace, gagnant, attaque, neutre, défense, retour gagnant, double faute), par calibrage sur des joueurs réels.

## 1. Le modèle

Chaque stat décale les issues du duel au service, en logit, et la part de premières rentrées :

```
x_k = coefficient × (valeur − référence) × orientation_k
```

| Stat | Joueur | Effet sur le duel au service | Référence | Origine |
|---|---|---|---|---|
| Service | serveur | qualité (placement, effets) : services gagnants et coup suivant en attaque ; moins de défense et de retours gagnants ; sur seconde, moins de doubles fautes | 75 | créateur, 60 % de la pente |
| Puissance | serveur | vitesse : aces et services gagnants | 75 | créateur, 40 % de la pente |
| Retour | relanceur | contre le Service : mêmes issues, sens opposé | 75 | design |
| Explosivité | relanceur | contre la Puissance (réaction) : moins d'aces et de services gagnants | 75 | design |
| Taille | les deux | un grand serveur sert mieux, un grand relanceur retourne moins bien (§5.1) | 188 cm | **mesuré** (par cm) |
| Agressivité | serveur | prise de risque sur les deux balles (§6.3) | 50 | **mesuré**, rendu neutre (section 2) |

### Ce que veut dire « chaque stat a son pendant »

Il s'agit d'un duel entre **deux joueurs** : la Puissance du serveur affronte l'Explosivité du relanceur, et le Service du serveur affronte le Retour du relanceur. Les stats d'un même joueur ne s'opposent pas entre elles :
- la Puissance d'un joueur n'agit que quand il sert, et ne le pénalise jamais au retour ;
- son Explosivité n'agit que quand il retourne, et ne le pénalise jamais au service.

Un joueur à la fois puissant et explosif est donc avantagé des deux côtés.

Seule la **Taille** agit sur les deux rôles d'un même joueur : elle l'aide au service et le pénalise au retour (§5.1).

Les orientations sont égales dans chaque duel (Puissance contre Explosivité, Service contre Retour). Conséquence : deux joueurs de même niveau, avec la taille et l'agressivité de référence, retrouvent les lois de base de la surface. C'est ce qui garde la simulation de masse fidèle : sa population a toutes ses stats égales au talent.

Configuration : `service.stats` dans `config/moteur.json`. Calage : `dotnet run --project src/Chelems.Console -- caler-stats --ecrire`, puis `calibrer --surface toutes --parametres pentes,vitesse --ecrire`.

## 2. Taille et Agressivité

Pentes mesurées sur 76 joueurs du Match Charting Project (dur, 2015-2025), pondérées par les points au service. Le moteur les reproduit (`resultats/effets_stats_service.txt`).

| Grandeur | Taille, pour 10 cm | Agressivité, pour 10 points |
|---|---|---|
| Premières balles | +0,96 | −1,63 |
| Aces sur première | +6,69 | +0,27 |
| Services gagnants sur première | +0,78 | −0,01 |
| Points gagnés sur première | +3,31 | **+0,65** |
| Aces sur seconde | +0,55 | 0 |
| Services gagnants sur seconde | +0,78 | 0 |
| Doubles fautes (pour 100 secondes) | +1,42 | +0,24 |
| Points gagnés sur seconde | −0,44 | −0,14 |

**Échelle de l'Agressivité** [Proposé] : de 1 à 99, la part de premières passe d'environ 72 % à 56 %, l'étendue des 81 joueurs du classement ATP sur 52 semaines.

**Le risque paie sur la première** [créateur, 2026-11-25] :
- Mesurées telles quelles, les pentes faisaient de l'Agressivité un pur handicap : moins de premières, autant de points gagnés sur première, et −0,65 point sur seconde pour 10 points d'Agressivité. Un joueur prudent aurait toujours été meilleur.
- Ces pentes mêlent le risque et la capacité : les bons serveurs passent à la fois beaucoup de premières et une bonne seconde.
- Règle retenue : à capacité de référence, l'Agressivité **ne change pas le total des points gagnés au service**.
  - Coûts : moins de premières et plus de doubles fautes, d'après les pentes mesurées. Les doubles fautes coûtent les points qui auraient été joués.
  - Gain : plus de points gagnés sur première, grâce à des services plus dangereux, surtout un coup suivant en attaque.
  - L'Agressivité devient un style, pas un handicap.
- Option `--agressivite-mesuree` : revient aux pentes brutes.
- [Proposé, pour l'étape 4] Faire dépendre le gain du risque de la capacité, pour qu'un grand serveur ait intérêt à risquer et un petit serveur à rester prudent (§6.3, choix de la jauge).

## 3. Les joueurs réels reproduits

Pour chaque joueur (`resultats/profils_joueurs_dur.txt`, commande `profils-joueurs`) :
- la Taille est celle de l'ATP ;
- on estime Service, Puissance, Agressivité et Échange face à un adversaire de référence ;
- on compare ses 8 grandeurs mesurées à celles du moteur.

Résultat sur 77 joueurs : χ²/8 médian 2,3. 35 joueurs sont reproduits au niveau du bruit de mesure, 59 à moins de 5 fois ce bruit, et 8 restent mal reproduits.

Exemples (stats estimées ; mesuré → moteur) :

| Joueur | Taille | Service | Puissance | Agressivité | Aces sur 1re | Premières |
|---|---|---|---|---|---|---|
| Isner | 208 | 83 | 88 | 3 | 29,9 → 29,2 | 70,0 → 70,4 |
| Kyrgios | 193 | 59 | 99 | 39 | 26,0 → 20,7 | 66,3 → 63,6 |
| Sinner | 191 | 96 | 48 | 59 | 14,5 → 13,5 | 60,2 → 60,2 |
| Zverev | 198 | 80 | 45 | 10 | 15,6 → 17,3 | 69,3 → 68,5 |
| De Minaur | 183 | 98 | 24 | 63 | 8,1 → 8,0 | 58,6 → 58,7 |
| Schwartzman | 170 | 83 | 35 | 31 | 3,7 → 4,5 | 62,7 → 62,7 |

**Limites** :
- **Échange saturé (99)** pour les meilleurs : le MCP suit surtout des matchs entre bons joueurs, et l'adversaire de référence (75) est trop faible pour eux.
- **Profils mal reproduits** : de gros serveurs atypiques.
  - Kyrgios : 26 % d'aces sur première, 21 % au moteur.
  - Bublik : 22 % de doubles fautes, 14 % au moteur.
  - Federer et Hurkacz : beaucoup d'aces avec peu de doubles fautes.
- **Puissance et Retour d'un même joueur** : ces estimations ne regardent que le service. Le Retour et l'Explosivité de chaque joueur s'estimeront avec ses statistiques au retour, qui sont déjà extraites (colonnes `r_` de `efficacite_joueurs.csv`).

## 4. Recentrage de l'échelle (2026-11-25)

Demande du créateur : des stats réparties normalement autour de 70, très peu de joueurs au-delà de 90, et 99 réservé à une saison parfaite (Roddick 2003).

**L'échelle de départ était déjà la bonne.** Dans la population de test (top 150 du simulateur de circuit), le talent vaut en moyenne 72,8 avec un écart-type de 6,4, et le top 10 est vers 87. C'est sur cette échelle que la pente du moteur est calée.

**Le défaut venait de l'estimation**, d'où deux changements.

1. **A priori normal** (`ProfilJoueur`, maximum a posteriori).
   - Stats de niveau : N(72,8 ; 6,4). Agressivité, qui est un style : N(50 ; 20).
   - L'adversaire de référence est à 72,8.
   - Une stat ne s'écarte de la moyenne que si les données l'exigent : plus de 99 par défaut d'information.
2. **Puissance et Explosivité deviennent des stats de style.** Elles sont indépendantes du classement : un n°1 ne fait pas forcément d'aces (Djokovic, Nadal).
   - Leur effet par point est absolu (0,042 en logit), réglé pour que les joueurs réels suivent l'échelle (`caler-echelles`).
   - Dans la population de test, elles restent à 75. Le niveau passe par le Service, le Retour et l'Échange.
   - Les régler sur les joueurs réels alors qu'elles valaient encore le talent dans la population cassait la simulation de masse : 68 mesures ok au lieu de 110, favoris beaucoup trop forts.

**Résultat sur 77 joueurs** : χ²/8 médian 2,4 ; 35 joueurs au niveau du bruit de mesure, 62 à moins de 5 fois ce bruit.

| Stat | Moyenne | Écart-type | Max | Joueurs > 90 |
|---|---|---|---|---|
| Service | 76,5 | 10,2 | 99 | 5 |
| Puissance | 72,1 | 6,0 | 88 | 0 |
| Agressivité | 45,8 | 18,2 | 99 | 1 |
| Échange | 78,0 | 8,1 | 99 | 7 |

Exemples :
- Sinner : Service 87, Puissance 71.
- Alcaraz : Service 82, Puissance 72.
- De Minaur : Service 88, Puissance 67.
- Kyrgios : Service 48, Puissance 88.
- Isner : Service 79, Puissance 77, Agressivité 5.

La simulation de masse est revenue à son niveau : dur 109 ok, terre 113, gazon 109.

**Ce qui reste trop large : le Service** (écart-type 10,2 ; De Minaur reste au-dessus d'Alcaraz).

- **Doubles fautes.** Le Service est la seule stat qui les fait vraiment baisser. Or elles varient énormément d'un joueur à l'autre (de 6 à 22 % des secondes). Un joueur qui en fait peu reçoit donc un Service très haut : c'est le cas de De Minaur, avec 7,4 % de doubles fautes. [Proposé] Confier les doubles fautes à une autre stat : la Régularité (§4.3, « augmente le taux de deuxièmes balles rentrées »), ou l'Agressivité sur la seconde.
- **Spécialisation.** Entre joueurs réels, la qualité de service varie plus que le niveau général : il y a des spécialistes du service et des spécialistes du retour. Élargir l'effet par point du Service casserait la simulation de masse, où le Service vaut le talent. Il faut d'abord donner des profils Service / Retour / Échange distincts à la population de test (prochaine étape déjà prévue).
- **Échange (7 joueurs au-delà de 90)** : il n'est estimé ici que par les points gagnés au service. Il sera estimé correctement avec les données d'échange et de retour.

## 5. Régularité et spécialisation (2026-11-25)

### Régularité [créateur]

Nouvelle stat du serveur, à effet absolu par point :
- elle donne une part des premières balles ; l'autre part vient de l'Agressivité, qui en retire ;
- elle donne surtout les doubles fautes, l'Agressivité n'en ajoutant qu'un peu.

Le Service ne touche plus aux doubles fautes.

Orientation de la Régularité : +1 sur la part de premières (logit), −4 sur les doubles fautes. Échelle : 0,0109 par point.

### Une seule échelle pour les effets du service

Toutes les stats du duel au service ont maintenant un **effet absolu par point**, réglé pour que les 77 joueurs réels se répartissent avec l'écart-type de l'échelle, 6,4 (`caler-echelles`) :

| Stat | Effet par point (logit) |
|---|---|
| Service / Retour | 0,0333 |
| Puissance / Explosivité | 0,0449 |
| Régularité | 0,0109 |
| Taille | par cm, mesuré |
| Agressivité | par point, mesuré |

La pente du service ne sert plus qu'à fixer le rapport avec la pente de l'échange pendant le calage par simulation de masse.

### Profils de la population de test [créateur]

[Mesuré] Dans le top 81 ATP (52 semaines), le service et le retour d'un joueur sont négativement corrélés (−0,28). L'écart entre service et retour (écart-type 4,3 points) varie plus que leur somme (3,2) : les spécialistes sont la règle.

Profil de chaque joueur de la population (`config/population.json`, `caler-specialisation`) :
- Service = 72 + β × (talent − 72) + dS ;
- Retour = 72 + β × (talent − 72) + dR ;
- Échange = talent − (dS + dR) / 2.

Paramètres :
- **β = 0,41**. Un point de Service pèse 2,4 fois plus qu'avant : β garde l'effet du talent sur le service que la simulation de masse a calé.
- dS et dR ~ N(0 ; **8,0**), corrélation **−0,95**. Ils sont tirés d'une graine propre à chaque joueur, donc le même profil revient d'une simulation à l'autre.

Résultat, top 81 :

| | ATP | Population |
|---|---|---|
| Écart-type des points au service | 2,95 | 2,72 |
| Écart-type des points au retour | 2,41 | 2,71 |
| Corrélation service / retour | −0,28 | −0,27 |

**Point de vigilance** : la corrélation des écarts bute sur sa limite (−0,95). Les profils sont presque tous sur un seul axe, « serveur » ou « relanceur ». Peu de joueurs sont bons, ou faibles, aux deux à niveau égal.

### Résultats

**Joueurs réels** :
- χ²/8 médian **1,1**, presque le bruit de mesure (2,4 avant la Régularité) ;
- écarts-types estimés : Service 6,6, Puissance 6,5, Régularité 6,4.

**Simulation de masse**, après recalage de la pente de l'échange et des vitesses : **dur 111 ok, terre 117, gazon 110**. C'est le meilleur résultat de la V0.3.

**Écart nouveau** : les rangs 6-10 tiennent un peu moins leurs jeux de service (−1,4 point sur dur et terre, −2,3 sur gazon). À niveau égal, un mélange de grands serveurs et de grands relanceurs tient moins ses jeux en moyenne, car le hold n'augmente pas en ligne droite avec les points au service.

**Estimation finale** (`resultats/profils_joueurs_dur.txt`) : χ²/8 médian 1,1 ; 54 joueurs au niveau du bruit de mesure, 75 sur 77 à moins de 5 fois ce bruit.

| Stat | Moyenne | Écart-type | Joueurs > 90 |
|---|---|---|---|
| Service | 74,5 | 6,4 | 1 (Berrettini, 98) |
| Puissance | 72,2 | 6,4 | 0 (max : Kyrgios, 90) |
| Régularité | 77,0 | 6,4 | 0 |
| Agressivité | 48,7 | 19,0 | 1 |
| Échange | 77,5 | 7,4 | 5 |

Exemples (Service / Puissance / Agressivité / Échange / Régularité) :

| Joueur | Service | Puissance | Agressivité | Échange | Régularité |
|---|---|---|---|---|---|
| Sinner | 76 | 72 | 74 | 88 | 85 |
| Alcaraz | 78 | 71 | 30 | 92 | 77 |
| De Minaur | 79 | 68 | 71 | 77 | 81 |
| Zverev | 77 | 71 | 1 | 88 | 68 |
| Kyrgios | 59 | 90 | 15 | 89 | 69 |
| Bublik | 69 | 78 | 36 | 75 | 57 |

## 6. La taille au cœur de la spécialisation (2026-11-26)

### Le modèle

D'après le créateur : la taille donne un gros avantage au service (la balle part de plus haut, avec plus de marge) et un désavantage au retour (des grands bras se placent moins vite dans les situations réflexes, surtout au corps ; d'où Medvedev, puis Zverev, très loin de la ligne). Elle va aussi de pair avec la puissance et coûte en explosivité. En revanche, la stat de Service ne dépend pas de la taille.

- **Effet direct de la Taille**, calé séparément au service (`taille`) et au retour (`taille_relanceur`), sur les pentes mesurées dans le MCP. C'est le reste, une fois retiré ce qui passe par la Puissance et l'Explosivité.
- **Population** : taille tirée selon le MCP (188,7 ± 7,4 cm) ; Puissance = 75 + 0,11 × (taille − 188) + bruit ; Explosivité = 75 − 0,15 × (taille − 188) + bruit (écart-type total 6,4). Service et Retour ne dépendent pas de la taille.
- **Liens 0,11 et 0,15 par cm** : ce sont les valeurs les plus fortes qui laissent l'effet direct de la taille dans le bon sens. Un grand serveur ne perd pas de services gagnants, un grand relanceur n'est pas directement avantagé. Avec des liens plus forts (0,43 essayé d'abord), la Puissance et l'Explosivité expliquent plus que l'effet total mesuré.
- **Estimation des joueurs réels** : Retour et Explosivité sont maintenant estimés sur leurs statistiques au retour. Les a priori de Puissance et d'Explosivité sont centrés selon la taille.

### Ce que disent les données

| Pour 10 cm de plus | Aces sur 1re | Services gagnants sur 1re | Points du serveur sur 1re |
|---|---|---|---|
| Serveur plus grand | +6,7 | +0,8 | +3,3 |
| Relanceur plus grand | −1,2 | +1,4 | +1,1 |

- Un relanceur plus grand concède moins d'aces (ses grands bras touchent plus de balles), mais plus de services gagnants (balles au corps, réflexes).
- **Conflit avec le §5.1** (« compensé à l'égal côté retour ») : la gêne au retour vaut environ le tiers de l'avantage au service.
- **La taille ne suffit pas à expliquer la corrélation négative service / retour.** Sans spécialisation de style, la population donne +0,31 : le niveau général pousse service et retour dans le même sens. Avec une corrélation des profils bornée à −0,6 (« modérée », créateur), la population obtient −0,01, contre −0,28 à l'ATP. La dispersion du service est aussi trop forte (3,7 contre 2,95).

### Résultats

- **Simulation de masse** : dur 128 ok, terre 130, gazon 120 (sur environ 195 mesures). C'est le meilleur résultat de la V0.3, et les contrôles (sets, tie-breaks, dépendance des sets) progressent nettement grâce à la variété des profils.
  - Les vitesses compensatrices deviennent fortes : dur −0,27, terre −0,17, gazon −0,02.
  - Pente de l'échange : 0,0076.
- **Écarts connus** (1,5 à 2 points) :
  - Conversion% : 64,1 contre 66 ;
  - fins hors service sur dur : fautes provoquées −2, fautes directes +2 ;
  - quelques mesures voisines.
  - Les lois de l'échange sont calées entre joueurs égaux, alors que la population mélange des profils variés.
- **Bug corrigé** : depuis que chaque stat du service a son effet absolu par point, la pente du service ne joue plus. Le calage des lois de l'échange la recalculait et la faisait exploser (0,80), ce qui bloquait le calibreur. Désormais, seule la pente de l'échange est recalée (`calibrer --parametres pente_echange,vitesse`).

### Ordre de calage

1. `caler-stats` ;
2. `caler-echelles` ;
3. `caler-echange` sur chaque surface ;
4. `caler-stats` à nouveau, car les lois du service ont bougé ;
5. `caler-specialisation` ;
6. `calibrer --parametres pente_echange,vitesse`.

## 7. Effets par balle et clôture de la V0.3 (2026-11-26)

### Ce dont chaque balle tire son résultat [créateur]

| Balle | Stats, par ordre d'importance |
|---|---|
| 1re balle | Taille, Service, Puissance, Agressivité, Régularité |
| 2e balle | Taille, Régularité, Service, Agressivité |
| Retour de 1re | Retour, Explosivité, Taille |
| Retour de 2e | Retour, Explosivité, Taille |

- La Puissance ne joue qu'au service, et seulement sur la première. L'Explosivité ne joue qu'au retour, surtout sur la première (réflexes face aux services rapides).
- Poids sur la seconde [Proposé] : Service 0,7, Retour 0,6, Explosivité 0,3.
- **L'Échange ne joue pas dans le duel au retour** [créateur]. Nadal et Federer gagnent beaucoup de points au retour surtout parce qu'ils convertissent les points lancés, et cela relève de l'échange.
- **Artefact corrigé** : quand le Service ou le Retour variaient, la part des aces bougeait à cause de la renormalisation des issues ; avec le Retour, les aces concédés montaient. L'orientation des aces vaut désormais 0,7 pour ces deux stats, ce qui laisse leur part stable.
- **Pour l'étape 4** [créateur] : la stat Effet sur la seconde (rotation, kick) et un poids plus fort de l'Agressivité sur la seconde. Exemple : Shelton, qui a probablement la meilleure seconde du circuit grâce à son agressivité et à son effet, pas à sa puissance.

### État figé de la V0.3

- **Simulation de masse** : dur 118 ok, terre 126, gazon 123. Tests : 120 réussis, 8 groupes d'écarts connus.
- **Effets par point (logit)** : Service / Retour 0,0328 ; Puissance 0,0366 ; Explosivité 0,0468 ; Régularité 0,0109.
- **Pente de l'échange** : 0,0073.
- **Vitesses** : dur −0,43, terre −0,41, gazon −0,14. Ce sont des compensations de plus en plus fortes (absence de forme, profils variés) ; elles devront être recalées quand la forme reviendra (décision ouverte n°20).
- **Population** : β = 0,42 ; écart de spécialisation 8,1 points, corrélation −0,6 ; Puissance +0,11 et Explosivité −0,15 point par cm.

### Limites connues

- **Estimation du Retour des joueurs réels peu fiable.** Écart médian au retour : 5,8 fois le bruit de mesure. Le Retour reste corrélé à la taille (+0,28) et certains profils sont peu crédibles (Alcaraz 53, De Minaur 56, Kyrgios 84). Ces chiffres agrègent des carrières 2015-2025 face à des adversaires variés, avec des hypothèses fortes : part de premières adverses fixe, adversaire de référence. **Le moteur lui-même** reproduit bien les moyennes et la dispersion ATP ; c'est l'estimation joueur par joueur qui reste à reprendre, avec des données par saison.
- La population n'atteint pas la corrélation service / retour de l'ATP : −0,05 contre −0,28.
- Écarts de 1,5 à 2 points sur Conversion% et sur les fins de point hors service (section 6).
