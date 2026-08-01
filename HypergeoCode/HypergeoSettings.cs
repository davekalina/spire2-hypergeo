using Godot;

namespace Hypergeo.HypergeoCode;

/// <summary>
/// The mod's own settings, kept in the game's user directory.
///
/// The game's <c>ModSettings</c> only records which mods are enabled — there is no
/// per-mod settings store to write into and no API for one — so this holds the handful
/// of choices that have to outlive a session, in a small file of its own.
///
/// Everything except the Draw Pile override is a *starting* value. The All Cards screen
/// keeps its own copy for the session, so a toggle flipped mid-run is answering a
/// question about that run and is forgotten when the game closes; what is written here
/// is what each run begins with. See <see cref="AllCardsSession" />.
/// </summary>
internal static class HypergeoSettings
{
    private const string Path = "user://hypergeo_settings.cfg";
    private const string Section = "hypergeo";

    private static bool _loaded;
    private static readonly Dictionary<string, bool> Values = [];

    internal static class Keys
    {
        public const string DrawPileTakeover = "all_cards_on_draw_pile_button";
        public const string ShowOddsOnCards = "show_odds_on_cards";
        public const string CombineSameCardOdds = "combine_same_card_odds";
        public const string IncludeHandInReshuffle = "include_hand_in_reshuffle";
        public const string RawdogMode = "rawdog_mode";
    }

    private static readonly Dictionary<string, bool> Defaults = new()
    {
        // Off: it costs the player the draw pile screen on a controller, which is only
        // a fair trade for someone who wants All Cards more.
        [Keys.DrawPileTakeover] = false,
        // On: the odds are what the screen is for.
        [Keys.ShowOddsOnCards] = true,
        // On: "will I draw a Strike" is the usual question, not "will I draw this one".
        [Keys.CombineSameCardOdds] = true,
        // On: the usual question is about next turn, and the turn ending does put the
        // hand in the discard.
        [Keys.IncludeHandInReshuffle] = true,
        // Off: the plain calculator is the specialist view, not the everyday one.
        [Keys.RawdogMode] = false,
    };

    /// <summary>Whether the controller's Draw Pile button opens All Cards instead.</summary>
    public static bool DrawPileTakeover
    {
        get => Get(Keys.DrawPileTakeover);
        set => Set(Keys.DrawPileTakeover, value);
    }

    public static bool Get(string key)
    {
        Load();
        return Values.TryGetValue(key, out var value) ? value : Default(key);
    }

    public static void Set(string key, bool value)
    {
        Load();
        if (Values.TryGetValue(key, out var current) && current == value)
            return;
        Values[key] = value;
        Save();
    }

    public static bool Default(string key) =>
        Defaults.TryGetValue(key, out var value) && value;

    private static void Load()
    {
        if (_loaded)
            return;
        _loaded = true;
        var file = new ConfigFile();
        if (file.Load(Path) != Error.Ok)
            return;
        foreach (var (key, fallback) in Defaults)
            Values[key] = file.GetValue(Section, key, fallback).AsBool();
    }

    private static void Save()
    {
        var file = new ConfigFile();
        // Keep anything a later version wrote that this one does not know about.
        file.Load(Path);
        foreach (var (key, value) in Values)
            file.SetValue(Section, key, value);
        var error = file.Save(Path);
        if (error != Error.Ok)
            MainFile.Logger.Warn($"Could not write {Path}: {error}.");
    }
}
