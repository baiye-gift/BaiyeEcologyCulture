using System;

namespace Baiye.EcologyCulture
{
    public enum CultureMode { Idle, Organic, Photosynthetic, Preserve, OxygenBlocked }

    // Transient presentation signals: record only AFTER a paid material commit.
    // These fields are deliberately absent from the serialized stock ledger.
    public sealed class CultureActivity
    {
        public double LastWork { get; private set; } = double.NegativeInfinity;
        public double LastPhoto { get; private set; } = double.NegativeInfinity;
        public int HarvestSequence { get; private set; }
        public void Committed(CultureStage before, CultureDelta delta, double now)
        {
            if (!delta.Worked) return;
            LastWork = now;
            if (delta.PhotoGain > 1e-9) LastPhoto = now;
            if (before == CultureStage.Grow && delta.LiveTaken >= CultureModel.BatchKg - 1e-7)
                HarvestSequence++;
        }
        public bool Running(double now, bool enabled) => enabled && now - LastWork <= .45;
        public CultureMode Mode(CultureState state, double now, bool enabled, bool oxygenFull)
        {
            if (!Running(now, enabled) || state.Stage != CultureStage.Grow) return CultureMode.Idle;
            if (state.Policy == CulturePolicy.Preserve && state.LiveKg >= 16 - 1e-7) return CultureMode.Preserve;
            if (oxygenFull) return CultureMode.OxygenBlocked;
            return now - LastPhoto <= .45 ? CultureMode.Photosynthetic : CultureMode.Organic;
        }
        public static string Loop(CultureState state)
        {
            if (state.Stage == CultureStage.Drain || state.Stage == CultureStage.Clean) return "cleaning_loop";
            if (state.Stage == CultureStage.Recover || state.Stage == CultureStage.SaveSample) return "recovery_loop";
            if (state.Stage == CultureStage.Inoculate) return "inoculating_loop";
            return state.Policy == CulturePolicy.Preserve && state.LiveKg >= 16 - 1e-7 ? "preserving_loop" : "working_loop";
        }
    }
}
