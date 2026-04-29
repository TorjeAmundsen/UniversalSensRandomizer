using System;
using UniversalSensRandomizer.Util;

namespace UniversalSensRandomizer.Services;

public sealed class RandomizerEngine
{
    private readonly IRawAccelClient client;
    private readonly BaselineSnapshot baseline;
    private readonly LiveOutputWriter liveOutput;

    public event Action<double, string>? MultiplierChanged;

    public double CurrentMultiplier { get; private set; } = 1.0;
    public double BaseCm360 { get; set; } = 30.0;

    public RandomizerEngine(IRawAccelClient client, BaselineSnapshot baseline, LiveOutputWriter liveOutput)
    {
        this.client = client;
        this.baseline = baseline;
        this.liveOutput = liveOutput;
    }

    public void Randomize(double min, double max)
    {
        if (max < min)
        {
            (min, max) = (max, min);
        }
        double sample = Random.Shared.NextDouble() * (max - min) + min;
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
