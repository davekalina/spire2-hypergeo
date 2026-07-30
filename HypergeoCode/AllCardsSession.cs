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
/// <item>The odds overlay and Rawdog Mode are display preferences and last as long as
/// the game does.</item>
/// <item>The calculator's numbers and any hand-picked draw count belong to one combat,
/// and are dropped when a different combat starts.</item>
/// <item>The selection belongs to one hand. It survives closing and reopening the
/// screen, and nothing else — see <see cref="SyncToPiles" />.</item>
/// </list>
/// </summary>
internal static class AllCardsSession
{
    private static object? _combat;
    private static string? _pileFingerprint;

    /// <summary>
    /// Whether the per-card odds overlay is switched on. Outlives combat, and starts on
    /// — the odds are what the screen is for.
    /// </summary>
    public static bool ShowOddsOnCards { get; set; } = true;

    /// <summary>The cards the player picked, by instance.</summary>
    public static HashSet<CardModel> SelectedCards { get; } = [];

    /// <summary>How many of the selection the hand needs.</summary>
    public static int TargetHits { get; set; }

    /// <summary>
    /// Whether the shelf shows the plain hypergeometric calculator instead of the
    /// combat query. A view preference, so it outlives combat like the odds overlay.
    /// </summary>
    public static bool RawdogMode { get; set; }

    /// <summary>
    /// Whether the hand joins the discard pile in the reshuffle.
    ///
    /// True by default, because the usual question is about next turn's hand, and the
    /// end of the turn does put the hand in the discard. Turn it off to ask about
    /// drawing more cards during *this* turn, when the hand is staying where it is and
    /// only the discard would be reshuffled.
    /// </summary>
    public static bool IncludeHandInReshuffle { get; set; } = true;

    /// <summary>
    /// The calculator's four numbers, and whether they have been seeded from the deck
    /// yet. Seeding is deferred because the deck size is only known once a screen opens.
    /// </summary>
    public static int Population { get; set; }
    public static int Sample { get; set; }
    public static int Successes { get; set; }
    public static int Wanted { get; set; }
    public static bool CalculatorSeeded { get; private set; }

    public static void SeedCalculator(int population)
    {
        if (CalculatorSeeded)
            return;
        CalculatorSeeded = true;
        Population = Math.Max(1, population);
        Sample = Math.Min(5, Population);
        Successes = Math.Min(1, Population);
        Wanted = Math.Min(1, Successes);
    }

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
        TargetHits = 0;
        ClearChosenDrawCount();
        // The deck changes between fights, so the calculator reseeds from the new one.
        CalculatorSeeded = false;
    }

    /// <summary>
    /// Drop the selection unless the piles are exactly as they were left.
    ///
    /// A selection is a question about one particular board — these cards, in these
    /// piles, with this many left to draw. Playing a card or drawing a hand asks a
    /// different question, and carrying the old answer over would quietly report odds
    /// for a board that no longer exists. Closing and reopening the screen changes
    /// nothing, which is the one case worth remembering.
    /// </summary>
    public static void SyncToPiles(string fingerprint)
    {
        if (_pileFingerprint == fingerprint)
            return;
        _pileFingerprint = fingerprint;
        SelectedCards.Clear();
        TargetHits = 0;
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
