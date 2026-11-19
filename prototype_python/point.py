"""Moteur au point (étape 2 de l'échelle) : jeu, tie-break, set, match, à partir d'une probabilité
de gagner le point au service. Calcul exact (chaînes de Markov) + simulation aléatoire de contrôle.

Règles :
- Jeu : premier à 4 points avec 2 d'écart (égalité/avantage).
- Tie-break à 6-6 : premier à 7 points avec 2 d'écart ; le joueur dont c'est le tour de servir sert le 1er point,
  puis on alterne tous les 2 points ; au set suivant, l'autre joueur sert le premier jeu.
- Match : 2 ou 3 sets gagnants ; le premier serveur est tiré au sort.
- p_point(talent_servant, talent_relanceur) = clamp(centre + pente * écart, centre - demi_amplitude, centre + demi_amplitude).
"""
import json
import os
import random
from functools import lru_cache

_CFG_PATH = os.path.join(os.path.dirname(os.path.abspath(__file__)), "point_config.json")


def charger_config(chemin=_CFG_PATH):
    with open(chemin, encoding="utf-8") as f:
        return json.load(f)["point_service"]


def p_point(talent_servant, talent_relanceur, cfg):
    c, k, h = cfg["centre"], cfg["pente_par_point_de_talent"], cfg["demi_amplitude"]
    return min(c + h, max(c - h, c + k * (talent_servant - talent_relanceur)))


# ---------- calcul exact ----------

def p_jeu(p):
    """Probabilité que le serveur gagne le jeu, points indépendants de probabilité p."""
    q = 1 - p
    avant_egalite = p ** 4 * (1 + 4 * q + 10 * q * q)
    egalite = 20 * p ** 3 * q ** 3
    return avant_egalite + egalite * p * p / (1 - 2 * p * q)


def p_tiebreak(pa, pb):
    """Probabilité que A gagne le tie-break, A servant le premier point."""
    @lru_cache(None)
    def f(a, b):
        if a >= 7 and a - b >= 2:
            return 1.0
        if b >= 7 and b - a >= 2:
            return 0.0
        if a == b and a >= 6:
            # à 6-6 (et au-delà), deux points : un service de chaque joueur
            x, y = pa * (1 - pb), (1 - pa) * pb
            return x / (x + y)
        n = a + b
        a_sert = (n % 4) in (0, 3)
        p = pa if a_sert else 1 - pb
        return p * f(a + 1, b) + (1 - p) * f(a, b + 1)
    return f(0, 0)


def distribution_set(pa, pb):
    """Pour un set où A sert le 1er jeu : (P(A gagne), P(A gagne et l'autre... ), ...).
    Retourne un dict {(gagnant, a_sert_set_suivant, tiebreak): proba}."""
    ga, gb = p_jeu(pa), p_jeu(pb)
    tb_a = p_tiebreak(pa, pb)   # A sert le 1er point du TB
    tb_b = 1 - p_tiebreak(pb, pa)  # B sert le 1er point du TB → P(A gagne)
    res = {}

    def ajoute(cle, v):
        res[cle] = res.get(cle, 0.0) + v

    @lru_cache(None)
    def etats(x, y, a_sert):
        return None

    # exploration en avant avec probabilités d'état
    front = {(0, 0, True): 1.0}
    while front:
        suivant = {}
        for (x, y, a_sert), pr in front.items():
            if x == 6 and y == 6:
                pw = tb_a if a_sert else tb_b
                # au set suivant, le receveur du 1er point du TB sert : donc A sert si B servait le TB
                ajoute(("A", not a_sert, True), pr * pw)
                ajoute(("B", not a_sert, True), pr * (1 - pw))
                continue
            pw = ga if a_sert else 1 - gb
            for nx, ny, w in ((x + 1, y, pw), (x, y + 1, 1 - pw)):
                fin = (nx >= 6 and nx - ny >= 2) or nx == 7
                finb = (ny >= 6 and ny - nx >= 2) or ny == 7
                if fin:
                    ajoute(("A", not a_sert, False), pr * w)
                elif finb:
                    ajoute(("B", not a_sert, False), pr * w)
                else:
                    cle = (nx, ny, not a_sert)
                    suivant[cle] = suivant.get(cle, 0.0) + pr * w
        front = suivant
    return res


def resume_match(pa, pb, sets_gagnants):
    """Mesures exactes d'un match (A et B : probas de point au service).
    Retourne : P(A gagne), distribution du nombre de sets, espérance de sets au TB, espérance de sets."""
    set_a = distribution_set(pa, pb)        # A sert le 1er jeu du set
    set_b_brut = distribution_set(pb, pa)   # B sert le 1er jeu (vu de B)
    set_b = {}
    for (g, b_sert_suiv, tb), v in set_b_brut.items():
        set_b[("A" if g == "B" else "B", not b_sert_suiv, tb)] = v
    victoire, nb_sets = 0.0, {}
    tb_esp, sets_esp = 0.0, 0.0
    front = {(0, 0, True): 0.5, (0, 0, False): 0.5}
    while front:
        suivant = {}
        for (x, y, a_sert), pr in front.items():
            d = set_a if a_sert else set_b
            for (g, a_sert_suiv, tb), v in d.items():
                w = pr * v
                sets_esp += w
                if tb:
                    tb_esp += w
                nx, ny = (x + 1, y) if g == "A" else (x, y + 1)
                if nx == sets_gagnants or ny == sets_gagnants:
                    n = nx + ny
                    nb_sets[n] = nb_sets.get(n, 0.0) + w
                    if nx == sets_gagnants:
                        victoire += w
                else:
                    cle = (nx, ny, a_sert_suiv)
                    suivant[cle] = suivant.get(cle, 0.0) + w
        front = suivant
    return victoire, nb_sets, tb_esp, sets_esp


# ---------- simulation aléatoire (contrôle) ----------

def jouer_jeu(p, rng):
    a = b = 0
    while True:
        if rng.random() < p:
            a += 1
        else:
            b += 1
        if a >= 4 and a - b >= 2:
            return True
        if b >= 4 and b - a >= 2:
            return False


def jouer_tiebreak(pa, pb, rng):
    a = b = 0
    n = 0
    while True:
        a_sert = (n % 4) in (0, 3)
        if rng.random() < (pa if a_sert else 1 - pb):
            a += 1
        else:
            b += 1
        n += 1
        if a >= 7 and a - b >= 2:
            return True
        if b >= 7 and b - a >= 2:
            return False


def jouer_match(pa, pb, sets_gagnants, rng):
    """Retourne (A gagne, [jeux service A, B], [jeux tenus A, B], nb sets, nb TB)."""
    sets = [0, 0]
    a_sert = rng.random() < 0.5
    serv, tenus = [0, 0], [0, 0]
    nb_tb = 0
    while max(sets) < sets_gagnants:
        x = y = 0
        while True:
            if x == 6 and y == 6:
                nb_tb += 1
                gagne = jouer_tiebreak(pa, pb, rng) if a_sert else not jouer_tiebreak(pb, pa, rng)
                if gagne:
                    x += 1
                else:
                    y += 1
                a_sert = not a_sert
                break
            i = 0 if a_sert else 1
            serv[i] += 1
            tient = jouer_jeu(pa if a_sert else pb, rng)
            tenus[i] += tient
            if (tient and a_sert) or (not tient and not a_sert):
                x += 1
            else:
                y += 1
            a_sert = not a_sert
            if (x >= 6 or y >= 6) and abs(x - y) >= 2:
                break
        sets[0 if x > y else 1] += 1
    return sets[0] > sets[1], serv, tenus, sum(sets), nb_tb
