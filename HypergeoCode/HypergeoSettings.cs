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
    private const string BadgeSection = "odds_badge";
    private const string DrawPileTakeoverKey = "all_cards_on_draw_pile_button";

    private static bool _loaded;
    private static bool _drawPileTakeover;
    private static bool _missingKeys;

    /// <summary>
    /// Where the on-card odds badge sits, in unscaled card pixels about the card's
    /// centre. For reference, from <c>scenes/cards/card.tscn</c>: the card runs
    /// -150..150 across and -211..211 down, and its art -125..125 and -168..22.
    ///
    /// These live in the settings file so the band can be moved by editing it and
    /// reopening the screen, without a rebuild or a restart.
    /// </summary>
    public static float BadgeLeft { get; private set; } = -120f;

    public static float BadgeRight { get; private set; } = 120f;
    public static float BadgeCenterY { get; private set; } = -106f;
    public static float BadgeMinHeight { get; private set; } = 32f;
    public static float BadgePadding { get; private set; } = 10f;
    public static int BadgeCaptionFontSize { get; private set; } = 17;

    /// <summary>
    /// Re-read the file, so a hand edit takes effect the next time the screen opens.
    /// </summary>
    public static void Reload()
    {
        _loaded = false;
        Load();
    }

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
        _missingKeys = false;

        var file = new ConfigFile();
        file.Load(Path);
        _drawPileTakeover = Read(file, Section, DrawPileTakeoverKey, false);
        BadgeLeft = Read(file, BadgeSection, "left", -120f);
        BadgeRight = Read(file, BadgeSection, "right", 120f);
        BadgeCenterY = Read(file, BadgeSection, "center_y", -106f);
        BadgeMinHeight = Read(file, BadgeSection, "min_height", 32f);
        BadgePadding = Read(file, BadgeSection, "padding", 10f);
        BadgeCaptionFontSize = Read(file, BadgeSection, "caption_font_size", 17);

        // Write every key back the first time, so the file lists what can be tuned
        // instead of leaving it to be guessed at.
        if (_missingKeys)
            Save();
    }

    private static T Read<[MustBeVariant] T>(
        ConfigFile file, string section, string key, T fallback)
    {
        if (file.HasSectionKey(section, key))
            return file.GetValue(section, key).As<T>();
        _missingKeys = true;
        return fallback;
    }

    private static void Save()
    {
        var file = new ConfigFile();
        // Keep anything a later version wrote that this one does not know about.
        file.Load(Path);
        file.SetValue(Section, DrawPileTakeoverKey, _drawPileTakeover);
        file.SetValue(BadgeSection, "left", BadgeLeft);
        file.SetValue(BadgeSection, "right", BadgeRight);
        file.SetValue(BadgeSection, "center_y", BadgeCenterY);
        file.SetValue(BadgeSection, "min_height", BadgeMinHeight);
        file.SetValue(BadgeSection, "padding", BadgePadding);
        file.SetValue(BadgeSection, "caption_font_size", BadgeCaptionFontSize);
        var error = file.Save(Path);
        if (error != Error.Ok)
            MainFile.Logger.Warn($"Could not write {Path}: {error}.");
    }
}
