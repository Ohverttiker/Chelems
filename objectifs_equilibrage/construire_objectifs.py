"""Construit les objectifs d'équilibrage figés à partir du dépôt tennis_atp (Jeff Sackmann).

Périmètre : simple messieurs, circuit ATP principal, 2015-2025, surface DUR, Coupe Davis exclue,
matchs incomplets (RET, W/O, DEF) exclus. Rang = rang ATP au moment du match.
Point de vue « joueur » : une ligne par joueur classé dans le top 150 (un match entre deux top 150 compte deux fois).
Usage : python construire_objectifs.py <dossier tennis_atp> <dossier de sortie>
"""
import csv
import os
import re
import sys
from collections import defaultdict

SRC, OUT = sys.argv[1], sys.argv[2]
TOP = 150
TRANCHES = [(1, 1), (2, 5), (6, 10), (11, 20), (21, 50), (51, 100), (101, 150)]
ECARTS = [(0, 5), (6, 10), (11, 20), (21, 40), (41, 80), (81, 160), (161, 99999)]


def dans(x, iv):
    for a, b in iv:
        if a <= x <= b:
            return (a, b)


def lab(t):
    return f"{t[0]}+" if t[1] >= 9999 else f"{t[0]}-{t[1]}"


def i(x):
    try:
        return int(float(x))
    except (TypeError, ValueError):
        return None


par_rang = defaultdict(lambda: defaultdict(int))      # (bo, tranche) -> compteurs
fav = defaultdict(lambda: defaultdict(int))           # (bo, écart) -> compteurs
sets = defaultdict(lambda: defaultdict(int))          # bo -> nb sets -> nb matchs
tb = defaultdict(lambda: [0, 0])                      # bo -> [sets au TB, sets]
glob = defaultdict(int)
controle = defaultdict(int)

for an in range(2015, 2026):
    with open(os.path.join(SRC, f"atp_matches_{an}.csv"), newline="", encoding="utf-8") as f:
        for m in csv.DictReader(f):
            controle["bruts"] += 1
            if m["tourney_level"] == "D":
                controle["davis"] += 1
                continue
            if m["surface"] != "Hard":
                controle["non_dur"] += 1
                continue
            s = m["score"]
            if re.search(r"RET|W/O|DEF|Def|unfinished", s):
                controle["incomplets"] += 1
                continue
            bo = i(m["best_of"])
            rw, rl = i(m["winner_rank"]), i(m["loser_rank"])
            rw_, rl_ = rw or 99999, rl or 99999
            if min(rw_, rl_) > TOP:
                continue
            controle["matchs_retenus"] += 1
            # sets et tie-breaks (au moins un joueur top 150)
            manches = re.findall(r"(\d+)-(\d+)(?:\(\d+\))?", s)
            if manches:
                sets[bo][len(manches)] += 1
                for a, b in manches:
                    tb[bo][1] += 1
                    if {int(a), int(b)} == {7, 6}:
                        tb[bo][0] += 1
            stats = {}
            for c in ("w", "l"):
                v = {k: i(m[f"{c}_{k}"]) for k in ("svpt", "1stIn", "1stWon", "2ndWon", "SvGms", "bpSaved", "bpFaced", "ace", "df")}
                stats[c] = v if None not in v.values() and v["SvGms"] > 0 and v["svpt"] > 0 else None
            # point de vue joueur : Hold% et points au service par tranche de rang
            for c, r, ro, gagne in (("w", rw_, rl_, 1), ("l", rl_, rw_, 0)):
                if r > TOP:
                    continue
                if stats[c]:
                    v = stats[c]
                    k = par_rang[(bo, dans(r, TRANCHES))]
                    k["jeux"] += v["SvGms"]
                    k["tenus"] += v["SvGms"] - (v["bpFaced"] - v["bpSaved"])
                    k["pts"] += v["svpt"]
                    k["pts_gagnes"] += v["1stWon"] + v["2ndWon"]
            # niveau match : mieux classé dans le top 150, adversaire de rang quelconque (non classé = 99999)
            if rw_ != rl_:
                cf, co = ("w", "l") if rw_ < rl_ else ("l", "w")
                k = fav[(bo, dans(abs(rw_ - rl_), ECARTS))]
                k["n"] += 1
                k["fav_gagne"] += 1 if cf == "w" else 0
                if stats["w"] and stats["l"]:
                    for cote, c in (("fav", cf), ("out", co)):
                        v = stats[c]
                        k[f"jeux_{cote}"] += v["SvGms"]
                        k[f"tenus_{cote}"] += v["SvGms"] - (v["bpFaced"] - v["bpSaved"])
            for c in ("w", "l"):
                r = rw_ if c == "w" else rl_
                if stats[c] and r <= TOP:
                    v = stats[c]
                    for k_ in v:
                        glob[k_] += v[k_]

