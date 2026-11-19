using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Chelems.Donnees;

/// <summary>
/// Construit les objectifs d'équilibrage figés pour une surface (port C# de objectifs_equilibrage/construire_objectifs.py,
/// complété des fichiers dependance_sets_bo3, dispersion_service_par_match et adversaires).
/// Périmètre identique au dur : simple messieurs, circuit principal, 2015-2025, Coupe Davis et matchs incomplets exclus,
/// au moins un joueur du top 150 (rang du jour du match, non classé = 99 999).
/// Usage : dotnet run --project outils/Chelems.Donnees -- objectifs Hard|Clay|Grass [dossier de sortie]
/// </summary>
internal static class Objectifs
{
    private static readonly CultureInfo Ci = CultureInfo.InvariantCulture;
    private const int Top = 150;
    private static readonly (int A, int B)[] Tranches = [(1, 1), (2, 5), (6, 10), (11, 20), (21, 50), (51, 100), (101, 150)];
    private static readonly (int A, int B)[] Ecarts = [(0, 5), (6, 10), (11, 20), (21, 40), (41, 80), (81, 160), (161, 99999)];
    private static readonly (int A, int B)[] EcartsDisp = [(0, 5), (6, 20), (21, 80), (81, 99999)];
    private static readonly Regex Incomplet = new("RET|W/O|DEF|Def|unfinished", RegexOptions.Compiled);
    private static readonly Regex Manche = new(@"(\d+)-(\d+)(?:\(\d+\))?", RegexOptions.Compiled);

    private sealed record Stats(int Svpt, int In1, int Won1, int Won2, int SvGms, int BpSaved, int BpFaced, int Ace, int Df)
    {
        public int Tenus => SvGms - (BpFaced - BpSaved);
        public double PctPoints => (double)(Won1 + Won2) / Svpt;
    }

