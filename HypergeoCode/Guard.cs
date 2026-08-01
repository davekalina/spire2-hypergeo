namespace Hypergeo.HypergeoCode;

/// <summary>
/// Keeps a failure in this mod from becoming a failure in the game.
///
/// Every entry point here is a Harmony patch on a method the game calls for its own
/// reasons — a screen readying itself, a card holder building its hover tips. An
/// exception thrown inside one of those does not stop at the mod: it comes out of the
/// game's own method, and the player loses the draw pile screen, or card hovers, or
/// the combat UI. The mod reads the game through some three dozen node paths and a
/// handful of private fields, any of which a game update can move — 0.110 changed the
/// type of one label and would have thrown on it — so that is a question of when.
///
/// Wrapping the boundary turns "this mod broke my game" into "this mod stopped
/// working", which for something that only reads and reports is the whole promise.
/// The failure is logged, so the log still says what went wrong.
///
/// This belongs only at the boundary. Inside the mod, an exception should be allowed
/// to surface while it is being written.
/// </summary>
internal static class Guard
{
    private static readonly HashSet<string> Reported = [];

    /// <summary>Run <paramref name="action" />; log and swallow anything it throws.</summary>
    public static void Run(string what, Action action)
    {
        try
        {
            action();
        }
        catch (Exception error)
        {
            Report(what, error);
        }
    }

    /// <summary>
    /// Run <paramref name="action" />, falling back to <paramref name="onFailure" />.
    /// For patches whose return value decides what the game does next: the fallback is
    /// the answer that leaves the game behaving as though the mod were not installed.
    /// </summary>
    public static T Run<T>(string what, Func<T> action, T onFailure)
    {
        try
        {
            return action();
        }
        catch (Exception error)
        {
            Report(what, error);
            return onFailure;
        }
    }

    /// <summary>
    /// The first failure of a kind is reported in full, later ones in a line. Some of
    /// these sit on paths the game runs every frame, and a stack trace per frame would
    /// bury the first one, which is the one worth reading.
    /// </summary>
    private static void Report(string what, Exception error)
    {
        if (Reported.Add(what))
            MainFile.Logger.Error(
                $"{what} failed, so {MainFile.ModName} skipped it. The game is " +
                $"unaffected; the mod may be missing part of its display.\n{error}");
        else
            MainFile.Logger.Warn($"{what} failed again.");
    }
}
