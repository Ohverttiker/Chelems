"""Option B : combien de dispersion d'un match à l'autre faut-il pour reproduire l'ATP ?

Hypothèse testée (sans variable dynamique codée dans le jeu, simple mesure) : dans chaque match, l'écart de
talent effectif vaut écart réel + e, avec e ~ N(0, sigma) tiré une fois par match et symétrique (ce que
produiraient, par exemple, des oppositions de styles avec des profils de stats détaillés).
Pour chaque sigma, on réajuste p_point (centre, pente, borne) sur les mêmes cibles que ajuster_point.py
(Hold% et points au service par tranche, victoires par écart), puis on regarde les contrôles indépendants :
matchs en 2 sets (Bo3), répartition 3/4/5 sets (Bo5), tie-breaks, et « même vainqueur aux sets 1 et 2 ».
Intégration exacte sur e par quadrature de Gauss-Hermite (9 nœuds).
"""
import csv
import json
import math
import sys
from collections import defaultdict
from functools import lru_cache

import numpy as np

import point as P
import ajuster_point as A

NOEUDS, POIDS = np.polynomial.hermite_e.hermegauss(9)
POIDS = POIDS / POIDS.sum()


@lru_cache(None)
def meme_vainqueur_12(pa, pb, sets_gagnants):
    """P(le même joueur gagne les sets 1 et 2), premier serveur tiré au sort."""
    sa = P.distribution_set(pa, pb)
    sb_brut = P.distribution_set(pb, pa)
    sb = {("A" if g == "B" else "B", not s, t): v for (g, s, t), v in sb_brut.items()}
    tot = 0.0
    for d1 in (sa, sb):
        for (g1, a_sert2, _t), v1 in d1.items():
            d2 = sa if a_sert2 else sb
            for (g2, _s, _t2), v2 in d2.items():
                if g1 == g2:
                    tot += 0.5 * v1 * v2
    return tot


def cibles_dependance(obj):
    c = {}
    for l in csv.DictReader(open(f"{obj}/dependance_sets_bo3.csv")):
        c[("meme12", 3, l["ecart_places"])] = (float(l["meme_vainqueur_sets_1_et_2_pct"]) / 100, int(l["matchs"]))
    return c


def mesurer(cfg, joueur, match, sigma):
    noeuds = [(0.0, 1.0)] if sigma == 0 else list(zip(NOEUDS * sigma, POIDS))

    @lru_cache(None)
    def pp(x):
        return P.p_point(x, 0, cfg)

    @lru_cache(None)
    def rm(x, bo):
        pa, pb = pp(x), pp(-x)
        return P.resume_match(pa, pb, 3 if bo == 5 else 2)

    @lru_cache(None)
    def m12(x):
        return meme_vainqueur_12(pp(x), pp(-x), 2)

    acc = defaultdict(lambda: [0.0, 0.0])
    sc = defaultdict(float)
    for (bo, t, d), n in joueur.items():
        for e, w in noeuds:
            x = round(2 * (d + e)) / 2  # pas de 0,5 point de talent (cache)
            acc[("hold", bo, t)][0] += n * w * P.p_jeu(pp(x))
            acc[("pts", bo, t)][0] += n * w * pp(x)
        acc[("hold", bo, t)][1] += n
        acc[("pts", bo, t)][1] += n
    for (bo, e_pl, d), n in match.items():
        for e, w in noeuds:
            x = round(2 * (d + e)) / 2  # pas de 0,5 point de talent (cache)
            v, ns, tb, se = rm(x, bo)
            acc[("vict", bo, e_pl)][0] += n * w * v
            for k, val in ns.items():
                sc[(bo, f"matchs_en_{k}_sets")] += n * w * val
            sc[(bo, "tb")] += n * w * tb
            sc[(bo, "sets")] += n * w * se
            if bo == 3:
                acc[("meme12", 3, e_pl)][0] += n * w * m12(x)
        acc[("vict", bo, e_pl)][1] += n
        sc[(bo, "_n")] += n
        if bo == 3:
            acc[("meme12", 3, e_pl)][1] += n
    m = {k: v[0] / v[1] for k, v in acc.items()}
    for bo in (3, 5):
        for k in list(sc):
            if k[0] == bo and k[1].startswith("matchs_en"):
                m[("score", bo, k[1])] = sc[k] / sc[(bo, "_n")]
        m[("score", bo, "sets_au_tiebreak")] = sc[(bo, "tb")] / sc[(bo, "sets")]
    return m


