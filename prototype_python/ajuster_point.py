"""Ajuste p_point(écart de talent) sur les objectifs figés (objectifs_equilibrage/, dur, top 150).

Population simulée : couples (joueur, adversaire) tirés comme dans saison_calibree.simuler_classement_bruite —
rang et talent des 17 saisons du simulateur C#, adversaire tiré de la répartition ATP réelle (adversaires_dur.csv),
format Bo3/Bo5 tiré avec. Mesures exactes (chaînes de Markov), mises en cache par (écart de talent, format).
- Hold% et points gagnés au service par tranche : point de vue joueur (comme les cibles).
- Victoires du mieux classé, sets, tie-breaks : niveau match, mieux classé dans le top 150 (comme les cibles).
Ajustement : chi2 binomial pondéré par les effectifs ATP, sur Hold% par tranche, points au service par tranche
et victoires par écart. Sets et tie-breaks ne sont pas ajustés : ils servent de contrôle indépendant.
"""
import csv
import json
import math
import random
import sys
from collections import defaultdict
from functools import lru_cache

import point as P

OBJ = sys.argv[1] if len(sys.argv) > 1 else "/home/claude/objectifs_equilibrage"
SIMC = "/home/claude/atp_data/sim_classements.csv"
CASES = [(1, 1), (2, 3), (4, 5), (6, 10), (11, 15), (16, 20), (21, 30), (31, 40), (41, 50),
         (51, 65), (66, 80), (81, 100), (101, 125), (126, 150)]
TR = {"1-1": (1, 1), "2-5": (2, 5), "6-10": (6, 10), "11-20": (11, 20), "21-50": (21, 50),
      "51-100": (51, 100), "101-150": (101, 150)}
EC = {"0-5": (0, 5), "6-10": (6, 10), "11-20": (11, 20), "21-40": (21, 40), "41-80": (41, 80),
      "81-160": (81, 160), "161+": (161, 10 ** 9)}


def dans(x, d):
    for nom, (a, b) in d.items():
        if a <= x <= b:
            return nom


def dans_cases(x):
    for a, b in CASES:
        if a <= x <= b:
            return (a, b)


def charger_cibles():
    c = {}
    for l in csv.DictReader(open(f"{OBJ}/hold_et_service_par_rang.csv")):
        bo, t = int(l["best_of"]), l["tranche_rang"]
        c[("hold", bo, t)] = (float(l["hold_pct"]) / 100, int(l["jeux_service"]))
        c[("pts", bo, t)] = (float(l["points_gagnes_service_pct"]) / 100, int(l["points_service"]))
    for l in csv.DictReader(open(f"{OBJ}/resultats_par_ecart_de_rang.csv")):
        c[("vict", int(l["best_of"]), l["ecart_places"])] = (float(l["victoire_mieux_classe_pct"]) / 100, int(l["matchs"]))
    for l in csv.DictReader(open(f"{OBJ}/scores_sets_et_tiebreaks.csv")):
        c[("score", int(l["best_of"]), l["mesure"])] = (float(l["valeur_pct"]) / 100, int(l["effectif"]))
    return c


def construire_population(tirages_par_joueur=60, seed=7):
    rng = random.Random(seed)
    pool = defaultdict(list)
    for l in csv.DictReader(open(f"{OBJ}/adversaires_dur.csv")):
        pool[dans_cases(int(l["rang_joueur"]))].append((int(l["rang_adversaire"]), int(l["best_of"])))
    par_annee = defaultdict(dict)
    for l in csv.DictReader(open(SIMC)):
        par_annee[int(l["annee"])][int(l["rang"])] = int(l["talent"])
    joueur = defaultdict(int)   # (bo, tranche, écart de talent servant - relanceur) -> nb
    match = defaultdict(int)    # (bo, écart de places, écart de talent fav - out) -> nb
    for an, table in par_annee.items():
        rmax = max(table)
        for r in range(1, 151):
            if r not in table:
                continue
            for _ in range(tirages_par_joueur):
                ro, bo = rng.choice(pool[dans_cases(r)])
                ro = min(ro, rmax)
                if ro == r or ro not in table:
                    continue
                d = table[r] - table[ro]
                joueur[(bo, dans(r, TR), d)] += 1
                if r < ro:
                    match[(bo, dans(ro - r, EC), d)] += 1
    return joueur, match


