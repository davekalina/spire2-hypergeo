using MegaCrit.Sts2.Core.Models;

namespace Hypergeo.HypergeoCode;

/// <summary>
/// One hand the player could plausibly draw, dealt from the same two stages the odds
/// are worked out over: the draw pile first, then the reshuffle once it runs dry.
///
/// The cards are sampled at random rather than taken from the top of the draw pile,
/// even though the real order is sitting right there in combat state. Dealing the true
/// top of the pile would not be a simulation — it would be the answer, and this mod
/// reports odds rather than removing the need for them. A random sample is what the
/// percentages elsewhere on the screen actually describe, so the two agree: deal often
/// enough and the hands turn up in the proportions the odds predict.
/// </summary>
internal sealed record SimulatedHand(
    IReadOnlyList<CardModel> Cards, int FromDrawPile, int FromReshuffle)
{
    /// <summary>
    /// Deal <paramref name="cardsDrawn" /> cards, taking the draw pile first and
    /// reaching into the reshuffle only for what is left over — which is how the game
    /// itself would fill a hand the draw pile cannot cover.
    /// </summary>
    public static SimulatedHand Deal(DrawPools pools, int cardsDrawn)
    {
        var fromDraw = Sample(pools.Draw, cardsDrawn);
        var fromReshuffle = Sample(pools.Reshuffle, cardsDrawn - fromDraw.Count);
        return new SimulatedHand(
            [.. fromDraw, .. fromReshuffle], fromDraw.Count, fromReshuffle.Count);
    }

    /// <summary>
    /// Take <paramref name="count" /> cards from a pile without replacement, by
    /// shuffling only as far as needed. Fewer come back when the pile is too small.
    /// </summary>
    private static List<CardModel> Sample(IReadOnlyList<CardModel> pile, int count)
    {
        var taken = Math.Clamp(count, 0, pile.Count);
        if (taken == 0)
            return [];
        var pool = pile.ToArray();
        for (var index = 0; index < taken; index++)
        {
            var pick = Random.Shared.Next(index, pool.Length);
            (pool[index], pool[pick]) = (pool[pick], pool[index]);
        }
        return [.. pool.Take(taken)];
    }
}