    public static void Executer(string source, string surface, string sortie)
    {
        Directory.CreateDirectory(sortie);
        var parRang = new Dictionary<(int, (int, int)), long[]>();      // jeux, tenus, pts, pts gagnés
        var retour = new Dictionary<(int, (int, int)), long[]>();       // jeux de retour, breaks, points de retour, points de retour gagnés
        var fav = new Dictionary<(int, (int, int)), long[]>();          // n, fav gagne, jeux fav, tenus fav, jeux out, tenus out
        var sets = new Dictionary<int, Dictionary<int, int>>();
        var tb = new Dictionary<int, long[]>();
        var glob = new long[9];
        var dep = new Dictionary<(int, int), long[]>();                 // n, 2 sets, même vainqueur 1-2
        var disp = new Dictionary<(int, (int, int)), List<(double A, double V)>>();
        var adv = new StringBuilder("rang_joueur,rang_adversaire,best_of,gagne,jeux_tenus,jeux_service,niveau_tournoi\n");
        var controle = new Dictionary<string, int> { ["bruts"] = 0, ["davis"] = 0, ["autre_surface"] = 0, ["incomplets"] = 0, ["matchs_retenus"] = 0 };

        for (int an = 2015; an <= 2025; an++)
        {
            using var r = new StreamReader(Path.Combine(source, $"atp_matches_{an}.csv"), Encoding.UTF8);
            var entete = Sackmann.Decouper(r.ReadLine() ?? "");
            int I(string c) => entete.IndexOf(c);
            string? ligne;
            while ((ligne = r.ReadLine()) is not null)
            {
                var f = Sackmann.Decouper(ligne);
                if (f.Count < entete.Count) continue;
                controle["bruts"]++;
                if (f[I("tourney_level")] == "D") { controle["davis"]++; continue; }
                if (f[I("surface")] != surface) { controle["autre_surface"]++; continue; }
                string score = f[I("score")];
                if (Incomplet.IsMatch(score)) { controle["incomplets"]++; continue; }
                int bo = Ent(f[I("best_of")]) ?? 0;
                int rw = Ent(f[I("winner_rank")]) ?? 99999, rl = Ent(f[I("loser_rank")]) ?? 99999;
                if (Math.Min(rw, rl) > Top) continue;
                controle["matchs_retenus"]++;

                var manches = Manche.Matches(score).Select(m => (A: int.Parse(m.Groups[1].Value, Ci), B: int.Parse(m.Groups[2].Value, Ci))).ToList();
                if (manches.Count > 0)
                {
                    if (!sets.TryGetValue(bo, out var d)) sets[bo] = d = new();
                    d[manches.Count] = d.GetValueOrDefault(manches.Count) + 1;
                    if (!tb.TryGetValue(bo, out var t)) tb[bo] = t = new long[2];
                    foreach (var (a, b) in manches)
                    {
                        t[1]++;
                        if ((a == 7 && b == 6) || (a == 6 && b == 7)) t[0]++;
                    }
                }
                Stats? Lire(string c)
                {
                    int?[] v = [Ent(f[I($"{c}_svpt")]), Ent(f[I($"{c}_1stIn")]), Ent(f[I($"{c}_1stWon")]), Ent(f[I($"{c}_2ndWon")]),
                                Ent(f[I($"{c}_SvGms")]), Ent(f[I($"{c}_bpSaved")]), Ent(f[I($"{c}_bpFaced")]), Ent(f[I($"{c}_ace")]), Ent(f[I($"{c}_df")])];
                    if (v.Any(x => x is null) || v[4] <= 0 || v[0] <= 0) return null;
                    return new Stats(v[0]!.Value, v[1]!.Value, v[2]!.Value, v[3]!.Value, v[4]!.Value, v[5]!.Value, v[6]!.Value, v[7]!.Value, v[8]!.Value);
                }
                var sw = Lire("w"); var sl = Lire("l");

                foreach (var (c, rr, ro, gagne) in new[] { ("w", rw, rl, 1), ("l", rl, rw, 0) })
                {
                    if (rr > Top) continue;
                    var s = c == "w" ? sw : sl;
                    int roA = ro == 99999 ? 9999 : ro; // convention du fichier d'origine pour un adversaire non classé
                    string niv = f[I("tourney_level")];
                    adv.AppendLine(s is null ? $"{rr},{roA},{bo},{gagne},,,{niv}" : $"{rr},{roA},{bo},{gagne},{s.Tenus},{s.SvGms},{niv}");
                    var so = c == "w" ? sl : sw;
                    if (so is not null)
                    {
                        var kr = Compteur(retour, (bo, Dans(rr, Tranches)), 4);
                        kr[0] += so.SvGms; kr[1] += so.SvGms - so.Tenus; kr[2] += so.Svpt; kr[3] += so.Svpt - so.Won1 - so.Won2;
                    }
                    if (s is null) continue;
                    var k = Compteur(parRang, (bo, Dans(rr, Tranches)), 4);
                    k[0] += s.SvGms; k[1] += s.Tenus; k[2] += s.Svpt; k[3] += s.Won1 + s.Won2;
                    long[] vals = [s.Svpt, s.In1, s.Won1, s.Won2, s.SvGms, s.BpSaved, s.BpFaced, s.Ace, s.Df];
                    for (int i = 0; i < 9; i++) glob[i] += vals[i];
                }

                if (rw != rl)
                {
                    bool favGagne = rw < rl;
                    var e = Dans(Math.Abs(rw - rl), Ecarts);
                    var k = Compteur(fav, (bo, e), 6);
                    k[0]++; if (favGagne) k[1]++;
                    var sf = favGagne ? sw : sl; var so = favGagne ? sl : sw;
                    if (sw is not null && sl is not null)
                    {
                        k[2] += sf!.SvGms; k[3] += sf.Tenus; k[4] += so!.SvGms; k[5] += so.Tenus;
                        var de = Dans(Math.Abs(rw - rl), EcartsDisp);
                        if (!disp.TryGetValue((bo, de), out var l)) disp[(bo, de)] = l = [];
                        double pf = sf.PctPoints, po = so.PctPoints;
                        l.Add((100 * (pf - po), 1e4 * (pf * (1 - pf) / sf.Svpt + po * (1 - po) / so.Svpt)));
                    }
                    if (bo == 3 && manches.Count >= 2)
                    {
                        var kd = Compteur(dep, (3, Array.IndexOf(Ecarts, e)), 3);
                        kd[0]++;
                        if (manches.Count == 2) kd[1]++;
                        if ((manches[0].A > manches[0].B) == (manches[1].A > manches[1].B)) kd[2]++;
                    }
                }
            }
        }

        string Pc(double a, double b) => b == 0 ? "" : Math.Round(100 * a / b, 1, MidpointRounding.ToEven).ToString("0.0", Ci);
        string Lab((int A, int B) t) => t.B >= 9999 ? $"{t.A}+" : $"{t.A}-{t.B}";
        void Ecrire(string nom, StringBuilder sb) => File.WriteAllText(Path.Combine(sortie, nom), sb.ToString());

        var h = new StringBuilder("best_of,tranche_rang,hold_pct,points_gagnes_service_pct,jeux_service,points_service\n");
        foreach (int bo in new[] { 3, 5 })
            foreach (var t in Tranches)
            {
                var k = parRang.GetValueOrDefault((bo, t)) ?? new long[4];
                h.AppendLine($"{bo},{Lab(t)},{Pc(k[1], k[0])},{Pc(k[3], k[2])},{k[0]},{k[2]}");
            }
        Ecrire("hold_et_service_par_rang.csv", h);

        var rt = new StringBuilder("best_of,tranche_rang,jeux_retour_gagnes_pct,points_retour_gagnes_pct,jeux_retour,points_retour\n");
        foreach (int bo in new[] { 3, 5 })
            foreach (var t in Tranches)
            {
                var k = retour.GetValueOrDefault((bo, t)) ?? new long[4];
                rt.AppendLine($"{bo},{Lab(t)},{Pc(k[1], k[0])},{Pc(k[3], k[2])},{k[0]},{k[2]}");
            }
        Ecrire("retour_par_rang.csv", rt);

        var rs = new StringBuilder("best_of,ecart_places,victoire_mieux_classe_pct,matchs,hold_mieux_classe_pct,hold_moins_bien_classe_pct,jeux_mieux_classe,jeux_moins_bien_classe\n");
        foreach (int bo in new[] { 3, 5 })
            foreach (var e in Ecarts)
            {
                var k = fav.GetValueOrDefault((bo, e)) ?? new long[6];
                rs.AppendLine($"{bo},{Lab(e)},{Pc(k[1], k[0])},{k[0]},{Pc(k[3], k[2])},{Pc(k[5], k[4])},{k[2]},{k[4]}");
            }
        Ecrire("resultats_par_ecart_de_rang.csv", rs);

        var sc = new StringBuilder("best_of,mesure,valeur_pct,effectif\n");
        foreach (int bo in new[] { 3, 5 })
        {
            var d = sets.GetValueOrDefault(bo) ?? new();
            int n = d.Values.Sum();
            foreach (int ns in d.Keys.Order())
                if (ns >= 2 && ((bo == 3 && ns <= 3) || (bo == 5 && ns >= 3)))
                    sc.AppendLine($"{bo},matchs_en_{ns}_sets,{Pc(d[ns], n)},{n}");
            var t = tb.GetValueOrDefault(bo) ?? new long[2];
            sc.AppendLine($"{bo},sets_au_tiebreak,{Pc(t[0], t[1])},{t[1]}");
        }
        Ecrire("scores_sets_et_tiebreaks.csv", sc);

        var g = glob;
        var sm = new StringBuilder("mesure,valeur_pct,definition\n");
        sm.AppendLine($"hold,{Pc(g[4] - (g[6] - g[5]), g[4])},1 - (bpFaced - bpSaved) / SvGms");
        sm.AppendLine($"points_gagnes_service,{Pc(g[2] + g[3], g[0])},(1stWon + 2ndWon) / svpt");
        sm.AppendLine($"premieres_balles,{Pc(g[1], g[0])},1stIn / svpt");
        sm.AppendLine($"points_gagnes_1re,{Pc(g[2], g[1])},1stWon / 1stIn");
        sm.AppendLine($"points_gagnes_2e,{Pc(g[3], g[0] - g[1])},\"2ndWon / (svpt - 1stIn), doubles fautes comptées perdues\"");
        sm.AppendLine($"aces_par_point_service,{Pc(g[7], g[0])},ace / svpt");
        sm.AppendLine($"doubles_fautes_par_point_service,{Pc(g[8], g[0])},df / svpt");
        sm.AppendLine($"balles_de_break_sauvees,{Pc(g[5], g[6])},bpSaved / bpFaced");
        Ecrire("service_moyen_top150.csv", sm);

        var dp = new StringBuilder("best_of,ecart_places,matchs,matchs_en_2_sets_pct,meme_vainqueur_sets_1_et_2_pct\n");
        for (int i = 0; i < Ecarts.Length; i++)
        {
            var k = dep.GetValueOrDefault((3, i)) ?? new long[3];
            dp.AppendLine($"3,{Lab(Ecarts[i])},{k[0]},{Pc(k[1], k[0])},{Pc(k[2], k[0])}");
        }
        Ecrire("dependance_sets_bo3.csv", dp);

        var ds = new StringBuilder("best_of,ecart_places,matchs,avantage_moyen_mieux_classe_pts,ecart_type_observe_pts,ecart_type_bruit_binomial_pts,ecart_type_reel_estime_pts\n");
        foreach (int bo in new[] { 3, 5 })
            foreach (var e in EcartsDisp)
            {
                var l = disp.GetValueOrDefault((bo, e)) ?? [];
                if (l.Count < 2) continue;
                double m = l.Average(x => x.A);
                double sd = Math.Sqrt(l.Sum(x => (x.A - m) * (x.A - m)) / (l.Count - 1));
                double sb = Math.Sqrt(l.Average(x => x.V));
                double reel = Math.Sqrt(Math.Max(0, sd * sd - sb * sb));
                ds.AppendLine(string.Create(Ci, $"{bo},{Lab(e)},{l.Count},{m:F1},{sd:F1},{sb:F1},{reel:F1}"));
            }
        Ecrire("dispersion_service_par_match.csv", ds);
        Ecrire($"adversaires_{Nom(surface)}.csv", adv);

        var ce = new StringBuilder("compteur,valeur\n");
        foreach (var (k, v) in controle) ce.AppendLine($"{k},{v}");
        ce.AppendLine($"source,\"Jeff Sackmann, tennis_atp (atp_matches_2015-2025.csv), surface {surface}, construit par outils/Chelems.Donnees le {DateTime.Now:yyyy-MM-dd}\"");
        Ecrire("controle_extraction.csv", ce);
        Console.WriteLine($"{surface} : {controle["matchs_retenus"]} matchs retenus → {sortie}");
    }

    public static string Nom(string surface) => surface switch { "Hard" => "dur", "Clay" => "terre", "Grass" => "gazon", _ => surface.ToLowerInvariant() };

    private static long[] Compteur<TK>(Dictionary<TK, long[]> d, TK k, int n) where TK : notnull
    {
        if (!d.TryGetValue(k, out var v)) d[k] = v = new long[n];
        return v;
    }

    private static (int, int) Dans(int x, (int A, int B)[] iv) => iv.First(t => t.A <= x && x <= t.B);

    private static int? Ent(string s) =>
        double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out double v) ? (int)v : null;
}
