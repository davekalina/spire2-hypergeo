using Godot;

namespace Hypergeo.HypergeoCode;

/// <summary>
/// The mod's own settings, kept in the game's user directory.
///
/// The game's <c>ModSettings</c> only records which mods are enabled — there is no
/// per-mod settings store to write into and no API for one — so this holds the handful
/// of choices that have to outlive a session, in a small file of its own.
/// </summary>
internal static class HypergeoSettings
{
    private const string Path = "user://hypergeo_settings.cfg";
    private const string Section = "hypergeo";
    private const string DrawPileTakeoverKey = "all_cards_on_draw_pile_button";

    private static bool _loaded;
    private static bool _drawPileTakeover;

    /// <summary>
    /// Whether the controller's Draw Pile button opens All Cards instead.
    ///
    /// Off by default: it costs the player the draw pile screen on a controller, which
    /// is only a fair trade for someone who wants All Cards more.
    /// </summary>
    public static bool DrawPileTakeover
    {
        get
        {
            Load();
            return _drawPileTakeover;
        }
        set
        {
            Load();
            if (_drawPileTakeover == value)
                return;
            _drawPileTakeover = value;
            Save();
        }
    }

    private static void Load()
    {
        if (_loaded)
            return;
        _loaded = true;
        var file = new ConfigFile();
        if (file.Load(Path) != Error.Ok)
            return;
        _drawPileTakeover = (bool)file.GetValue(Section, DrawPileTakeoverKey, false);
    }

    private static void Save()
    {
        var file = new ConfigFile();
        // Keep anything a later version wrote that this one does not know about.
        file.Load(Path);
        file.SetValue(Section, DrawPileTakeoverKey, _drawPileTakeover);
        var error = file.Save(Path);
        if (error != Error.Ok)
            MainFile.Logger.Warn($"Could not write {Path}: {error}.");
    }
}
