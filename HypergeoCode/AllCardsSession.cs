using MegaCrit.Sts2.Core.Models;

namespace Hypergeo.HypergeoCode;

/// <summary>
/// What the All Cards screen remembers between visits.
///
/// The game builds a fresh screen every time the pile view opens, so anything the
/// player set up would otherwise be thrown away the moment they closed it. State
/// lives here instead, at two different lifetimes:
///
/// <list type="bullet">
/// <item>The odds overlay is a display preference and lasts as long as the game does.</item>
/// <item>The selection, how many of it is wanted, and any hand-picked draw count belong
/// to one combat, and are dropped when a different combat starts.</item>
/// </list>
///
/// Selections hold card instances, so they survive cards moving between piles — playing
/// a card does not deselect it. A card that leaves the reachable pools entirely is
/// pruned by the screen when it next renders.
/// </summary>
internal static class AllCardsSession
{
    private static object? _combat;

    /// <summary>Whether the per-card odds overlay is switched on. Outlives combat.</summary>
    public static bool ShowOddsOnCards { get; set; }

    /// <summary>The cards the player picked, by instance.</summary>
    public static HashSet<CardModel> SelectedCards { get; } = [];

    /// <summary>How many of the selection the hand needs.</summary>
    public static int TargetHits { get; set; } = 1;

    /// <summary>
    /// A draw count the player set by hand, and the natural count it was set against.
    /// Both null while the screen is simply showing the real next-turn draw.
    /// </summary>
    public static int? ChosenDrawCount { get; private set; }

    private static int? _naturalWhenChosen;

    /// <summary>
    /// Point the combat-scoped state at <paramref name="combat" />, clearing it if this
    /// is a different fight from the one it was gathered in.
    /// </summary>
    public static void SyncToCombat(object? combat)
    {
        if (ReferenceEquals(_combat, combat))
            return;
        _combat = combat;
        SelectedCards.Clear();
        TargetHits = 1;
        ClearChosenDrawCount();
    }

    public static void SetChosenDrawCount(int chosen, int natural)
    {
        ChosenDrawCount = chosen;
        _naturalWhenChosen = natural;
    }

    public static void ClearChosenDrawCount()
    {
        ChosenDrawCount = null;
        _naturalWhenChosen = null;
    }

    /// <summary>
    /// The draw count to open with. A hand-picked count is kept only while the real
    /// next-turn draw is unchanged — once the situation moves, the honest number wins.
    /// </summary>
    public static int ResolveDrawCount(int natural)
    {
        if (ChosenDrawCount is not { } chosen || _naturalWhenChosen != natural)
        {
            ClearChosenDrawCount();
            return natural;
        }
        return chosen;
    }
}
