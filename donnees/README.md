# Données d'entrée

## `sim_classements.csv`

Talent et rang de fin de saison des joueurs du simulateur de circuit C# du créateur, 17 saisons (2055 à 2071, environ 504 joueurs par saison).

- **Source :** `docs/Classements 55+.xlsx`, colonnes A (Rang) et G (Talent) de chaque onglet « Année 20xx ». Extrait le 2026-11-18.
- **Usage :** population des simulations de masse (`PopulationAtp`). Le rang y est obtenu aux points, donc bruité par rapport au talent, comme l'exige la règle 3 du README des objectifs.
- Ce ne sont **pas** des données ATP : ce sont les talents du simulateur, que la V0.1 prend comme niveau d'entrée.
