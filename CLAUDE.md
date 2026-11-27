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

**Moteur V0.3** [Décidé, 2026-11-20] : première ébauche complète. On construit un jeu complet d'abord, on le corrige ensuite. Le moteur n'utilise pour l'instant que **trois stats** :
- **Service** : duel au service, côté serveur ;
- **Retour** : duel au service, côté relanceur ;
- **Échange** : tout le reste du point, non découpé.

Les stats détaillées ci-dessous restent la cible. Elles remplaceront Échange (puis Service et Retour) par étapes, en gardant la fidélité ATP. Structure du point : voir 6.7.

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
| Régularité | [Proposé] Stat **en match** : capacité à réussir plusieurs fois le même coup (réduit la dispersion d'exécution) ; augmente le taux de deuxièmes balles rentrées. [Décidé, codé 2026-11-25] Au service : une part des premières balles et la plus grande part des doubles fautes (`docs/etape1_service_stats.md`, section 5). À classer entre technique et mental |
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

**Retirées du moteur V0.3** [Décidé, 2026-11-20] : forme, pression, élan, enjeu et style caché. Elles reviendront, exprimées sur les stats, une fois le point en deux phases satisfaisant. Leur absence explique les écarts actuels sur la dispersion, la dépendance des sets et la force des favoris (`docs/moteur_v0_3.md`). Les mesures ci-dessous restent valables.

**Retour dans le point en deux phases** [Décidé pour le principe, 2026-11-20 ; formules À calibrer sur les résultats] : forme et pression deviennent des **décalages des issues** de l'échange (et du service), comme l'écart de stats.
- **Forme positive** : transforme des fautes en coups gagnants ou en coups qui mettent l'adversaire en défense ; forme négative, l'inverse.
- **Pression élevée sans Résilience** : « bras qui se crispe » ; moins de coups gagnants et de coups offensifs, plus de neutres, de défenses et de fautes.

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
- [Mesuré, 2026-11-26, conflit ouvert n°27] Dans le Match Charting Project, la gêne au retour ne vaut qu'environ le tiers de l'avantage au service : pour 10 cm de plus, le serveur gagne +3,3 points sur première ; face à un relanceur plus grand de 10 cm, +1,1 (moins d'aces concédés grâce aux grands bras, mais plus de services gagnants, au corps). Le moteur suit la mesure.
- [Décidé, 2026-11-25] La taille est corrélée à la Puissance (positivement) et à l'Explosivité (négativement), pas à la stat de Service. Codé : population Puissance +0,11 et Explosivité −0,15 point par cm (valeurs les plus fortes compatibles avec un effet direct de la taille dans le bon sens).
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
- **Moteur V0.3** [Décidé, 2026-11-20] : chaque surface a ses **lois du duel au service** (efficacité du créateur, où aces et services gagnants sont ceux mesurés sur la surface dans le Match Charting Project ; elles ne s'en écartent que de 2 points au plus) et une **vitesse** qui agit dans l'échange seulement. Les lois de l'échange sont communes (calées sur dur).
- [Mesuré, conflit ouvert n°21] D'après le Match Charting Project, une fois l'échange lancé, la terre n'est pas plus défensive que le dur (longs échanges plus souvent finis sur un coup gagnant). Ce qui allonge les points sur terre, c'est le service (moins d'aces et de services gagnants). Le calage donne donc à la terre une vitesse proche du dur.
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

**Agressivité et prise de risque au service** [Proposé par le créateur, 2026-11-21 ; Mesuré] : plus l'Agressivité est haute, plus le joueur prend de risques au service : ses services sont meilleurs, mais sa première passe moins. Ce que disent les données (MCP, dur, 76 joueurs, à taille égale), pour 10 points de premières balles en moins :
- +1,6 point de services « libres » (aces et services gagnants) sur première ;
- points gagnés sur première inchangés (+0,05 par point) ;
- +1,5 double faute pour 100 secondes, et −4 points gagnés sur seconde.

Le risque touche donc les **deux balles**. Sur première, il rapporte des points gratuits sans changer le total des points gagnés. Les chiffres ATP sur 52 semaines (81 joueurs, sans la taille) vont dans le même sens : moins de premières, plus de doubles fautes (r = −0,47) et moins de points sur seconde (r = 0,30). La capacité (Service, Taille, Puissance) décale la courbe : Zverev ou Sinner ont à la fois beaucoup de premières et un bon service.

**Codé (roadmap, étape 1, 2026-11-25)** : chaque stat décale les issues du duel au service (`service.stats`, `docs/etape1_service_stats.md`). [Décidé] Le Service (placement, effets) donne des services gagnants et un coup suivant en attaque ; la Puissance (vitesse) donne des aces et des services gagnants. Le Retour s'oppose au Service du serveur, l'Explosivité à sa Puissance : ce sont des duels entre deux joueurs, jamais un handicap pour le joueur qui a la stat (Nadal, puissant et grand relanceur, est avantagé des deux côtés). Seule la Taille agit sur les deux rôles d'un même joueur. [Décidé] Le risque paie sur la première : à capacité de référence, l'Agressivité ne change pas le total des points au service (moins de premières et plus de doubles fautes, compensés par plus de points gagnés sur première). [Proposé] Échelle : de 1 à 99, la part de premières passe d'environ 72 % à 56 %.

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

### 6.7 Structure du point (moteur V0.3) [Décidé, 2026-11-20]

Le point est une **chaîne d'états**. L'état après chaque coup donne l'état suivant ; on ne tire pas l'issue du point d'un coup.

1. **Duel au service** (Service du serveur contre Retour du relanceur). Issues :
   - ace, service gagnant, retour gagnant, double faute (seconde balle seulement) ;
   - ou l'état du serveur pour son coup suivant : attaque, neutre, défense.

   Les lois sont l'efficacité du service fournie par le créateur (moyenne du tour 2023), avec les aces et services gagnants mesurés sur chaque surface (MCP). [Décidé] Une fois les stats fiables, on ne s'en éloigne pas : quelques points au plus, jamais 15. Pour recaler, on change l'efficacité, pas le fonctionnement du jeu ; aucun traitement particulier du coup qui suit le service. Service gagnant = service hors ace non remis en jeu (non retourné, retour en faute provoquée ou raté sur une balle retournable). L'efficacité d'un joueur se déduit de ses aces, de ses services non retournés et de ses points sur première (ex. Draper 30,4 / 21 / 23,6 / 20 / 3,5 / 1,5 : la qualité et la puissance de son service transforment des services neutres en services avantageux).
2. **Échange** (Échange contre Échange). À chaque coup, selon l'état du frappeur (attaque, neutre, défense), cinq issues :
   - coup gagnant ;
   - adversaire en défense ;
   - neutre ;
   - adversaire en attaque ;
   - faute : **directe** si elle est faite en attaque ou en neutre, **provoquée** si elle est faite en défense.

   On boucle jusqu'à la fin du point. [Décidé] La faute est la plus probable en **défense** (on cherche à se sortir d'une situation, sans pouvoir jouer un coup efficace) et la moins probable en **neutre** (on prend plus de risques en attaque) ; le calage impose cet ordre.

Définitions :
- **Conversion%** : points gagnés par le joueur en attaque au **premier déséquilibre** du point (service compris). Aces, services gagnants, retours gagnants et échanges restés neutres sont exclus.
- **Steal%** = 100 − Conversion% pour un même déséquilibre. Moyenne du tour : 66 / 34.

Surfaces : des lois de service et une **vitesse** d'échange par surface (voir 5.3). Les surfaces rapides avantagent l'attaque, les lentes la défense.

Chaque loi est celle de deux joueurs égaux, décalée par l'écart de stats. Une résolution exacte, sans simulation, existe (`AnalysePoint`). Détail, calage et résultats : `docs/moteur_v0_3.md`.

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

Depuis la V0.3 :
- **Échanges** : longueur, % du serveur par longueur, coups gagnants, fautes provoquées et directes. Source : Match Charting Project 2015-2025, fichiers `echanges_mcp.csv` de chaque surface (`outils/Chelems.Donnees -- echanges Hard|Clay|Grass`).
- **Efficacité du service et Conversion% / Steal%** : moyenne du tour 2023 fournie par le créateur, fichier `objectifs_equilibrage/efficacite_service.csv`. Conversion% / Steal% sont une cible du dur ; le détail de l'efficacité n'est plus une cible, c'est le point de départ des lois de service de chaque surface.
- Fins de point **hors service** (définition du créateur : aces, services gagnants et doubles fautes exclus, retours gagnants compris) : 25-35 % de coups gagnants, 25-30 % de fautes provoquées, 35-45 % de fautes directes. [Mesuré] MCP dur : 33,5 / 24,6 / 41,9. Le MCP compte les retours ratés sur une balle retournable en fautes directes : le moteur fait de même (comptage seulement, `fautes_retour`).
- **Premières balles, aces, doubles fautes, points sur 1re et 2e** : ATP, déjà figés.
- **Efficacité au service par joueur** (roadmap, étape 1) : Match Charting Project, dur 2015-2025, 77 joueurs (au moins 1 500 points au service et au retour), avec la taille de l'ATP. Fichier `objectifs_equilibrage/efficacite_joueurs.csv` (`outils/Chelems.Donnees -- efficacite-joueurs Hard`), lu par `dotnet run --project src/Chelems.Console -- efficacite-joueurs`.
- **Classements ATP sur 52 semaines** (service, retour, points sous pression ; 81 joueurs, toutes surfaces, relevé du 2026-11-21) : `objectifs_equilibrage/atp_classements_52_semaines.csv`. Base principale pour estimer les qualités des joueurs actuels (créateur). Le détail de l'efficacité au service par joueur (attaque, neutre, défense) vient d'entreprises privées : il n'est pas disponible.

Bilan à date : `docs/bilan_v0_2.md`, puis `docs/moteur_v0_3.md`.

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
13. ~~Roadmap par jalons~~ : tranché, voir section 12.
14. Conflit Confiance / données ATP : garder suspendu ou abandonner ?
15. Population de test : le lien rang ↔ talent du simulateur de circuit est plus « plat » que l'ATP, ce qui fausse les victoires à 0-10 places et à plus de 160 places. Faut-il corriger le simulateur ou seulement la population de test ?
16. Source de données pour le court pace index par tournoi.
17. ~~Points sur première (V0.3)~~ : tranché, bonus du coup suivant le service supprimé ; l'efficacité du service est ajustée par surface.
18. ~~Aces par surface~~ : tranché, lois de service extrapolées par surface depuis celles du créateur.
19. ~~Définition des fins de point~~ : tranché, hors service (aces, services gagnants, doubles fautes exclus) ; le MCP concorde avec les fourchettes du créateur.
20. Vitesses (dur −0,43, terre −0,41, gazon −0,14 au 2026-11-26) : elles compensent l'absence de forme [accepté], à remonter quand la forme reviendra.
21. Vitesse de la terre : les données du MCP la placent au niveau du dur (l'échange n'y est pas plus défensif ; la lenteur passe par le service). Garder la vitesse tirée des données, ou imposer une vitesse négative ? Le créateur pense que les coups gagnants sont plus rares sur terre (le MCP ne le montre pas hors service : 34,3 % contre 33,5 % sur dur) et cherchera d'autres sources.
22. Gazon : la vitesse ne joue pas assez sur l'avantage offensif (points un peu trop longs, 1-3 coups 62,4 % contre 65,6 %). Écart jugé satisfaisant pour l'instant.
23. ~~Agressivité et seconde balle~~ : tranché, le risque paie sur la première et l'Agressivité est neutre au total à capacité de référence. [Proposé, étape 4] Faire dépendre le gain du risque de la capacité (un grand serveur a intérêt à risquer).
24. ~~Service et Puissance~~ : tranché, Service = services gagnants et attaque, Puissance = aces et services gagnants.
25. ~~Doubles fautes~~ : tranché, la Régularité donne la plus grande part des doubles fautes et une part des premières balles (l'autre part revient à l'Agressivité) ; le Service n'y touche plus.
26. ~~Spécialisation~~ : tranché, la population de test a des profils Service / Retour / Échange : Service et Retour suivent 41 % du talent, plus un écart de 8 points, et l'Échange compense. Les profils reproduisent la dispersion et la corrélation ATP (−0,28). Point de vigilance : la corrélation des écarts bute sur −0,95 (profils presque tous « serveur » ou « relanceur »). [2026-11-26] Le créateur attribue la corrélation négative surtout à la taille ; codé (section 5.1), mais la taille n'explique pas tout : avec une corrélation des profils bornée à −0,6, la population obtient −0,01 au lieu de −0,28, et la dispersion du service est trop forte (3,7 contre 2,95).
27. Gêne au retour d'un grand joueur : un tiers de l'avantage au service d'après le MCP, contre « à l'égal » au §5.1. Le moteur suit la mesure.
28. Retour estimé des joueurs réels corrélé à la taille (+0,34), alors qu'il ne devrait pas l'être. Piste : l'Échange n'est estimé que par les points au service ; l'estimer sur le service et le retour ensemble. [2026-11-26] Après les corrections (artefact des aces, effets par balle) : +0,28, et des profils encore peu crédibles. Laissé comme limite de l'estimation ; le moteur n'est pas en cause.

## 11. Travail avec les agents

- **Agent d'équilibrage** : brief dans `AGENT_EQUILIBRAGE.txt`. Il lit ce fichier comme source de vérité des intentions de design. Toute modification de design importante se fait ici d'abord.
- **Agent de relecture** : brief dans `AGENT_RELECTURE.txt`. Il relit le code (qualité, bugs, conformité aux conventions de la section 9) et vérifie à chaque étape que les simulations restent fidèles aux statistiques ATP (principe 5).
- Les deux agents considèrent les statistiques de l'ATP comme la bible du jeu.

## 12. Roadmap [Décidé, 2026-11-21]

Priorité : **un jeu jouable et agréable**, avant le simulateur de circuit. Tant que le jeu n'est pas jouable, on ne peut pas juger si le rendu est réaliste et intéressant. On avance étape par étape, sans chercher à parfaire chaque moteur V0 : leurs petits écarts aux statistiques ATP ne sont pas graves, car le jeu ne les utilisera pas tels quels.

1. **Service et retour par les stats.** Lien entre les stats et le détail de l'efficacité au service (ace, gagnant, attaque, neutre, défense, retour gagnant ; double faute sur seconde). Stats utilisées : Taille, Service, Retour, Puissance, Explosivité. Rien de compliqué techniquement : du calibrage, sur des profils de joueurs réels et leurs cibles (aces, services non retournés, points sur 1re et 2e, retour ; ATP et Match Charting Project). Exemple de Draper : 30,4 / 21 / 23,6 / 20 / 3,5 / 1,5. **Fait (2026-11-25)** : `docs/etape1_service_stats.md`. **Figé le 2026-11-26** (section 7) : effets par balle selon le créateur (Puissance sur la première seulement, Explosivité au retour surtout sur la première, Échange absent du duel au retour), simulation de masse dur 118 / terre 126 / gazon 123 ok, 120 tests. Limite connue : l'estimation du Retour des joueurs réels reste peu fiable (à reprendre avec des données par saison). [Décidé, 2026-11-25] Échelle des stats : répartition normale autour de 70 (population de test : 72,8 ± 6,4), très peu de joueurs au-delà de 90, 99 réservé à une saison parfaite (Roddick 2003). Puissance et Explosivité sont des stats de style, indépendantes du classement. Estimation des joueurs réels avec un a priori normal. Décisions ouvertes n°25 et 26. Reste à estimer Retour et Explosivité des joueurs réels avec leurs statistiques au retour.
2. **Moteur coup par coup avec décisions.** Le point avance coup par coup en attendant une décision (joueur humain ou IA), au lieu d'être tiré d'un bloc. Changement d'architecture du cœur, fait avant l'interface pour que celle-ci reste fine.
3. **Terrain et interface graphique.** Terrain à l'échelle vu de dessus, joueurs, balle, tableau de bord (§6.1), sans soin graphique au début. Moteur d'interface à trancher (décision ouverte n°1) ; [Proposé] Godot 4 .NET.
4. **Service et retour physiques.** Les stats deviennent vitesse, angle et effet du service ; la couverture du relanceur (ellipse, temps de réaction) décide de la difficulté du retour. [Proposé] Modèle hybride : la physique donne la difficulté, une table de probabilités donne l'issue (§6.4), pour garder la main sur les objectifs de l'étape 1. Choix de la jauge de service par le joueur et par l'IA (§6.3). À intégrer alors [créateur, 2026-11-26] : la stat **Effet** sur la seconde balle (rotation, kick) et un poids plus fort de l'Agressivité sur la seconde. Exemple : Shelton, première terrifiante par la puissance, et probablement la meilleure seconde du circuit, par son agressivité et surtout l'effet qu'il met dans la balle, pas par sa puissance.
5. **Échanges jouables.** D'abord des choix simples (zone parmi 9, effet, puissance), puis le placement et l'anticipation (§6.2, §6.5).
6. **IA de choix de coup** (§6.6).
7. **Forme, pression et élan**, selon la définition de 4.6.
8. **Simulateur de circuit**, plus tard, pour donner un contexte au jeu.

Points à trancher par le créateur, liés à cette roadmap :
- [Proposé] **Principe 5** : contrôle de fidélité à chaque jalon, avec des tolérances larges pour les moteurs V0, sans bloquer l'avancée (aujourd'hui : « absolument à tout instant »).
- [Proposé] **Principe 4** : le moteur physique du jeu remplacera à terme le moteur V0.3, qui pourra devenir la version allégée du simulateur de circuit, recalée sur le moteur physique, pour ne pas maintenir deux moteurs qui divergent.
