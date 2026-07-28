using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Models;

namespace Hypergeo.HypergeoCode;

/// <summary>
/// The card populations the next hand is drawn from, resolved from live combat state.
///
/// The draw pile is consumed first. A draw that empties it reshuffles the discard
/// pile and continues from there. Cards in hand reach the discard pile when the turn
/// ends, so they are shuffled back in alongside it — unless a retain effect keeps
/// them in hand, in which case they cannot be drawn at all.
///
/// Every screen resolves its odds through this type so the piles, the natural draw
/// count, and the reshuffle model cannot silently diverge between them.
/// </summary>
internal sealed class DrawPools
{
    private readonly bool _handIsFlushed;

    private DrawPools(
        IReadOnlyList<CardModel> draw,
        IReadOnlyList<CardModel> discard,
        IReadOnlyList<CardModel> hand,
        bool handIsFlushed,
        NextHandDraw nextDraw)
    {
        _handIsFlushed = handIsFlushed;
        Draw = draw;
        Discard = discard;
        Hand = hand;
        NaturalDrawCount = nextDraw.Count;
        DrawModifiers = nextDraw.Modifiers;

        // Two different reasons a card in hand cannot be drawn, kept apart because they
        // are worth telling apart: an effect is holding it, or the player is asking
        // about this turn and so nothing in hand is going anywhere.
        Retained = hand.Where(IsRetained).ToList();
        var rest = hand.Where(card => !IsRetained(card)).ToList();
        if (AllCardsSession.IncludeHandInReshuffle)
        {
            Reshuffle = discard.Concat(rest).ToList();
            HandOutsideReshuffle = [];
        }
        else
        {
            Reshuffle = discard;
            HandOutsideReshuffle = rest;
        }
    }

    /// <summary>Cards in the draw pile. Drawn before anything else.</summary>
    public IReadOnlyList<CardModel> Draw { get; }

    /// <summary>Cards in the discard pile.</summary>
    public IReadOnlyList<CardModel> Discard { get; }

    /// <summary>Cards in hand, including the ones retain will keep there.</summary>
    public IReadOnlyList<CardModel> Hand { get; }

    /// <summary>Everything the reshuffle returns to the draw pile.</summary>
    public IReadOnlyList<CardModel> Reshuffle { get; }

    /// <summary>
    /// Hand cards an effect keeps out of the reshuffle. They only rejoin the deck once
    /// they are played and leave the hand, so no upcoming draw can reach them.
    /// </summary>
    public IReadOnlyList<CardModel> Retained { get; }

    /// <summary>
    /// Hand cards left out of the reshuffle because the question is about drawing during
    /// this turn, when the hand is staying where it is. Empty otherwise.
    /// </summary>
    public IReadOnlyList<CardModel> HandOutsideReshuffle { get; }

    /// <summary>Everything in hand that no upcoming draw can reach, for either reason.</summary>
    public int HeldInHandCount => Retained.Count + HandOutsideReshuffle.Count;

    /// <summary>Next-hand draw after modifiers, retain, and hand capacity.</summary>
    public int NaturalDrawCount { get; }

    /// <summary>Names of the effects that moved the draw count off its base of 5.</summary>
    public IReadOnlyList<string> DrawModifiers { get; }

    /// <summary>Total cards the next hand could reach, across both stages.</summary>
    public int ReachableCount => Draw.Count + Reshuffle.Count;

    /// <summary>A retained card stays in hand through the turn and is never drawn.</summary>
    public bool IsRetained(CardModel card) =>
        !_handIsFlushed || card.ShouldRetainThisTurn;

    /// <summary>Chance the next hand contains at least one selected card.</summary>
    public double ChanceOfAny(Func<CardModel, bool> isSelected, int cardsDrawn) =>
        DrawOddsCalculator.AtLeastOneAcrossPiles(
            Draw.Count, Draw.Count(isSelected),
            Reshuffle.Count, Reshuffle.Count(isSelected),
            cardsDrawn);

    /// <summary>
    /// Chance the next hand contains none of the selected cards.
    ///
    /// Not the same question as "at least zero", which is always true. This is the
    /// complement of drawing any of them, and is worth asking of a card you would
    /// rather not see.
    /// </summary>
    public double ChanceOfNone(Func<CardModel, bool> isSelected, int cardsDrawn) =>
        1 - ChanceOfAny(isSelected, cardsDrawn);

    /// <summary>Chance the next hand contains at least <paramref name="requiredHits"/> selected cards.</summary>
    public double ChanceOfAtLeast(
        Func<CardModel, bool> isSelected, int cardsDrawn, int requiredHits) =>
        DrawOddsCalculator.AtLeastAcrossPiles(
            Draw.Count, Draw.Count(isSelected),
            Reshuffle.Count, Reshuffle.Count(isSelected),
            cardsDrawn, requiredHits);

    public static DrawPools Resolve(Player player)
    {
        var combatState = player.PlayerCombatState ??
            throw new InvalidOperationException("Draw pools require an active combat state.");
        var draw = combatState.DrawPile.Cards.ToList();
        var discard = combatState.DiscardPile.Cards.ToList();
        var hand = combatState.Hand.Cards.ToList();

        var state = CombatManager.Instance.DebugOnlyGetState();
        if (state == null)
            return new DrawPools(
                draw, discard, hand, handIsFlushed: true, new NextHandDraw(0, []));

        // ShouldFlush decides what happens at the end of the turn in progress, so it is
        // asked about the turn the player is on right now. It is the game's answer
        // alone — whether the player wants the hand counted is a separate question.
        var handIsFlushed = Hook.ShouldFlush(state, player);

        // Cards that keep their place in hand through the draw, and so take up the room
        // the next hand would have filled. Asking about drawing during this turn means
        // the whole hand stays, not just what an effect is holding.
        var heldInHand = AllCardsSession.IncludeHandInReshuffle
            ? handIsFlushed
                ? hand.Count(card => card.ShouldRetainThisTurn)
                : hand.Count
            : hand.Count;
        var nextDraw = NextHandDraw.Resolve(state, player, heldInHand);
        return new DrawPools(draw, discard, hand, handIsFlushed, nextDraw);
    }

    public static DrawPools? TryResolveForLocalPlayer()
    {
        var players = CombatManager.Instance.DebugOnlyGetState()?.Players;
        var player = players == null ? null : LocalPlayerResolver.Resolve(players);
        return player?.PlayerCombatState == null ? null : Resolve(player);
    }
}