def ajuster(cibles, joueur, match, sigma, depart):
    cfg = dict(depart)
    x0 = A.chi2(mesurer(cfg, joueur, match, sigma), cibles)
    pas = {"centre": 0.004, "pente_par_point_de_talent": 0.0004, "demi_amplitude": 0.01}
    for _ in range(5):
        for cle, dp in pas.items():
            for sgn in (-1, 1):
                while True:
                    essai = dict(cfg)
                    essai[cle] = max(1e-4, cfg[cle] + sgn * dp)
                    x = A.chi2(mesurer(essai, joueur, match, sigma), cibles)
                    if x < x0:
                        x0, cfg = x, essai
                    else:
                        break
        pas = {k: v / 2 for k, v in pas.items()}
    return x0, cfg


if __name__ == "__main__":
    obj = sys.argv[1] if len(sys.argv) > 1 else A.OBJ
    cibles = A.charger_cibles()
    cibles.update(cibles_dependance(obj))
    joueur, match = A.construire_population(tirages_par_joueur=40)
    depart = {"centre": 0.640, "pente_par_point_de_talent": 0.0049, "demi_amplitude": 0.048}
    SIGMAS = [int(s) for s in sys.argv[2].split(",")] if len(sys.argv) > 2 else [0, 4, 8, 12, 16, 20]
    lignes = []
    controles = [("score", 3, "matchs_en_2_sets"), ("score", 5, "matchs_en_3_sets"), ("score", 5, "matchs_en_4_sets"),
                 ("score", 5, "matchs_en_5_sets"), ("score", 3, "sets_au_tiebreak"), ("score", 5, "sets_au_tiebreak"),
                 ("meme12", 3, "0-5"), ("meme12", 3, "21-40"), ("meme12", 3, "161+"),
                 ("vict", 3, "81-160"), ("vict", 3, "161+"), ("vict", 5, "81-160"), ("vict", 5, "161+")]
    print("ATP : " + "  ".join(f"{k[0]}Bo{k[1]}:{k[2]}={100*cibles[k][0]:.1f}" for k in controles))
    for sigma in SIGMAS:
        x, cfg = ajuster(cibles, joueur, match, sigma, depart)
        depart = cfg
        m = mesurer(cfg, joueur, match, sigma)
        ajust = A.chi2(m, cibles)
        dep = A.chi2(m, cibles, familles=("score", "meme12"))
        # écart-type de la dispersion exprimé en points au service (pente au centre)
        sig_pts = 100 * sigma * cfg["pente_par_point_de_talent"]
        print(f"\nsigma={sigma:>2} talent (≈ {sig_pts:.1f} pt de % au service) chi2 ajusté={ajust:.0f} chi2 contrôles={dep:.0f} "
              f"cfg={json.dumps({k: round(v, 4) for k, v in cfg.items()})}")
        print("   " + "  ".join(f"{k[0]}Bo{k[1]}:{k[2]}={100*m[k]:.1f}" for k in controles))
        ligne = {"sigma_talent": sigma, "sigma_pts_service": round(sig_pts, 2), "chi2_ajuste": round(ajust),
                 "chi2_controles": round(dep), **{k: round(v, 5) for k, v in cfg.items()}}
        ligne.update({f"{k[0]}_bo{k[1]}_{k[2]}": round(100 * m[k], 1) for k in controles})
        lignes.append(ligne)
    with open("/home/claude/chelem_sim/dispersion_resultats.csv", "w", newline="") as f:
        w = csv.DictWriter(f, fieldnames=list(lignes[0]))
        w.writeheader()
        w.writerows(lignes)
