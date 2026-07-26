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

    public static string FormatPercent(double probability) =>
        probability.ToString("P1", System.Globalization.CultureInfo.InvariantCulture);

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
