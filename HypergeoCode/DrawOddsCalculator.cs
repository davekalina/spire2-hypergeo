namespace Hypergeo.HypergeoCode;

/// <summary>
/// Two-stage draw odds. The draw pile is consumed first; a draw that empties it
/// continues from the reshuffle pool, which is the discard pile plus every card the
/// end of turn sends there. <see cref="DrawPools" /> builds that pool from live
/// combat state.
/// </summary>
public static class DrawOddsCalculator
{
    public static double AtLeastOneAcrossPiles(
        int drawPopulation,
        int drawSuccesses,
        int reshufflePopulation,
        int reshuffleSuccesses,
        int cardsDrawn)
    {
        if (drawPopulation < 0 || reshufflePopulation < 0 ||
            drawSuccesses < 0 || drawSuccesses > drawPopulation ||
            reshuffleSuccesses < 0 || reshuffleSuccesses > reshufflePopulation ||
            cardsDrawn < 0)
            throw new ArgumentOutOfRangeException();

        var drawsFromDrawPile = Math.Min(cardsDrawn, drawPopulation);
        var drawsFromReshuffle = Math.Min(
            Math.Max(0, cardsDrawn - drawPopulation),
            reshufflePopulation);
        var missDraw = 1 - Hypergeometric.AtLeastOne(
            drawPopulation, drawSuccesses, drawsFromDrawPile);
        var missReshuffle = 1 - Hypergeometric.AtLeastOne(
            reshufflePopulation, reshuffleSuccesses, drawsFromReshuffle);
        return 1 - missDraw * missReshuffle;
    }

    public static double AtLeastAcrossPiles(
        int drawPopulation,
        int drawSuccesses,
        int reshufflePopulation,
        int reshuffleSuccesses,
        int cardsDrawn,
        int requiredHits)
    {
        if (requiredHits <= 0)
            return 1;
        var drawsFromDrawPile = Math.Min(cardsDrawn, drawPopulation);
        var drawsFromReshuffle = Math.Min(
            Math.Max(0, cardsDrawn - drawPopulation),
            reshufflePopulation);
        double probability = 0;
        for (var drawHits = 0; drawHits <= Math.Min(drawSuccesses, drawsFromDrawPile); drawHits++)
        {
            var drawProbability = Hypergeometric.Exactly(
                drawPopulation, drawSuccesses, drawsFromDrawPile, drawHits);
            for (var reshuffleHits = 0;
                 reshuffleHits <= Math.Min(reshuffleSuccesses, drawsFromReshuffle);
                 reshuffleHits++)
            {
                if (drawHits + reshuffleHits < requiredHits)
                    continue;
                probability += drawProbability * Hypergeometric.Exactly(
                    reshufflePopulation, reshuffleSuccesses, drawsFromReshuffle, reshuffleHits);
            }
        }
        return Math.Clamp(probability, 0, 1);
    }
}