os.makedirs(OUT, exist_ok=True)
SOURCE = "Jeff Sackmann, tennis_atp (atp_matches_2015-2025.csv), extrait 2026-11-17"


def pc(a, b):
    return round(100 * a / b, 1) if b else ""


with open(os.path.join(OUT, "hold_et_service_par_rang.csv"), "w", newline="") as f:
    w = csv.writer(f)
    w.writerow(["best_of", "tranche_rang", "hold_pct", "points_gagnes_service_pct", "jeux_service", "points_service"])
    for bo in (3, 5):
        for t in TRANCHES:
            k = par_rang[(bo, t)]
            w.writerow([bo, lab(t), pc(k["tenus"], k["jeux"]), pc(k["pts_gagnes"], k["pts"]), k["jeux"], k["pts"]])

with open(os.path.join(OUT, "resultats_par_ecart_de_rang.csv"), "w", newline="") as f:
    w = csv.writer(f)
    w.writerow(["best_of", "ecart_places", "victoire_mieux_classe_pct", "matchs", "hold_mieux_classe_pct",
                "hold_moins_bien_classe_pct", "jeux_mieux_classe", "jeux_moins_bien_classe"])
    for bo in (3, 5):
        for e in ECARTS:
            k = fav[(bo, e)]
            w.writerow([bo, lab(e), pc(k["fav_gagne"], k["n"]), k["n"], pc(k["tenus_fav"], k["jeux_fav"]),
                        pc(k["tenus_out"], k["jeux_out"]), k["jeux_fav"], k["jeux_out"]])

with open(os.path.join(OUT, "scores_sets_et_tiebreaks.csv"), "w", newline="") as f:
    w = csv.writer(f)
    w.writerow(["best_of", "mesure", "valeur_pct", "effectif"])
    for bo in (3, 5):
        n = sum(sets[bo].values())
        for ns in sorted(sets[bo]):
            if ns >= 2 and (bo == 3 and ns <= 3 or bo == 5 and ns >= 3):
                w.writerow([bo, f"matchs_en_{ns}_sets", pc(sets[bo][ns], n), n])
        w.writerow([bo, "sets_au_tiebreak", pc(*tb[bo]), tb[bo][1]])

g = glob
with open(os.path.join(OUT, "service_moyen_top150.csv"), "w", newline="") as f:
    w = csv.writer(f)
    w.writerow(["mesure", "valeur_pct", "definition"])
    w.writerow(["hold", pc(g["SvGms"] - (g["bpFaced"] - g["bpSaved"]), g["SvGms"]), "1 - (bpFaced - bpSaved) / SvGms"])
    w.writerow(["points_gagnes_service", pc(g["1stWon"] + g["2ndWon"], g["svpt"]), "(1stWon + 2ndWon) / svpt"])
    w.writerow(["premieres_balles", pc(g["1stIn"], g["svpt"]), "1stIn / svpt"])
    w.writerow(["points_gagnes_1re", pc(g["1stWon"], g["1stIn"]), "1stWon / 1stIn"])
    w.writerow(["points_gagnes_2e", pc(g["2ndWon"], g["svpt"] - g["1stIn"]), "2ndWon / (svpt - 1stIn), doubles fautes comptées perdues"])
    w.writerow(["aces_par_point_service", pc(g["ace"], g["svpt"]), "ace / svpt"])
    w.writerow(["doubles_fautes_par_point_service", pc(g["df"], g["svpt"]), "df / svpt"])
    w.writerow(["balles_de_break_sauvees", pc(g["bpSaved"], g["bpFaced"]), "bpSaved / bpFaced"])

with open(os.path.join(OUT, "controle_extraction.csv"), "w", newline="") as f:
    w = csv.writer(f)
    w.writerow(["compteur", "valeur"])
    for k, v in controle.items():
        w.writerow([k, v])
    w.writerow(["source", SOURCE])
print(dict(controle))
