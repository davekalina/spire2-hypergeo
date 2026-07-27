using System.Reflection;
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
    private static readonly FieldInfo? TurnNumberField = typeof(PlayerCombatState)
        .GetFields(BindingFlags.Instance | BindingFlags.NonPublic)
        .FirstOrDefault(field =>
            field.FieldType == typeof(int) && field.Name.Contains("TurnNumber"));

    private readonly bool _handIsFlushed;

    private DrawPools(
        IReadOnlyList<CardModel> draw,
        IReadOnlyList<CardModel> discard,
        IReadOnlyList<CardModel> hand,
        bool handIsFlushed,
        int naturalDrawCount)
    {
        _handIsFlushed = handIsFlushed;
        Draw = draw;
        Discard = discard;
        Hand = hand;
        NaturalDrawCount = naturalDrawCount;
        Reshuffle = discard
            .Concat(hand.Where(card => !IsRetained(card)))
            .ToList();
    }

    /// <summary>Cards in the draw pile. Drawn before anything else.</summary>
    public IReadOnlyList<CardModel> Draw { get; }

    /// <summary>Cards in the discard pile.</summary>
    public IReadOnlyList<CardModel> Discard { get; }

    /// <summary>Cards in hand, including the ones retain will keep there.</summary>
    public IReadOnlyList<CardModel> Hand { get; }

    /// <summary>Everything the reshuffle returns to the draw pile.</summary>
    public IReadOnlyList<CardModel> Reshuffle { get; }

    /// <summary>Next-hand draw after modifiers, retain, and hand capacity.</summary>
    public int NaturalDrawCount { get; }

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
            return new DrawPools(draw, discard, hand, handIsFlushed: true, 0);

        // ShouldFlush decides what happens at the end of the turn in progress, so it
        // is asked about the turn the player is on right now.
        var handIsFlushed = Hook.ShouldFlush(state, player);
        var retained = handIsFlushed
            ? hand.Count(card => card.ShouldRetainThisTurn)
            : hand.Count;
        var naturalDrawCount = OnNextTurn(combatState, () =>
        {
            if (!Hook.ShouldDraw(state, player, fromHandDraw: true, out _))
                return 0;
            var modified = Hook.ModifyHandDraw(
                state, player, CombatManager.baseHandDrawCount, out _);
            return Math.Min(
                Math.Max(0, CardPile.MaxCardsInHand - retained),
                Math.Max(0, (int)modified));
        });
        return new DrawPools(draw, discard, hand, handIsFlushed, naturalDrawCount);
    }

    /// <summary>
    /// Evaluate a draw hook the way the game will evaluate it next turn.
    ///
    /// Draw modifiers are turn-sensitive — Ring of the Snake and Bag of Preparation
    /// only add on turn 1, Pocketwatch pays out only from turn 2, Ring of the Drake
    /// only inside a turn window — and the game runs these hooks *after* it has
    /// incremented the turn number. Asking them as-is reports the hand the player
    /// already has, not the one they are about to draw.
    ///
    /// Every ModifyHandDraw implementation is a pure read, so the turn number is
    /// nudged forward across the call and restored in a finally. If the field ever
    /// disappears the prediction falls back to the current turn rather than failing.
    /// </summary>
    private static int OnNextTurn(PlayerCombatState combatState, Func<int> evaluate)
    {
        if (TurnNumberField == null)
        {
            WarnOnceAboutMissingTurnNumber();
            return evaluate();
        }
        var currentTurn = combatState.TurnNumber;
        TurnNumberField.SetValue(combatState, currentTurn + 1);
        try
        {
            return evaluate();
        }
        finally
        {
            TurnNumberField.SetValue(combatState, currentTurn);
        }
    }

    private static bool _warnedAboutTurnNumber;

    private static void WarnOnceAboutMissingTurnNumber()
    {
        if (_warnedAboutTurnNumber)
            return;
        _warnedAboutTurnNumber = true;
        MainFile.Logger.Warn(
            "PlayerCombatState turn number field not found. Draw counts will be " +
            "predicted for the current turn, so first-turn draw bonuses will be " +
            "reported for the next hand as well.");
    }

    public static DrawPools? TryResolveForLocalPlayer()
    {
        var players = CombatManager.Instance.DebugOnlyGetState()?.Players;
        var player = players == null ? null : LocalPlayerResolver.Resolve(players);
        return player?.PlayerCombatState == null ? null : Resolve(player);
    }
}
