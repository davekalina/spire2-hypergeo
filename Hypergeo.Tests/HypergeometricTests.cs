using Hypergeo.HypergeoCode;
using Xunit;

namespace Hypergeo.Tests;

public sealed class HypergeometricTests
{
    [Fact]
    public void OneSpecificCardInTenWithFiveDraws_IsFiftyPercent() =>
        Assert.Equal(0.5, Hypergeometric.AtLeastOne(10, 1, 5), 12);

    [Fact]
    public void AnyOfThreeIdenticalDefendsInTenWithFiveDraws_UsesAllCopies() =>
        Assert.Equal(1 - (7d / 10) * (6d / 9) * (5d / 8) * (4d / 7) * (3d / 6),
            Hypergeometric.AtLeastOne(10, 3, 5), 12);

    [Fact]
    public void AnyOfFourBlockCardsInTenWithFiveDraws_IsComplementOfNoHits() =>
        Assert.Equal(1 - (6d / 10) * (5d / 9) * (4d / 8) * (3d / 7) * (2d / 6),
            Hypergeometric.AtLeastOne(10, 4, 5), 12);

    [Theory]
    [InlineData(10, 0, 5, 0)]
    [InlineData(10, 4, 0, 0)]
    [InlineData(5, 1, 5, 1)]
    [InlineData(5, 5, 1, 1)]
    public void Boundaries(int population, int successes, int draws, double expected) =>
        Assert.Equal(expected, Hypergeometric.AtLeastOne(population, successes, draws), 12);

    [Fact]
    public void AcrossPiles_DrawIsConsumedBeforeDiscard()
    {
        Assert.Equal(0.5,
            DrawOddsCalculator.AtLeastOneAcrossPiles(10, 1, 10, 10, 5), 12);
        Assert.Equal(1,
            DrawOddsCalculator.AtLeastOneAcrossPiles(10, 0, 10, 10, 11), 12);
    }

    [Fact]
    public void AcrossPiles_MixedSelectionsUseComplementOfBothStages()
    {
        var expected = 1 -
            (1 - Hypergeometric.AtLeastOne(4, 1, 4)) *
            (1 - Hypergeometric.AtLeastOne(6, 2, 2));
        Assert.Equal(expected,
            DrawOddsCalculator.AtLeastOneAcrossPiles(4, 1, 6, 2, 6), 12);
    }

    [Fact]
    public void AtLeastN_ConvolvesHitsAcrossBothPileStages()
    {
        var expected =
            Hypergeometric.Exactly(4, 2, 4, 2) *
            Hypergeometric.Exactly(6, 3, 2, 1) +
            Hypergeometric.Exactly(4, 2, 4, 2) *
            Hypergeometric.Exactly(6, 3, 2, 2);
        Assert.Equal(expected,
            DrawOddsCalculator.AtLeastAcrossPiles(4, 2, 6, 3, 6, 3), 12);
    }

    [Fact]
    public void Exactly_ProducesKnownHypergeometricProbability() =>
        Assert.Equal(0.5, Hypergeometric.Exactly(10, 1, 5, 1), 12);

    [Fact]
    public void ReshufflePool_HandCardsDiluteTheSecondStage()
    {
        // Two cards in the draw pile and four in the discard. Drawing five empties
        // the draw pile and takes three more after the reshuffle. Three cards in
        // hand join that reshuffle at end of turn, so they thin the same pool.
        var withoutHand = DrawOddsCalculator.AtLeastOneAcrossPiles(2, 0, 4, 1, 5);
        var withHand = DrawOddsCalculator.AtLeastOneAcrossPiles(2, 0, 7, 1, 5);
        Assert.Equal(Hypergeometric.AtLeastOne(4, 1, 3), withoutHand, 12);
        Assert.Equal(Hypergeometric.AtLeastOne(7, 1, 3), withHand, 12);
        Assert.True(withHand < withoutHand);
    }

    [Fact]
    public void ReshufflePool_ACardOnlyInHandIsStillReachable()
    {
        // The one selected card sits in hand, so it becomes a success in the
        // reshuffle pool rather than being unreachable.
        Assert.Equal(Hypergeometric.AtLeastOne(5, 1, 2),
            DrawOddsCalculator.AtLeastOneAcrossPiles(3, 0, 5, 1, 5), 12);
    }

    [Fact]
    public void ReshufflePool_RetainedCardsAreOutsideEveryStage()
    {
        // Retain keeps a card in hand through the turn, so DrawPools leaves it out
        // of the reshuffle pool entirely. No draw can ever reach it.
        Assert.Equal(0, DrawOddsCalculator.AtLeastOneAcrossPiles(3, 0, 0, 0, 9), 12);
        Assert.Equal(0, DrawOddsCalculator.AtLeastAcrossPiles(3, 0, 0, 0, 9, 1), 12);
    }
}
