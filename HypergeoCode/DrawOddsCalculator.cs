namespace Hypergeo.HypergeoCode;

public static class DrawOddsCalculator
{
    public static double AtLeastOneAcrossPiles(
        int drawPopulation,
        int drawSuccesses,
        int discardPopulation,
        int discardSuccesses,
        int cardsDrawn)
    {
        if (drawPopulation < 0 || discardPopulation < 0 ||
            drawSuccesses < 0 || drawSuccesses > drawPopulation ||
            discardSuccesses < 0 || discardSuccesses > discardPopulation ||
            cardsDrawn < 0)
            throw new ArgumentOutOfRangeException();

        var drawsFromDrawPile = Math.Min(cardsDrawn, drawPopulation);
        var drawsFromDiscard = Math.Min(
            Math.Max(0, cardsDrawn - drawPopulation),
            discardPopulation);
        var missDraw = 1 - Hypergeometric.AtLeastOne(
            drawPopulation, drawSuccesses, drawsFromDrawPile);
        var missDiscard = 1 - Hypergeometric.AtLeastOne(
            discardPopulation, discardSuccesses, drawsFromDiscard);
        return 1 - missDraw * missDiscard;
    }

    public static double AtLeastAcrossPiles(
        int drawPopulation,
        int drawSuccesses,
        int discardPopulation,
        int discardSuccesses,
        int cardsDrawn,
        int requiredHits)
    {
        if (requiredHits <= 0)
            return 1;
        var drawsFromDrawPile = Math.Min(cardsDrawn, drawPopulation);
        var drawsFromDiscard = Math.Min(
            Math.Max(0, cardsDrawn - drawPopulation),
            discardPopulation);
        double probability = 0;
        for (var drawHits = 0; drawHits <= Math.Min(drawSuccesses, drawsFromDrawPile); drawHits++)
        {
            var drawProbability = Hypergeometric.Exactly(
                drawPopulation, drawSuccesses, drawsFromDrawPile, drawHits);
            for (var discardHits = 0;
                 discardHits <= Math.Min(discardSuccesses, drawsFromDiscard);
                 discardHits++)
            {
                if (drawHits + discardHits < requiredHits)
                    continue;
                probability += drawProbability * Hypergeometric.Exactly(
                    discardPopulation, discardSuccesses, drawsFromDiscard, discardHits);
            }
        }
        return Math.Clamp(probability, 0, 1);
    }
}