def mesurer(cfg, joueur, match):
    @lru_cache(None)
    def pj(d):
        return P.p_jeu(P.p_point(d, 0, cfg))

    @lru_cache(None)
    def rm(d, bo):
        pa, pb = P.p_point(d, 0, cfg), P.p_point(-d, 0, cfg)
        return P.resume_match(pa, pb, 3 if bo == 5 else 2)

    acc = defaultdict(lambda: [0.0, 0])
    for (bo, t, d), n in joueur.items():
        acc[("hold", bo, t)][0] += n * pj(d)
        acc[("hold", bo, t)][1] += n
        acc[("pts", bo, t)][0] += n * P.p_point(d, 0, cfg)
        acc[("pts", bo, t)][1] += n
    sc = defaultdict(lambda: [0.0, 0])
    for (bo, e, d), n in match.items():
        v, ns, tb, se = rm(d, bo)
        acc[("vict", bo, e)][0] += n * v
        acc[("vict", bo, e)][1] += n
        for k, x in ns.items():
            sc[(bo, f"matchs_en_{k}_sets")][0] += n * x
        sc[(bo, "_n")][0] += n
        sc[(bo, "tb")][0] += n * tb
        sc[(bo, "sets")][0] += n * se
    m = {k: v[0] / v[1] for k, v in acc.items()}
    for bo in (3, 5):
        n = sc[(bo, "_n")][0]
        for k in list(sc):
            if k[0] == bo and k[1].startswith("matchs_en"):
                m[("score", bo, k[1])] = sc[k][0] / n
        m[("score", bo, "sets_au_tiebreak")] = sc[(bo, "tb")][0] / sc[(bo, "sets")][0]
    return m


def chi2(m, cibles, familles=("hold", "pts", "vict")):
    s = 0.0
    for k, (y, n) in cibles.items():
        if k[0] in familles and k in m:
            p = min(max(m[k], 1e-6), 1 - 1e-6)
            s += n * (y - p) ** 2 / (p * (1 - p))
    return s


def ajuster(cibles, joueur, match, h_libre=True):
    best = None
    for c in [0.640 + 0.0025 * i for i in range(7)]:
        for k in [0.0010 + 0.0002 * i for i in range(16)]:
            for h in ([0.03, 0.04, 0.05, 0.06, 0.08, 0.20] if h_libre else [0.30]):
                cfg = {"centre": c, "pente_par_point_de_talent": k, "demi_amplitude": h}
                x = chi2(mesurer(cfg, joueur, match), cibles)
                if best is None or x < best[0]:
                    best = (x, cfg)
    # affinage local
    x0, cfg = best
    pas = {"centre": 0.001, "pente_par_point_de_talent": 0.00005, "demi_amplitude": 0.005}
    for _ in range(4):
        for cle, dp in pas.items():
            if cle == "demi_amplitude" and not h_libre:
                continue
            for sgn in (-1, 1):
                for _r in range(10):
                    essai = dict(cfg)
                    essai[cle] = cfg[cle] + sgn * dp
                    x = chi2(mesurer(essai, joueur, match), cibles)
                    if x < x0:
                        x0, cfg = x, essai
                    else:
                        break
        pas = {k: v / 2 for k, v in pas.items()}
    return x0, cfg


def tableau(m, cibles):
    print(f"{'mesure':<38}{'ATP':>7}{'sim':>7}{'écart':>7}")
    for k in sorted(cibles, key=lambda k: (k[0], k[1])):
        if k in m:
            y = cibles[k][0]
            print(f"{k[0] + ' Bo' + str(k[1]) + ' ' + k[2]:<38}{100*y:7.1f}{100*m[k]:7.1f}{100*(m[k]-y):+7.1f}")


if __name__ == "__main__":
    cibles = charger_cibles()
    joueur, match = construire_population()
    for nom, libre in (("linéaire (sans borne)", False), ("linéaire bornée", True)):
        x, cfg = ajuster(cibles, joueur, match, h_libre=libre)
        print(f"\n### {nom} : chi2 = {x:.0f} ; {json.dumps({k: round(v, 5) for k, v in cfg.items()})}")
        tableau(mesurer(cfg, joueur, match), cibles)
