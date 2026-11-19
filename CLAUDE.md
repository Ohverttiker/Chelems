# Chelem — jeu de stratégie de tennis (« Tennis Pourcentage »)

Convention des marques : **[Décidé]** = validé par le créateur. **[Proposé]** = proposition à valider avant d'être codée. **[À calibrer]** = valeur numérique à fixer avec les données ATP. **[Suspendu]** = idée mise de côté tant qu'une mesure ne prouve pas son utilité (voir principe 6). **[Mesuré]** = valeur ou constat tiré des données ATP (détail dans `docs/mesure_rivalites.md`).

## 1. But du projet

Jeu de stratégie au tour par tour : le joueur simule un match de tennis face à une IA en choisissant, à chaque coup, où viser, avec quel effet, avec quelle puissance, et où se replacer. Le résultat de chaque coup dépend des statistiques du joueur et du risque pris (approche « Tennis Pourcentage » : on joue les coups qui maximisent ses probabilités de gagner le point).

Le jeu doit à terme s'intégrer à un simulateur de circuit ATP déjà développé en C# (perpétuel, très fonctionnel), qui a deux limites connues :
1. Gestion de la fatigue trop simple : un top joueur allemand gagne Hambourg la semaine avant Roland-Garros, puis se repose à Roland-Garros (idem Halle puis Wimbledon). Piste : pondérer la décision de jouer/se reposer par l'importance du tournoi (points, prestige), pas seulement par la fatigue.
2. Niveau du joueur réduit à un seul chiffre (50-99). Le nouveau système de statistiques détaillées (section 4) remplace ce chiffre, qui devient une valeur **dérivée** (voir 4.5).

## 2. Stack et architecture

- **Langage : C#** [Décidé]. Le créateur maîtrise C#, connaît Python, a peu d'expérience Unity.
- **Cœur de jeu = bibliothèque .NET pure, sans dépendance à un moteur** [Proposé] : stats, formules, résolution d'un point, IA. Elle sert à la fois au jeu et au simulateur de circuit.
- **Interface fine par-dessus** [Proposé] : Godot 4 (.NET) recommandé, Unity ou MonoGame possibles. Choix du moteur : **à trancher**, pas avant que le cœur soit testable en console.
- Le cœur doit être testable sans interface : simulation de matchs complets en ligne de commande, avec graine aléatoire fixe pour les tests reproductibles.

## 3. Principes de conception

