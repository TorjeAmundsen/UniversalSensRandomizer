using System;
using UniversalSensRandomizer.Util;

namespace UniversalSensRandomizer.Services;

public sealed class RandomizerEngine(IRawAccelClient client, BaselineSnapshot baseline, LiveOutputWriter liveOutput)
{
    public event Action<double, string>? MultiplierChanged;

    public double CurrentMultiplier { get; private set; } = 1.0;
    public double BaseCm360 { get; set; } = 30.0;

    public void Randomize(double min, double max)
    {
        if (max < min)
        {
            (min, max) = (max, min);
        }
        // Log-uniform sampling: equal probability per ratio so perceived sensitivity
        // change is uniform (0.5x->1.0x feels the same as 1.0x->2.0x).
        double logMin = Math.Log(min);
        double logMax = Math.Log(max);
        double sample = Math.Exp(Random.Shared.NextDouble() * (logMax - logMin) + logMin);
        double multiplier = Math.Round(sample, 2);
        ApplyMultiplier(multiplier);
    }

    public void ApplyMultiplier(double multiplier)
    {
        if (multiplier <= 0 || !double.IsFinite(multiplier))
        {
            throw new ArgumentOutOfRangeException(nameof(multiplier));
        }

        byte[] buffer = baseline.BuildBuffer(multiplier);
        client.Write(buffer);

        CurrentMultiplier = multiplier;

        double cm360 = BaseCm360 / multiplier;
        string output = InvariantFormat.LiveOutput(multiplier, cm360);
        liveOutput.Write(output);
        MultiplierChanged?.Invoke(multiplier, output);
    }

    public void RestoreOriginal()
    {
        client.Write(baseline.OriginalBuffer);
        CurrentMultiplier = 1.0;
    }
}
