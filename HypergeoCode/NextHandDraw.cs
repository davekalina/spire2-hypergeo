using System.Reflection;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Models.Relics;

namespace Hypergeo.HypergeoCode;

/// <summary>
/// How many cards the next hand draws, and which effects decided that.
///
/// The game's draw hooks answer for the turn they are asked in, and the real draw runs
/// at the *start* of the next turn — after the turn number has been incremented and
/// after several effects have rolled their per-turn bookkeeping forward. Asking them
/// mid-turn therefore describes the hand the player is already holding. This type
/// advances that bookkeeping across the call and restores it immediately, so the
/// answer is the hand about to be dealt.
///
/// Order at turn start, from CombatManager: Creature.BeforeTurnStart captures each
/// power's amount, Hook.BeforeSideTurnStart rolls per-turn relic counters,
/// SetupPlayerTurn runs BeforeHandDraw then ModifyHandDraw then the draw itself and
/// then AfterPlayerTurnStart, and only afterwards does Hook.AfterSideTurnStart decay
/// powers. So effects that decay after the draw are read at their current amount.
/// </summary>
internal sealed record NextHandDraw(int Count, IReadOnlyList<string> Modifiers)
{
    private static readonly NextHandDraw None = new(0, []);

    public static NextHandDraw Resolve(
        ICombatState state, Player player, int retainedCards)
    {
        if (player.PlayerCombatState == null)
            return None;

        bool shouldDraw;
        decimal modified;
        IEnumerable<AbstractModel> modifiers;
        using (new TurnBoundary(player))
        {
            shouldDraw = Hook.ShouldDraw(state, player, fromHandDraw: true, out _);
            modified = Hook.ModifyHandDraw(
                state, player, CombatManager.baseHandDrawCount, out modifiers);
        }
        if (!shouldDraw)
            return None;

        var names = modifiers
            .Select(DisplayName)
            .OfType<string>()
            .Distinct()
            .ToList();

        var desired = Math.Max(0, (int)modified);
        var capacity = Math.Max(0, CardPile.MaxCardsInHand - retainedCards);
        if (desired > capacity)
            names.Add("Hand capacity");
        return new NextHandDraw(Math.Min(capacity, desired), names);
    }

    private static string? DisplayName(AbstractModel model) => model switch
    {
        RelicModel relic => relic.Title.GetRawText(),
        PowerModel power => power.Title.GetRawText(),
        CardModel card => card.Title,
        _ => null,
    };

    /// <summary>
    /// Advances the per-turn bookkeeping the game rolls over before the next draw, and
    /// puts every value back on dispose. All the hooks read across this scope are pure,
    /// so nothing observes the shifted state except the prediction itself.
    ///
    /// Any field that cannot be found is skipped rather than throwing, which degrades
    /// that one effect's prediction instead of breaking the screen.
    /// </summary>
    private sealed class TurnBoundary : IDisposable
    {
        private static readonly FieldInfo? TurnNumber =
            IntField(typeof(PlayerCombatState), "TurnNumber");
        private static readonly FieldInfo? PocketwatchThisTurn =
            IntField(typeof(Pocketwatch), "ThisTurn");
        private static readonly FieldInfo? PocketwatchLastTurn =
            IntField(typeof(Pocketwatch), "LastTurn");
        private static readonly FieldInfo? PollinousTurnsSeen =
            IntField(typeof(PollinousCore), "turnsSeen");
        private static readonly FieldInfo? PendulumTurnsSeen =
            IntField(typeof(Pendulum), "turnsSeen");
        private static readonly FieldInfo? AmountOnTurnStart =
            IntField(typeof(PowerModel), "amountOnTurnStart");

        private readonly List<(object Target, FieldInfo Field, int Original)> _changes = [];

        public TurnBoundary(Player player)
        {
            if (player.PlayerCombatState is { } combatState)
                Shift(combatState, TurnNumber, combatState.TurnNumber + 1);

            foreach (var relic in player.Relics)
                switch (relic)
                {
                    // BeforeSideTurnStart rolls this turn's plays into the count
                    // ModifyHandDraw reads, so Pocketwatch breaks the moment the player
                    // passes its threshold rather than one turn later.
                    case Pocketwatch pocketwatch
                        when PocketwatchThisTurn?.GetValue(pocketwatch) is int played:
                        Shift(pocketwatch, PocketwatchLastTurn, played);
                        break;
                    // BeforeHandDraw ticks this immediately before ModifyHandDraw reads it.
                    case PollinousCore core:
                        Shift(core, PollinousTurnsSeen, core.TurnsSeen + 1);
                        break;
                    // Pendulum counts the same way but wraps, and pays out on the wrap:
                    // BeforeHandDraw sets TurnsSeen = (TurnsSeen + 1) % Turns, and its
                    // ModifyHandDraw adds cards only when that landed on zero. Read
                    // without the tick it pays out a turn early — on a fresh Pendulum
                    // showing 0 rather than on the 2 that is about to wrap.
                    case Pendulum pendulum
                        when pendulum.DynamicVars["Turns"].IntValue is > 0 and var turns:
                        Shift(
                            pendulum,
                            PendulumTurnsSeen,
                            (pendulum.TurnsSeen + 1) % turns);
                        break;
                }

            // Creature.BeforeTurnStart copies Amount into AmountOnTurnStart. A power
            // applied during this turn still reads zero there, which is exactly how
            // "draw cards next turn" avoids firing on the turn it is played.
            foreach (var power in player.Creature.Powers)
                if (power is DrawCardsNextTurnPower)
                    Shift(power, AmountOnTurnStart, power.Amount);
        }

        public void Dispose()
        {
            for (var index = _changes.Count - 1; index >= 0; index--)
            {
                var (target, field, original) = _changes[index];
                field.SetValue(target, original);
            }
            _changes.Clear();
        }

        private void Shift(object target, FieldInfo? field, int value)
        {
            if (field?.GetValue(target) is not int original)
                return;
            _changes.Add((target, field, original));
            field.SetValue(target, value);
        }

        private static FieldInfo? IntField(Type type, string fragment)
        {
            for (var current = type; current != null; current = current.BaseType)
            {
                var field = current
                    .GetFields(
                        BindingFlags.Instance |
                        BindingFlags.NonPublic |
                        BindingFlags.DeclaredOnly)
                    .FirstOrDefault(candidate =>
                        candidate.FieldType == typeof(int) &&
                        candidate.Name.Contains(
                            fragment, StringComparison.OrdinalIgnoreCase));
                if (field != null)
                    return field;
            }
            MainFile.Logger.Warn(
                $"No '{fragment}' field on {type.Name}. Its effect on the next hand's " +
                "draw count will not be predicted.");
            return null;
        }
    }
}
