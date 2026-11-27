# Moteur V0.3 : le point en deux phases — 2026-11-20

Première ébauche complète du point découpé, calée sur les **trois surfaces**. Choix du créateur : avancer vite, puis corriger.

## 1. Ce qui change par rapport à la V0.2

| | V0.2 | V0.3 |
|---|---|---|
| Joueur | un talent (50-99), un style caché, une Constance | trois stats : **Service**, **Retour**, **Échange** (même échelle). Talent = moyenne des trois (OVR provisoire) |
| Point | une probabilité p de gagner au service | **duel au service** (Service contre Retour), puis **échange** (Échange contre Échange), coup par coup |
| Forme, pression, élan, style caché, enjeu | codés et calés | **retirés** pour l'instant (décision du créateur) ; le code reste dans l'historique git (commit `30168b1`) |
| Surfaces | trois centres calés | des **lois de service** et une **vitesse d'échange** par surface |
| Statistiques produites | hold, points au service, balles de break | en plus : 1res balles, aces, doubles fautes, points sur 1re et 2e, issues du service, Conversion% / Steal%, coups gagnants, fautes directes et provoquées, longueur des échanges |

Population de test : les trois stats de chaque joueur valent son talent du simulateur de circuit.

## 2. Le moteur

### Phase 1 : duel au service

1. Première balle rentrée avec la probabilité de la surface (ATP : 61,4 % sur dur, 62,4 % sur terre, 62,8 % sur gazon ; constante en V0.3). Sinon, seconde balle.
2. Issue tirée dans la loi de la balle, propre à la surface :
   - première : ace, service gagnant, attaque, neutre, défense, retour gagnant ;
   - seconde : les mêmes, plus la double faute.
3. Attaque, neutre et défense désignent l'état du serveur pour son coup suivant.

**Service gagnant** (définition du créateur) : tout service hors ace qui n'est pas remis en jeu, c'est-à-dire non retourné, retour en faute provoquée, ou retour raté sur une balle retournable.

### Phase 2 : échange

Le joueur qui frappe est dans un état (attaque, neutre ou défense). Son coup a cinq issues :
- coup gagnant ;
- l'adversaire jouera en défense ;
- neutre ;
- l'adversaire jouera en attaque ;
- faute : **directe** si le frappeur était en attaque ou neutre, **provoquée** s'il était en défense.

On boucle jusqu'à la fin du point. Il n'y a **aucun traitement particulier** du coup qui suit le service (décision du créateur) : l'état laissé par le service suffit.