1. **Comportements émergents, pas de règles ad hoc par joueur.** Un archétype est un profil de statistiques, jamais un cas spécial codé en dur. Si un comportement réel doit apparaître (ex. un mauvais serveur joue une première balle prudente), il doit sortir des formules, pas d'un `if`.
2. **Les stats décrivent des profils, pas seulement un niveau.** Une stat basse n'est pas toujours un défaut (voir Effet, 4.1).
3. **Les surfaces modifient la physique de la balle, pas les stats du joueur.** Les affinités de surface émergent.
4. **Un seul moteur de point** pour le jeu et le simulateur de circuit. Le simulateur peut l'appeler en version allégée mais avec les mêmes formules.
5. **Les statistiques de l'ATP sont la bible du jeu** [Décidé]. Le jeu doit, **absolument à tout instant**, permettre de recréer ces statistiques : à chaque étape du développement, une simulation de masse doit retrouver les chiffres du circuit (premières balles, aces, doubles fautes, points gagnés au service et au retour, **jeux gagnés derrière son service, dont la moyenne du circuit est d'environ 80 %** [Mesuré : 80,7 % sur dur, 76,7 % sur terre, 83,9 % sur gazon pour le top 150, 2015-2025], breaks, longueur des échanges, tie-breaks, etc.), à la fois en moyenne et par profil de joueur. Une formule, un paramètre ou une nouvelle mécanique qui éloigne le jeu de ces statistiques est un défaut, quelle que soit son élégance. En cas de conflit entre une intention de design et les statistiques ATP, on ne tranche pas en silence : on le signale au créateur. Aucune valeur ATP n'est inventée (voir section 8).
6. **Sobriété en variables dynamiques** [Décidé]. On essaiera de se passer, autant que possible, de variables dynamiques du style forme du jour ou confiance. Hypothèse de travail : la construction des probabilités (le point, qui s'agrège en jeu, set et match) produit déjà par elle-même la variance observée dans la réalité, par exemple des favoris sortis par surprise au premier tour. C'est ce qu'a montré le simulateur de circuit, où le taux de break dépend uniquement de l'écart de niveau. Une variable dynamique n'est ajoutée que si une mesure contre les données ATP prouve que la variance émergente est insuffisante (ou mal répartie) **et** qu'aucun mécanisme déjà présent ne peut l'expliquer. Elle est alors bornée, retourne vers sa valeur de base et se justifie par un écart chiffré. Tant que ce test n'a pas été fait, ces variables ne sont pas codées.
   **Test effectué (2026-11-18, `docs/mesure_rivalites.md`).** La variance émergente des points ne suffit pas.
   - Environ 80 à 90 % de la dispersion d'un match à l'autre, une fois le niveau retiré (Elo), ne s'explique ni par le duel (environ 5 %), ni par la surface, ni par la semaine, ni par l'élan ou la pression.
   - Une **forme par match** est donc justifiée et codée (voir 4.6), ainsi que la **Constance**.
   - La **Confiance alimentée par les résultats** n'est pas confirmée : gagner un match serré ne rend pas meilleur au tournoi suivant.

## 4. Statistiques

Échelle : **1-99** [Décidé, plus précise que le 1-20 de Football Manager ; les exemples du créateur en 1-20 se convertissent]. Les pros se situent surtout entre 40 et 99 [À calibrer].

### 4.1 Techniques (7)

| Stat | Rôle |
|---|---|
| Coup droit | Qualité et fiabilité du coup droit |
| Revers | Idem, côté revers |
| Service | Qualité de la 1re et de la 2e balle (avec Taille, Puissance, Régularité) |
| Retour | Qualité du retour de service |
| Volée | Jeu au filet (smash inclus [Proposé]) |
| Toucher | Précision des coups demandant peu de puissance : amortis, lobs, petit jeu, volées enchaînées, balles sur les lignes, placement en défense |
| Effet | **Stat bipolaire** (voir ci-dessous) |

**Effet** [Décidé] : détermine le profil de spin. Effet haut = lift très rapide (≈3500 tr/min, type Nadal), slice peu tournant. Effet bas = lift faible (≈1500-2000 tr/min), slice très prononcé (type Federer), frappe plus à plat. Ce n'est donc pas « bon/mauvais » mais « lifteur » vs « slicer/plat ». [Proposé] lift_rpm = f(Effet), slice_rpm = f(100 − Effet) ; le revers à une main amplifie les deux (voir 5.2).

### 4.2 Physiques (5 + attributs fixes)

| Stat | Rôle |
|---|---|
| Puissance | Augmente la vitesse de balle des coups d'attaque **sans réduire la précision** [Décidé] |
| Endurance | Résistance à la fatigue sur le match et sur les longs échanges |
| Déplacements | Vitesse et taille de la couverture de terrain |
| Explosivité | Démarrages, changements de direction (première stat touchée par l'âge). [Proposé] Rôle en match : réduit l'excentricité de l'ellipse de couverture (le joueur couvre mieux la zone dans son dos, donc les contre-pieds), réduit le temps de réaction au retour de service et améliore la couverture à courte distance vers le filet (amortis). Déplacements garde la vitesse et la taille générale de l'ellipse |
| Fragilité | Risque de blessure. **N'a aucun effet sur un match**, uniquement dans le simulateur de circuit |

Attributs fixes (ne sont pas des stats) :
- **Taille** (voir 5.1), **main dominante** (gaucher/droitier : les coups droit/revers sont inversés par rapport au côté, impact sur les zones et les services), **type de revers** (une ou deux mains, voir 5.2).

### 4.3 Mentales / tactiques (7)

| Stat | Rôle |
|---|---|
| Concentration | Évite le relâchement quand le match est facile ou l'adversaire plus faible (Federer vs joueurs hors top 10) |
| Résilience à la pression | Évite la baisse de stats quand l'enjeu monte : tie-breaks, balles de break, finales (Djokovic, Nadal ; à l'inverse Zverev face aux top 10). [Mesuré] À l'ATP, les meilleurs semblent mieux sauver leurs balles de break, mais le moteur reproduit le même écart sans aucune résilience : c'est un effet mécanique, pas un trait des meilleurs. La Résilience reste à mesurer **joueur par joueur** (balles de break, tie-breaks, points importants). Piste prioritaire : le n°1 de 2015-2025 convertit ses points importants bien mieux que le hasard ne le prévoit |
| Agressivité | IA : tendance à attaquer et prendre des risques. Humain : zone de confort (voir 6.4) |
| Constance | [Décidé, Mesuré] Amplitude de la forme d'un joueur : multiplie la variance de sa forme par un facteur allant de ×1,5 (Constance 1) à ×0,5 (Constance 99). Trait stable mais modéré (fiabilité 0,35). Il est lié au niveau, puisque les meilleurs sont plus réguliers, mais pas seulement. Il réduit aussi l'aléa du jour dans les tournois à fort enjeu (voir 4.6). Exemples mesurés de joueurs réguliers : Federer, Sinner, Alcaraz, Djokovic |
| Régularité | [Proposé] Stat **en match** : capacité à réussir plusieurs fois le même coup (réduit la dispersion d'exécution) ; augmente le taux de deuxièmes balles rentrées. À classer entre technique et mental |
| Créativité | [Décidé] Augmente la réussite (ou la qualité) des coups candidats **moins bien classés** par le score de l'IA, donc plus rarement choisis (amorti, lob, long de ligne à plat, montée surprise…). Il n'existe pas de catégorie de coups « atypiques » : c'est le classement contextuel qui décide. Les coups sûrs ne reçoivent aucun bonus. Un joueur créatif réussit plus souvent les coups non intuitifs et les choisit donc plus souvent (ex. répartition 45-35-20 au lieu de 55-30-15) ; un joueur peu créatif choisit plus souvent le coup sûr, qu'il réussit aussi bien. Le bonus dépend de la stat technique de chaque coup : fort pour un joueur complet (Alcaraz), presque nul pour un joueur à une seule qualité ou aux lacunes techniques importantes. Mécanisme détaillé en 6.6 |
| **Lecture du jeu** [Décidé] | Qualité de l'anticipation et de l'évaluation des coups (voir 6.5). Remplace l'idée d'une stat « Anticipation » |

### 4.4 Âge et progression [Décidé]

- Le déclin lié à l'âge attaque les stats physiques : endurance, déplacements, surtout explosivité. Il touche très peu la technique et **aucunement** le mental.
- Les jeunes ont déjà des qualités remarquables quand ce sont de futurs talents (ex. coup droit d'Arthur Fils, retour de Rafael Jodar) et progressent surtout en technique et en mental. Leur physique est haut et peut encore progresser.
- Une même stat peut évoluer au fil des ans (ex. service de Djokovic entre 2011 et 2013).
- [Proposé] chaque joueur a un potentiel par stat (plafond) et une courbe de progression par catégorie.
- [Proposé] un vétéran peut compenser son déclin physique par une progression technique tardive (ex. Nadal en 2019 : volée, coup droit, revers) et raccourcit alors ses points.

### 4.5 Niveau global dérivé [Proposé]

Un indicateur global (OVR) est calculé à partir des stats, avec une pondération par archétype. Il sert au simulateur de circuit, aux classements et à l'équilibrage (budget de points, voir 7). Il n'est jamais saisi à la main.

### 4.6 Variables dynamiques

**Soumises au principe 6 (sobriété).** Variables qui évoluent pendant le match ou entre les matchs. Toutes sont **bornées** et reviennent progressivement vers une valeur de base (pas d'effet boule de neige incontrôlé). Avant de coder l'une d'elles, on vérifie par simulation que la variance émergente des probabilités ne suffit pas déjà à reproduire les statistiques ATP (favoris battus, séquences de points, tie-breaks, etc.). Les descriptions ci-dessous restent la **référence si le test échoue**, pas un programme de développement.

- **Forme physique** [Décidé, formules À calibrer] : jauge propre à chaque joueur pendant le match. Elle baisse avec les échanges longs, les courses et les balles difficiles à renvoyer (variées, liftées). La vitesse de baisse dépend de l'**Endurance** : un joueur endurant (Nadal et Djokovic à leur pic, Van Assche) perd très peu. Plus la forme est basse, plus des stats sont pénalisées : Déplacements, Puissance, Explosivité, d'autres à décider. Propositions : [Proposé] effet non linéaire (faible jusqu'à un seuil, puis accéléré) ; coût selon la surface et le format (3 ou 5 sets) ; petite récupération entre les points, plus grande aux changements de côté ; la valeur finale du match alimente la fatigue de circuit (limite n°1 du simulateur), avec une vitesse de récupération liée à l'Endurance.
  - Effet émergent attendu [Décidé] : le plan de jeu change avec la forme relative des deux joueurs et avec l'âge (Nadal 2005-2007 cherche l'échange long à Roland-Garros ; Nadal 2019 joue la majorité de ses points en trois coups ou moins). [Proposé] l'IA ajoute dans son évaluation un coût de prolongement de l'échange selon l'écart de forme entre elle et l'adversaire.
- **Confiance** [Suspendu, en conflit avec les données] : l'effet d'un résultat sur la performance suivante n'est pas mesurable (−0,2 ± 0,1 point). À ne coder que si une autre mesure la justifie. Le créateur doit trancher ce conflit (principe 5, décision ouverte n°14). Description d'origine, conservée comme référence : augmente avec les résultats récents et la réussite de coups risqués, baisse avec les échecs. Elle donne un léger bonus de réussite sur les coups risqués, d'autant plus fort que le joueur est jeune (elle permet de combler son potentiel). Pour un joueur créatif, elle dépend de la réussite des coups atypiques : dévastateur en confiance, « trou d'air » quand ça rate (Alcaraz, Bublik). [Proposé] mise à jour pondérée par le risque du coup (un coup risqué réussi pèse plus qu'un coup facile). **Gains** : plus forts chez les jeunes. **Chutes** [Décidé] : d'autant plus fortes que la Résilience et la Constance sont faibles. Exemple : Nadal, mental exceptionnel, n'a jamais eu de trous d'air et accrochait Roddick en Coupe Davis à 17 ans ; Djokovic, mental très haut mais quelques trous d'air (à calibrer).
- **Forme du jour** [Décidé, Mesuré] : deux étages, en probabilité de point au service. Pas affichée directement ; le joueur n'en voit que les symptômes.
  - Une phase lente (environ 20 % de la variance) persiste d'un match à l'autre (0,9 par match) et s'efface en 1 à 3 mois : ce sont les périodes de grâce ou de creux (Tiafoe).
  - L'aléa du jour (environ 80 %) est tiré à chaque match.
  - Une forme du set s'y ajoute (variation pendant le match).
  - Écart-type calibré : 2,1 points pour le match, 2,1 points pour le set, à Constance 50.
  - Bornée par construction (retour vers 0). Le simulateur de circuit garde la phase lente de chaque joueur.
- **Pression** [Décidé, Mesuré sur le Match Charting Project] : calculée à partir du contexte (score, enjeu), pas stockée. (Pas une variable dynamique au sens du principe 6 : rien n'est stocké.) Décalage du serveur selon l'enjeu du point, en moyenne sur le circuit :
  - balle de set ou de match pour le serveur : −1,1 point ;
  - tie-break : −1,0 point ;
  - balle de break : −0,5 point ;
  - balle de jeu : +0,8 point ;
  - 30-30 et égalité : +0,6 point.

  La Résilience modulera ces effets joueur par joueur.
- **Élan** [Décidé, Mesuré, effet faible] :
  - ±0,5 point sur le point suivant du même jeu ;
  - ±0,2 point sur un jeu de service selon le précédent ;
  - ±0,4 de niveau pendant le set qui suit un set gagné ou perdu.
- **Enjeu du tournoi** [Décidé, Mesuré] : en Grand Chelem, les meilleurs ont environ deux fois moins de mauvais jours. Codé ainsi : l'aléa du jour est réduit jusqu'à moitié selon la Constance. L'effet sur les victoires est faible ; la domination du n°1 relève du Retour et de la Résilience. Le relâchement des meilleurs dans les ATP 250/500 (n°1 : −3,2 points de victoire) relève de la priorisation des tournois, dans le simulateur de circuit (limite n°1).

## 5. Modificateurs structurels

### 5.1 Taille [Décidé, valeurs À calibrer]

- Taille idéale : **1m85-1m91**. Léger malus général pour 1m80-1m85 et 1m91-1m98. Malus plus important au-delà de ces bornes.
- Plus un joueur est grand : service meilleur (à stats égales : plus de premières et de secondes rentrent sur une même jauge, donc il peut se permettre plus de puissance), bonus de puissance de frappe, **mais** malus de déplacement et de retour.
- Plus un joueur est petit : l'inverse (bonus déplacement et retour, malus service).
- L'avantage au service est compensé à l'égal côté retour. Les bonus de puissance et de service **ne compensent pas** les malus de déplacement et de retour.
- Exemple de test : Medvedev et Rublev avec des stats identiques → Medvedev rentre plus de balles à jauge égale.

### 5.2 Main dominante et revers

- Gaucher : inverse les rôles coup droit/revers selon le côté, change les zones favorables et l'effet des services.
- Revers à une main [Décidé, issu de la V1] : favorise les coups d'attaque, pénalise la défense, plus difficile contre le gros lift, mais permet plus d'effet (positif comme négatif).

### 5.3 Surfaces [Décidé]

La surface agit sur la physique de la balle : conservation de vitesse au rebond, hauteur de rebond, amplification des effets. Les affinités émergent des stats [Décidé] :
- **Terre** : balle ralentie et effets amplifiés → favorise Effet haut, Déplacements, Endurance (échanges longs, adversaire fatigué plus vite, moins de coups gagnants subis).
- **Gazon** : balle plus rapide, rebond bas, slice qui glisse → favorise gros serveurs, Puissance, frappe à plat, volée.
- **Dur** : intermédiaire. Indoor : plus rapide.
- Pas de stat « affinité de surface » : si les résultats ne correspondent pas à la réalité, ajuster les paramètres physiques de la surface, pas les joueurs.
- [Mesuré] La surface agit surtout à travers l'**affinité de chaque joueur**. Les contres de styles propres à une surface sont faibles (2 % au plus de la dispersion). Les bascules à la Nadal–Djokovic (terre 18-9, dur 7-20) viennent d'une forte affinité.
- Dans le moteur actuel, un style caché est projeté sur une direction propre à chaque surface : terre et gazon opposés, dur orthogonal. Ce style sera remplacé par les attributs (taille, effet, déplacements).
- Centres calibrés : dur 64,1 %, terre 62,0 %, gazon 66,1 %.
- **Court pace index par tournoi** : pas encore intégré, il manque une source de données (décision ouverte n°16).

## 6. Gameplay

### 6.1 Interface

- Terrain de tennis **vu de dessus, à l'échelle**. Le joueur en bas de l'écran.
- **Tableau de bord à droite** : les informations qu'on aurait sur le terrain mais qu'on ne peut pas afficher (vitesse de balle, effet, etc.) + les options pour renvoyer la balle.
- **Jauge de placement sur le côté du terrain.**

### 6.2 Choix du joueur à chaque coup [Décidé]

1. **Zone visée** : clic sur le terrain adverse, divisé en **9 zones égales** (3 × 3).
2. **Effet** : lift, slice ou plat.
3. **Puissance** : défense, contrôle, attaque, risqué. La V1 utilisait une jauge continue **1-100 %** (voir 6.3). À trancher : jauge continue pour tous les coups ou paliers.
4. **Placement** : jauge libre pour choisir sa profondeur après le coup (monter à la volée, coller à la ligne, ou prendre de l'espace).
5. **Anticipation** : avant de recevoir le coup adverse, pivoter l'ellipse de couverture d'un côté du terrain.

### 6.3 Service [Décidé pour les principes, formules À calibrer]

Le service reprend la jauge de puissance (1-100 %) : plus la jauge est haute, plus la balle est rapide et moins elle rentre. Le joueur vise une jauge qui fait rentrer environ **60 %** de ses premières et environ **90 %** de ses secondes.

Brouillon de la V1 : la 1re balle dépend de Agressivité, Taille, Service, Puissance ; la 2e dépend de Agressivité, Taille, Service, Constance. Le calibrage était difficile. **Proposition pour le simplifier** [Proposé] : séparer deux choses.
- **Capacité** : courbe P(rentrer | jauge) et vitesse(jauge), fonction de Service, Taille, Puissance (1re) ou Régularité et Effet (2e). C'est ce qu'on calibre sur les données ATP.
- **Choix** : à quel point de la courbe on se place. Pour l'IA, la jauge maximise le gain espéré du point, biaisé par Agressivité. Ça fait émerger les comportements suivants sans règle spéciale :
  - Mpetshi Perricard / Shelton : capacité très haute, donc 2e balle aussi dangereuse que la 1re.
  - Moutet / Bublik : appétit du risque élevé sur la 2e, beaucoup de doubles fautes.
  - Nadal / Zverev actuel : très prudents, quasiment pas de doubles fautes. Zverev jeune en faisait beaucoup quand son service était moins une arme ; sa progression (Service, régularité) doit reproduire cette évolution. Aujourd'hui, d'après le créateur, il a le meilleur taux de premières balles du circuit (à confirmer avec les données ATP).
  - Tirante / J.M. Cerúndolo : service faible, donc peu de gain à prendre du risque, première balle prudente pour ne pas avoir à défendre la balle suivante.

**Cible de pourcentage de premières** [Décidé] : chaque joueur vise un pourcentage de premières balles selon son Agressivité (ex. un joueur très agressif comme Sinner s'approche de 60 %, un joueur très prudent comme Zverev de 70 %). La Pression et la Confiance modifient la capacité réelle.

**Jour sans** [Décidé] : un joueur dans un mauvais jour peut faire un match entier à 50 % de premières sans recalibrer, car il est habitué à un certain niveau de risque. [Proposé] l'IA choisit sa jauge d'après sa capacité **perçue** (ses stats habituelles), pas sa capacité réelle du moment, et ne la réestime qu'avec retard ; la vitesse d'ajustement dépend de la Lecture du jeu et de la Concentration. Le joueur humain voit ses pourcentages réels sur le tableau de bord et peut recalibrer lui-même, ce qui est une partie du jeu. (Attention au principe 6 : un « jour sans » ne doit pas devenir une variable de forme cachée ; à réexaminer après les premières mesures.)

### 6.4 Résolution d'un coup [Proposé]

1. **Contexte** : difficulté de la balle reçue (vitesse, effet, profondeur, rebond) + position du joueur par rapport au point d'impact (via l'ellipse) → temps disponible.
2. **Qualité de contact** = f(stat du coup, difficulté de la balle reçue, type de revers, surface, fatigue, pression mentale, taille).
3. **Dispersion autour de la zone visée** = f(qualité de contact, jauge de puissance, Toucher pour les petits coups, effet choisi). Le risque émerge de la géométrie : viser une zone de bord est plus risqué parce que sa cible est proche des lignes.
4. **Issue** : filet / faute (long ou large) / balle en jeu. Le lift protège contre le filet et le long, le plat est plus risqué.
5. **Pression infligée** (vitesse, profondeur, angle, effet) → devient la difficulté de la balle de l'adversaire. Si la balle sort de l'ellipse de couverture adverse, c'est un coup gagnant.

Agressivité côté humain [Proposé] : un joueur peu agressif subit un malus sur « attaque » et « risqué » ; un très agressif subit un malus sur « défense ». Créativité côté humain : bonus de réussite sur les options atypiques.

### 6.5 Couverture du terrain et anticipation [Décidé]

- La couverture de chaque joueur est une **ellipse** dont le joueur est l'un des foyers ; l'autre foyer est dans la direction où il regarde (taille et vitesse liées à Déplacements et Taille ; l'Explosivité agit sur la forme de l'ellipse, voir 4.2).
- Avant le coup adverse, le joueur peut **pivoter son ellipse** d'un côté pour anticiper.
- **Lecture du jeu** (IA) : l'IA anticipe d'après l'historique des coups du joueur (un joueur qui joue toujours en essuie-glace est anticipé du côté ouvert) et d'après un signal bruité sur le coup qui vient d'être frappé (elle décide après que le joueur a frappé). Plus la stat est haute, plus l'anticipation est correcte.
- **Lecture du jeu côté humain** [Proposé, à trancher] : le tableau de bord affiche un indice (direction probable, effet reconnu) dont la fiabilité dépend de la stat.
- Les coups puissants laissent moins de temps de lecture ; les coups déguisés (Toucher, Créativité) brouillent le signal [Proposé].

### 6.6 Choix de coup par l'IA [Décidé pour le principe]

L'IA calcule l'issue attendue de plusieurs coups candidats (zone × effet × puissance) puis en choisit un **avec pondération** (pas toujours le meilleur), pour ne pas être répétitive. Mapping proposé [Proposé] :
- **Lecture du jeu** → précision de l'évaluation (moins de bruit sur les issues estimées).
- **Créativité** → bonus sur les coups candidats moins bien classés (mécanisme ci-dessous).

**Score des coups candidats** [Décidé pour le principe, formules Proposées] : pour chaque coup candidat (ex. coup droit lifté croisé, amorti long de ligne, coup droit à plat long de ligne), l'IA calcule son **pourcentage de réussite** et sa **qualité** (avantage procuré) d'après le contexte : balle reçue, positions des deux joueurs, qualité du joueur. Un **score** combine les deux selon l'appétence : un joueur agressif accorde plus de poids à l'avantage, un joueur prudent plus de poids à la poursuite de l'échange (donc à la réussite). Les coups sont classés par score, puis choisis avec pondération.

**Application de la Créativité en deux passes** [Proposé, pour éviter une dépendance circulaire] : (1) calcul des scores de base et classement ; (2) bonus de Créativité sur la **réussite** (plus naturel que la qualité, qui relève davantage de la technique et de la puissance), pour les coups classés derrière le meilleur, proportionnel à l'écart avec le meilleur coup et à la stat technique du coup concerné, borné ; (3) recalcul des scores puis pondération. La pondération s'aplatit donc indirectement : les coups moins intuitifs réussissent plus souvent et deviennent plus compétitifs, sans pénaliser le joueur créatif.

**Pourquoi un vrai bonus** [Décidé] : l'IA ne sera pas assez performante pour exploiter la prévisibilité d'un adversaire. La Créativité doit donc donner un avantage réel aux joueurs qui variaient (Alcaraz, capable de battre régulièrement Sinner par ses variations face à un joueur moins créatif jouant coup droit et revers à cadence élevée). Elle est inutile pour un joueur aux lacunes techniques importantes, d'où la nécessité pour les tout meilleurs de la travailler. Point de vigilance : la Lecture du jeu exploite déjà l'historique des coups, ce qui pénalise un peu les joueurs prévisibles ; éviter un double comptage.
- **Agressivité** → poids du gain brut vs risque dans la fonction d'utilité.

## 7. Archétypes et équilibrage

Les archétypes sont des profils de stats. Liste actuelle :

- **Attaquant de fond de court** (Rublev, Rune) : coup droit et revers très hauts, volée basse, agressivité haute, créativité basse.
- **Serveur-volleyeur** (Rinderknech) : service, volée, agressivité très hauts, créativité haute, endurance, coup droit et revers plutôt bas.
- **Servbot** (Mpetshi Perricard) : service et puissance très hauts, déplacements très bas.
- **Spécialiste de la terre** : endurance, effet, déplacements très hauts ; retour et coup droit hauts ; agressivité et service bas.
- **Complet de fond de court** (Sinner, Zverev, Djokovic) : service, coup droit, revers, déplacements et retour très hauts, volée un peu plus basse. Sinner prend la balle très tôt (agressivité haute), Zverev et Djokovic restent plus loin de la ligne.
- **Attaquant créatif** (Federer, Alcaraz) : hauts partout, agressivité incluse ; Federer a un service exceptionnel, celui d'Alcaraz est bon mais reste plutôt un point faible.
- **Archétypes à ajouter** [Proposé] : Contre-attaquant (déplacements, endurance, retour, constance hauts ; puissance et agressivité basses), Artiste (toucher et créativité hauts, constance basse), Imprévisible (Kyrgios : technique très haute, constance et concentration très basses), Joueur des moments clés (résilience très haute, reste moyen), Retourneur de grande taille (Medvedev : retour et lecture du jeu hauts, service correct, placement reculé), Vétéran rusé (physique en baisse, toucher, lecture et mental au sommet).

### Roue de contres [Proposé, à vérifier par simulation]

L'objectif [Décidé] : une « roue » où chaque style a un contre. Hypothèse de départ :
- Cycle 1 : Attaquant de fond > Serveur-volleyeur (passings) > Contre-attaquant (points raccourcis, moins d'échanges) > Attaquant de fond (absorbe la puissance).
- Cycle 2 : Servbot > Artiste (pas d'échange pour utiliser le toucher) > Retourneur (variations, amortis) > Servbot (neutralise le service).
- Spécialiste de la terre : domine les gros serveurs sur terre, perd son avantage sur gazon.
- Le Complet n'a pas de contre franc mais il est limité par le budget de points : son niveau total est élevé et c'est ce qui le rend rare.
- **Règle** : à niveau global égal (même budget de points), un contre bat sa cible de l'ordre de 55-62 % ; aucun profil ne doit être systématiquement dominant ni inutile sur une surface donnée.
- [Mesuré] Un contre pur entre deux styles a un écart-type d'environ 2 points d'avantage au service, soit environ 57 % de victoires à niveau égal. La cible de 55-62 % correspond donc à des oppositions marquées : ce doit être l'exception, pas la règle.

## 8. Données et calibrage

**Les statistiques ATP sont la bible du jeu** (principe 5). Les statistiques ATP récentes servent à calibrer immédiatement les stats des vrais joueurs : service (1res balles rentrées, aces, doubles fautes, points gagnés sur 1re et 2e), retour, break, **jeux gagnés derrière son service (80,7 % sur dur pour le top 150, mesuré)**, longueur des échanges, tie-breaks, et les indicateurs de **« shot quality »** disponibles pour le service, le retour, le coup droit et le revers. Les données sont à fournir par le créateur ou à récupérer à la demande ; ne jamais inventer de valeur ATP.

Repère de cohérence : un taux de jeux gagnés au service d'environ 80 % correspond à environ 63-64 % des points gagnés derrière son service. Le jeu étant simulé au point, le taux de jeux au service est une **conséquence** à contrôler, pas un paramètre à saisir.

**Contrôle permanent** : à chaque jalon de la roadmap, une simulation de masse doit retrouver les statistiques ATP de référence (voir `AGENT_EQUILIBRAGE.txt`, mesures de fidélité) ; l'agent de relecture (`AGENT_RELECTURE.txt`) vérifie que chaque modification du code n'a pas cassé cette fidélité.

Cibles figées par surface : `objectifs_equilibrage/` pour le dur, `terre/` et `gazon/` pour les autres surfaces, construites par `outils/Chelems.Donnees`. Ce portage C# reproduit à l'identique les fichiers sur dur. Familles contrôlées par la simulation de masse :
- jeux de service tenus et points au service, par tranche de rang ;
- retour par tranche ;
- victoires par tranche et par écart de rang ;
- sets et tie-breaks ;
- dépendance des sets et dispersion par match.

Bilan à date : `docs/bilan_v0_2.md`.

## 9. Conventions de code

- C# récent, nullable activé, un projet de bibliothèque `Chelem.Core` (sans moteur) + des projets de tests + un projet d'interface séparé.
- Toutes les constantes de calibrage (courbes, seuils, coefficients) dans des fichiers de configuration (JSON), jamais dans le code, pour que l'agent d'équilibrage puisse les modifier sans toucher à la logique.
- Aléatoire injectable (graine fixe) pour tous les tests.
- Commentaires et noms de domaine en **français** [Décidé], un seul choix pour tout le projet.
- Écrire d'abord le cœur testable en console, puis l'interface.

## 10. Décisions ouvertes

1. Moteur d'interface (Godot .NET, Unity, MonoGame).
2. Jauge de puissance : continue 1-100 % partout, ou paliers (défense/contrôle/attaque/risqué) pour les coups hors service.
3. Formule de l'OVR (l'échelle des stats est tranchée : 1-99).
4. Lecture du jeu côté humain : indice affiché sur le tableau de bord, oui ou non.
5. Smash et lob : ils relèvent de Volée / Puissance / Toucher, mais cela reste à confirmer.
6. Simulateur de circuit : faut-il simuler chaque échange ou utiliser une version probabiliste allégée du moteur de point ?
7. Règle de décision repos/tournoi du simulateur (limite n°1).
8. ~~Langue du code~~ : tranché, français (section 9).
9. Séparation Régularité (en match) / Constance : la Constance est désormais l'amplitude de la forme (4.3) ; reste à confirmer la Régularité et son nom.
10. Stats touchées par la Forme physique (point de départ proposé : Déplacements, Puissance, Explosivité ; à tester par simulation).
11. Formule de la Confiance (gains et chutes) : sans objet tant que la décision n°14 n'est pas tranchée.
12. ~~Statut des variables dynamiques~~ : tranché par le test du principe 6 (forme du jour et Constance codées, confiance suspendue, voir 4.6).
13. Roadmap par jalons (à valider, voir section 11 et `docs/bilan_v0_2.md`).
14. Conflit Confiance / données ATP : garder suspendu ou abandonner ?
15. Population de test : le lien rang ↔ talent du simulateur de circuit est plus « plat » que l'ATP, ce qui fausse les victoires à 0-10 places et à plus de 160 places. Faut-il corriger le simulateur ou seulement la population de test ?
16. Source de données pour le court pace index par tournoi.

## 11. Travail avec les agents

- **Agent d'équilibrage** : brief dans `AGENT_EQUILIBRAGE.txt`. Il lit ce fichier comme source de vérité des intentions de design. Toute modification de design importante se fait ici d'abord.
- **Agent de relecture** : brief dans `AGENT_RELECTURE.txt`. Il relit le code (qualité, bugs, conformité aux conventions de la section 9) et vérifie à chaque étape que les simulations restent fidèles aux statistiques ATP (principe 5).
- Les deux agents considèrent les statistiques de l'ATP comme la bible du jeu.
