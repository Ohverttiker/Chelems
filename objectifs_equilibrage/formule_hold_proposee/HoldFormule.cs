using System;
using System.IO;
using System.Text.Json;

namespace Chelem.Core
{
    /// <summary>Constantes de calibrage du Hold% (chargées depuis hold_config.json, jamais codées en dur).</summary>
    public sealed record HoldConfig(double Centre, double PenteParPointDeTalent, double DemiAmplitude)
    {
        public static HoldConfig Charger(string chemin)
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(chemin));
            var h = doc.RootElement.GetProperty("hold");
            return new HoldConfig(
                h.GetProperty("centre").GetDouble(),
                h.GetProperty("pente_par_point_de_talent").GetDouble(),
                h.GetProperty("demi_amplitude").GetDouble());
        }
    }

    public static class HoldFormule
    {
        /// <summary>
        /// Probabilité que le serveur tienne son jeu de service (surface : dur).
        /// Linéaire en écart de talent, bornée symétriquement autour du centre :
        /// hold(A) + hold(B) = 2 × centre pour tout écart (structure mesurée sur les données ATP).
        /// Fonction pure : aucun état, aucun aléatoire.
        /// </summary>
        public static double ProbaTenirService(double talentServeur, double talentRelanceur, HoldConfig cfg)
        {
            double p = cfg.Centre + cfg.PenteParPointDeTalent * (talentServeur - talentRelanceur);
            return Math.Clamp(p, cfg.Centre - cfg.DemiAmplitude, cfg.Centre + cfg.DemiAmplitude);
        }
    }
}