**Ordre des fautes** (décision du créateur) : la faute est la plus probable en **défense** (on cherche à se sortir d'une situation, sans pouvoir jouer un coup efficace) et la moins probable en **neutre** (on prend plus de risques en attaque). Le calage impose défense > attaque > neutre.

### Le décalage par les stats

Chaque loi est celle de deux joueurs égaux, décalée par l'écart de stats :

```
p_k ∝ base_k × exp(x × orientation_k)
x = pente × écart (+ vitesse dans l'échange)
```

- **Écart** : Service − Retour au service ; Échange du frappeur − Échange de l'adversaire dans l'échange.
- **Orientation** : valeur de l'issue pour le joueur avantagé, entre −1 et +1. Exemples : ace et coup gagnant +1, faute et retour gagnant −1. L'attaque vaut ±0,32, soit 2 × Conversion − 1 = 2 × 0,66 − 1.

### La surface

- **Au service** : chaque surface a ses propres lois de base (`surfaces.<surface>.premiere`, `.seconde`, `.premiere_rentree`). La vitesse n'y joue pas, pour ne pas compter deux fois la surface.
- **Dans l'échange** : la vitesse aide l'attaquant en attaque et pénalise le défenseur en défense. Une surface rapide avantage l'attaque. Les lois de l'échange sont communes aux trois surfaces (calées sur dur).

### Comptage

Conventions du Match Charting Project :
- la longueur compte le service et exclut la faute finale (ace = 1 coup, service gagnant = 1, retour gagnant = 2, double faute = 0) ;
- les aces comptent comme coups gagnants ;
- un service gagnant compte comme faute provoquée du relanceur, **sauf** un retour raté sur une balle retournable, compté en faute directe. La part de ces retours ratés est mesurée par surface (`surfaces.<surface>.fautes_retour` : sur dur 1 % des services gagnants sur première, 67 % sur seconde). C'est un comptage, sans effet sur l'issue du point.

**Fins hors service** (définition du créateur) : aces, services gagnants et doubles fautes exclus, retours gagnants compris.

**Conversion%** se mesure au premier déséquilibre du point, service compris (définition du créateur) :
- les aces, services gagnants, retours gagnants et échanges restés neutres jusqu'au bout sont exclus ;
- Steal% = 100 − Conversion% pour un même déséquilibre.

### Résolution exacte (`AnalysePoint`)

La chaîne d'états se résout sans simulation. On obtient directement :
- la probabilité du point, les points sur 1re et 2e ;
- la longueur des échanges et les fins de point par tranche, et hors service ;
- Conversion% ;
- la valeur de chaque état.

Usages :
- le calage ;
- la version allégée du simulateur de circuit (décision ouverte n°6) ;
- le contrôle de la simulation : un test vérifie que les deux méthodes concordent.

## 3. Données et calage

| Source | Contenu | Fichier |
|---|---|---|
| Créateur (« moyenne du tour » 2023, toutes surfaces) | issues de la 1re et de la 2e balle, 62,82 % de 1res, Conversion% 66 / Steal% 34 | `objectifs_equilibrage/efficacite_service.csv` |
| Match Charting Project 2015-2025 : dur (3 021 matchs), terre (1 275), gazon (452) | longueur et fins des échanges, fins hors service, efficacité du service mesurée (aces, services gagnants, retours ratés) | `echanges_mcp.csv` dans `objectifs_equilibrage/`, `terre/` et `gazon/`, construits par `dotnet run --project outils/Chelems.Donnees -- echanges Hard` (`Clay`, `Grass`) |
| ATP (Sackmann, top 150, par surface) | 1res, aces, doubles fautes, points sur 1re et 2e, hold, etc. | `service_moyen_top150.csv` et suivants |

### L'efficacité du service mesurée confirme celle du créateur

Avec la définition du créateur (service gagnant = service non remis en jeu, hors ace), le MCP retrouve ses chiffres :

| | ace | gagnant | double faute |
|---|---|---|---|
| Première, créateur (2023, tour) | 16 | 22 | — |
| Première, MCP dur / terre / gazon | 14,2 / 8,3 / 15,8 | 24,2 / 21,3 / 26,9 | — |
| Seconde, créateur | 1 | 16 | 10 |
| Seconde, MCP dur / terre / gazon | 0,8 / 0,6 / 1,2 | 15,3 / 13,2 / 18,1 | 9,5 / 8,7 / 9,8 |

Sur seconde balle, l'essentiel des services gagnants sont des retours ratés sur une balle retournable (67 % sur dur) ; le MCP les compte en fautes directes, d'où la règle de comptage de la section 2.

### Lois du service : proches de la réalité

Décision du créateur : les lois du service ne s'éloignent pas de la réalité, quelques points au plus.

- **Référence de chaque surface** : les lois du créateur, où aces et services gagnants sont remplacés par ceux du MCP sur la surface ; attaque, neutre, défense et retour gagnant gardent les proportions du créateur.
- **Calage** : chaque issue paie ((p − référence) / 1,5 point)², autant qu'une cible manquée d'une tolérance (option `--ecart-service`).

Lois obtenues (en %) :

| Première | ace | gagnant | attaque | neutre | défense | retour gagnant |
|---|---|---|---|---|---|---|
| Créateur (2023) | 16 | 22 | 20 | 38 | 3 | 1 |
| Dur | 14,4 | 26,2 | 21,1 | 36,2 | 1,5 | 0,6 |
| Terre | 8,5 | 22,8 | 23,8 | 42,8 | 1,3 | 0,7 |
| Gazon | 15,4 | 28,2 | 19,0 | 33,2 | 1,9 | 2,3 |

| Seconde | ace | gagnant | attaque | neutre | défense | retour gagnant | double faute |
|---|---|---|---|---|---|---|---|
| Créateur (2023) | 1 | 16 | 6 | 58 | 6 | 3 | 10 |
| Dur | 0,5 | 15,6 | 6,8 | 57,9 | 5,6 | 3,7 | 9,9 |
| Terre | 0,6 | 13,7 | 7,0 | 60,4 | 5,4 | 3,6 | 9,3 |
| Gazon | 0,7 | 18,7 | 5,9 | 55,1 | 5,3 | 3,9 | 10,4 |

Plus grand écart avec la référence de la surface : +2,0 points (services gagnants sur première, dur).

### Lois de l'échange (dur, communes aux trois surfaces)

Calées en même temps que le service du dur. Cibles :
- longueur, fins et % du serveur par tranche, et fins hors service (MCP) ;
- points sur 1re et 2e, aces, doubles fautes (ATP) ;
- Conversion% (créateur, dur seulement).

Le calage part d'un profil (`echange.profil`), respecte l'ordre des fautes et s'écarte du profil autant que les données l'exigent.

| État du frappeur | gagnant | adv. en défense | neutre | adv. en attaque | faute |
|---|---|---|---|---|---|
| attaque | 18,3 % | 60,3 % | 8,1 % | 0,9 % | 12,5 % |
| neutre | 6,0 % | 11,0 % | 69,7 % | 1,8 % | 11,5 % |
| défense | 2,3 % | 2,7 % | 28,9 % | 35,3 % | 30,9 % |

Probabilité de gagner le point pour le joueur qui frappe, entre joueurs égaux sur dur :
- en attaque : 63,8 % ;
- en neutre : 49,4 % (un neutre presque vraiment neutre) ;
- en défense : 31,6 %.

### Vitesses et pentes

1. Vitesse de chaque surface calée exactement, le dur servant de référence (0) : terre +0,07, gazon +0,35.
2. Puis pentes et vitesses calées ensemble par simulation de masse sur les trois surfaces (`calibrer --surface toutes --parametres pentes,vitesse`) :
   - **service 0,0175**, **échange 0,0037** par point d'écart (partage 2/3 Service, 1/3 Échange de l'effet d'un point de talent au service) ;
   - **vitesse dur −0,006, terre +0,027, gazon +0,198**.

Commandes :
- `dotnet run --project src/Chelems.Console -- point [--surface terre] [--a 75 --b 75 --service-a 85 ...]` : résultats exacts d'un point, comparés aux cibles ;
- `caler-echange [--surface dur] [--rappel 1] [--ecart-service 1.5] [--echange] [--ecrire]` : sur dur, service + échange + pentes ; ailleurs, service + vitesse ;
- `match`, `fidelite`, `calibrer`, `bareme` comme avant.

## 4. Décisions et conflits (principe 5)

### 4.1 Points sur première — tranché

- Le bonus du coup suivant le service est **supprimé**.
- Les lois du service restent à moins de 2 points de la réalité mesurée.
- L'écart restant est absorbé par l'échange : le joueur qui frappe en neutre gagne 49,4 % des points (43 % au premier calage).
- Résultat sur dur, simulation de masse : 72,0 % contre 73,0 % à l'ATP (72,5 % entre joueurs égaux).

### 4.2 Aces — tranché

Aces mesurés par surface dans le MCP. Simulation : 9,0 / 5,5 / 9,9 % des points au service (ATP 8,9 / 5,5 / 9,9).

### 4.3 Fins de point — tranché

Avec la définition du créateur (hors service), le MCP sur dur donne **33,5 % de coups gagnants, 24,6 % de fautes provoquées, 41,9 % de fautes directes**. C'est dans ses fourchettes (25-35, 25-30, 35-45), à la limite basse pour les fautes provoquées. Simulé : 33,7 / 23,2 / 43,0.

Sur terre, le MCP donne 34,3 % de coups gagnants hors service, autant que sur dur. Le créateur pense qu'ils y sont plus rares, et cherchera d'autres sources.

### 4.4 Vitesses : une compensation — accepté

Les vitesses calées par simulation de masse compensent l'absence de forme : elles devront être recalées quand la forme reviendra.

### 4.5 Ouvert : la terre n'est pas « lente » dans l'échange

D'après le MCP, une fois l'échange lancé, la terre n'est pas plus défensive que le dur. Ce qui allonge ses points, c'est le service : moins d'aces, moins de services gagnants. Les vitesses calées vont dans ce sens (terre +0,07 au calage exact, +0,03 après la simulation de masse).

Le principe « les surfaces lentes avantagent la défense » voudrait une vitesse négative. À trancher par le créateur, idéalement avec une source sur les coups gagnants par surface.

### 4.6 Gazon : la vitesse ne joue pas assez sur l'avantage offensif

Les points du gazon sont un peu trop longs (1-3 coups : 62,4 % contre 65,6 %), et les échanges de 4-6 coups n'y finissent pas assez sur un coup gagnant (35 % contre 40 %).

Lecture du créateur : la vitesse ne joue pas assez sur l'avantage offensif. Le calage exact donnait au gazon une vitesse de +0,35 ; la compensation de l'absence de forme la ramène à +0,20, ce qui raccourcit moins les points.

Écart jugé satisfaisant pour l'instant (créateur).

## 5. Résultats (simulation de masse, environ 101 000 matchs par surface, graine 1)

Statuts : **dur 107 ok, 29 limite, 60 écart ; terre 116 / 24 / 54 ; gazon 110 / 31 / 53**. Détail : `resultats/fidelite_v0.3.txt`.

**Ce qui est fidèle** (ATP ou MCP → simulé) :

| Mesure | Dur | Terre | Gazon |
|---|---|---|---|
| Hold moyen (top 150) | 80,7 → 80,2 | 76,7 → 76,2 | 83,9 → 83,2 |
| Points au service | 64,5 → 63,6 | 62,2 → 61,4 | 66,3 → 65,3 |
| Points sur 1re | 73,0 → 72,0 | 69,2 → 68,1 | 74,5 → 73,3 |
| Points sur 2e | 51,0 → 50,2 | 50,8 → 50,2 | 52,4 → 51,8 |
| Conversion% | 66 → 66,2 | (pas de cible) 66,9 | (pas de cible) 71,0 |
| Points de 1-3 coups (MCP) | 59,7 → 58,9 | 54,4 → 54,7 | 65,6 → 62,4 (écart) |
| Gagnants / provoquées / directes hors service (MCP) | 33,5 / 24,6 / 41,9 → 33,7 / 23,2 / 43,0 | 34,3 / 23,8 / 41,8 → 33,7 / 23,5 / 42,8 | 35,5 / 27,0 / 37,4 → 35,4 / 25,2 / 39,4 |
| Hold Bo3 par tranche de rang | ok ou limite | ok ou limite, sauf le n°1 (81,2 → 85,7) | ok ou limite |
| Sets au tie-break, Bo3 | 20,0 → 20,9 | 16,3 → 17,3 | 24,7 → 23,4 |

**Ce qui ne l'est pas, et pourquoi** (dur, sauf mention) :

| Écart | ATP → simulé | Cause |
|---|---|---|
| Dispersion par match (écart-type, 0-5 places) | 12,9 → 9,8 | **pas de forme** |
| Même vainqueur aux sets 1 et 2 | 61,8 → 50,6 | pas de forme du match |
| Matchs en 2 sets (Bo3), en 5 sets (Bo5) | 63,5 → 58,0 ; 20,1 → 28,0 | pas de forme |
| Favoris trop forts : rangs 2-5 (Bo3), 81-160 places | 72,9 → 79,9 ; 70,1 → 76,8 | pas de forme (et population de test au-delà de 160 places) |
| Retour du n°1 en Bo5 | 33,7 → 28,8 | trois stats égales au talent : le n°1 est un grand relanceur |
| Le serveur gagne trop peu les échanges de 4-6 coups (toutes surfaces) | 39,3 → 35,7 | les lois de l'échange ne dépendent pas du numéro du coup |
| Gazon : points trop longs | 1-3 coups 65,6 → 62,4 | voir 4.6 |

## 6. Suite

La roadmap est dans CLAUDE.md, section 12 (décidée le 2026-11-21) : un jeu jouable d'abord. Prochaine étape : lier les stats (Taille, Service, Retour, Puissance, Explosivité) au détail de l'efficacité au service, par calibrage sur des profils de joueurs réels.

Écarts de la V0.3 laissés en l'état (moteur V0, non utilisé tel quel par le jeu) : ceux dus à l'absence de forme, le retour du n°1, les échanges de 4-6 coups, les points du gazon. Forme, pression et élan reviendront à l'étape 7, selon la définition de CLAUDE.md 4.6.
