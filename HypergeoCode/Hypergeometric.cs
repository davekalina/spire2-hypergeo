namespace Hypergeo.HypergeoCode;

public static class Hypergeometric
{
    public static double Exactly(int population, int successes, int draws, int hits)
    {
        if (population < 0 || successes < 0 || successes > population ||
            draws < 0 || hits < 0)
            throw new ArgumentOutOfRangeException();
        draws = Math.Min(draws, population);
        if (hits > successes || hits > draws || draws - hits > population - successes)
            return 0;
        return Math.Exp(
            LogChoose(successes, hits) +
            LogChoose(population - successes, draws - hits) -
            LogChoose(population, draws));
    }

    public static double AtLeastOne(int population, int successes, int draws)
    {
        if (population < 0 || successes < 0 || successes > population || draws < 0)
            throw new ArgumentOutOfRangeException();
        draws = Math.Min(draws, population);
        if (successes == 0 || draws == 0)
            return 0;
        if (draws > population - successes)
            return 1;

        double none = 1;
        for (var i = 0; i < draws; i++)
            none *= (population - successes - i) / (double)(population - i);
        return 1 - none;
    }

    /// <summary>Chance of drawing at least <paramref name="hits" /> successes.</summary>
    public static double AtLeast(int population, int successes, int draws, int hits)
    {
        if (hits <= 0)
            return 1;
        draws = Math.Min(draws, population);
        double probability = 0;
        for (var hit = hits; hit <= Math.Min(successes, draws); hit++)
            probability += Exactly(population, successes, draws, hit);
        return Math.Clamp(probability, 0, 1);
    }

    /// <summary>Chance of drawing no more than <paramref name="hits" /> successes.</summary>
    public static double AtMost(int population, int successes, int draws, int hits)
    {
        draws = Math.Min(draws, population);
        if (hits >= Math.Min(successes, draws))
            return 1;
        double probability = 0;
        for (var hit = 0; hit <= hits; hit++)
            probability += Exactly(population, successes, draws, hit);
        return Math.Clamp(probability, 0, 1);
    }

    /// <summary>
    /// Successes the sample is expected to contain. The hypergeometric mean, n·K/N.
    /// </summary>
    public static double ExpectedHits(int population, int successes, int draws) =>
        population <= 0 ? 0 : Math.Min(draws, population) * (double)successes / population;

    /// <summary>
    /// Two decimal places, and never a rounded-off zero: a chance that exists but is
    /// smaller than the format can show reads as less than the smallest value rather
    /// than as impossible.
    /// </summary>
    public static string FormatPercent(double probability)
    {
        const double smallest = 0.0001;
        var culture = System.Globalization.CultureInfo.InvariantCulture;
        return probability > 0 && probability < smallest
            ? "< " + smallest.ToString("P2", culture)
            : probability.ToString("P2", culture);
    }

    private static double LogChoose(int n, int k)
    {
        if (k < 0 || k > n)
            return double.NegativeInfinity;
        k = Math.Min(k, n - k);
        double value = 0;
        for (var i = 1; i <= k; i++)
            value += Math.Log(n - k + i) - Math.Log(i);
        return value;
    }
}
