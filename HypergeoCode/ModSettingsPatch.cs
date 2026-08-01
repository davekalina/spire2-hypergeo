using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Modding;
using MegaCrit.Sts2.Core.Nodes.HoverTips;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.Screens.CardLibrary;
using MegaCrit.Sts2.Core.Nodes.Screens.ModdingScreen;

namespace Hypergeo.HypergeoCode;

/// <summary>
/// The mod's settings, shown in the info panel of Settings → Mod Settings when this mod
/// is selected.
///
/// The game has no per-mod settings API and no page to put one on, but it does have a
/// panel that already describes the selected mod. Adding the controls there keeps them
/// where a player would look for them, and keeps them out of the combat UI, where a
/// controller binding has no business being changed.
/// </summary>
[HarmonyPatch(typeof(NModInfoContainer))]
internal static class ModSettingsPatch
{
    private const string ContainerName = "HypergeoModSettings";
    private const string TickboxScene = "screens/card_library/card_library_tickbox";

    // The info panel is 666 x 901 with its description running to y 886. The controls
    // take the bottom strip and the description gives up the room. Five rows of 42
    // with 4 between them need 226, so the strip starts high enough to hold them.
    private const float RowHeight = 42f;
    private const float RowSeparation = 4f;
    private const float PanelBottom = 886f;
    private const float ControlsTop =
        PanelBottom - (5 * RowHeight + 4 * RowSeparation);
    private const float DescriptionBottom = ControlsTop - 16f;

    /// <summary>
    /// The defaults every run starts from, in the order they appear in the shelf so the
    /// two screens read the same way round. The Draw Pile override is not one of these:
    /// it is an input binding that applies the moment it is set, not a starting value.
    /// </summary>
    private static readonly (string Key, string Label, string Description)[] Defaults =
    [
        (HypergeoSettings.Keys.ShowOddsOnCards, "Show Odds on Cards",
            "Print each card's chance of being drawn on the card itself."),
        (HypergeoSettings.Keys.CombineSameCardOdds, "Combine Same Card Odds",
            "Count every copy of a card together, so a Strike shows the chance of " +
            "drawing any Strike rather than that one copy."),
        (HypergeoSettings.Keys.IncludeHandInReshuffle, "Include Hand in Reshuffle",
            "Count your hand as part of the reshuffle, which is what happens when the " +
            "turn ends. Off asks about drawing more cards during this turn instead."),
        (HypergeoSettings.Keys.RawdogMode, "Rawdog Mode",
            "Open on the plain hypergeometric calculator rather than the combat query."),
    ];

    private const string DefaultPrefix = "Default: ";

    private const string DefaultsNote =
        "\n\nThis sets what the All Cards screen starts with. Changing it there still " +
        "works, and lasts until you close the game.";

    [HarmonyPostfix]
    [HarmonyPatch(nameof(NModInfoContainer.Fill))]
    private static void AfterFill(NModInfoContainer __instance, Mod mod) =>
        Guard.Run("Adding this mod's settings to the info panel", () =>
        {
            var isThisMod = mod.manifest?.id == MainFile.ModId;
            var controls = Resolve(__instance, isThisMod);
            if (controls != null)
                controls.Visible = isThisMod;
            if (__instance.GetNodeOrNull<Control>("ModDescription") is { } description)
                description.OffsetBottom = isThisMod ? DescriptionBottom : 886f;
        });

    [HarmonyPostfix]
    [HarmonyPatch(nameof(NModInfoContainer.Clear))]
    private static void AfterClear(NModInfoContainer __instance) =>
        Guard.Run("Hiding this mod's settings", () =>
        {
            if (__instance.GetNodeOrNull<Control>(ContainerName) is { } controls)
                controls.Visible = false;
        });

    private static Control? Resolve(NModInfoContainer panel, bool create)
    {
        if (panel.GetNodeOrNull<Control>(ContainerName) is { } existing)
            return existing;
        if (!create)
            return null;

        var controls = new VBoxContainer
        {
            Name = ContainerName,
            OffsetLeft = 25f,
            OffsetTop = ControlsTop,
            OffsetRight = 641f,
            OffsetBottom = 886f,
        };
        controls.AddThemeConstantOverride("separation", 4);

        // Not a default: this one applies the moment it is set, so it carries no prefix.
        controls.AddChild(CreateTickbox(
            HypergeoSettings.Keys.DrawPileTakeover,
            "Override Draw Pile button",
            "Make the combat Draw Pile button open All Cards instead. Worth it on a " +
            "controller, where All Cards has no button of its own — at the cost of " +
            "the draw pile screen."));
        foreach (var (key, label, description) in Defaults)
            controls.AddChild(
                CreateTickbox(key, DefaultPrefix + label, description + DefaultsNote));

        panel.AddChild(controls);
        return controls;
    }

    private static NLibraryStatTickbox CreateTickbox(
        string key, string label, string description)
    {
        var tickbox = SceneHelper.Instantiate<NLibraryStatTickbox>(TickboxScene);
        tickbox.Name = key;
        // The scene's neighbours name nodes that only exist in the Card Library.
        tickbox.FocusNeighborTop = new NodePath();
        tickbox.FocusNeighborBottom = new NodePath();
        tickbox.CustomMinimumSize = new Vector2(0, RowHeight);
        tickbox.Ready += () =>
        {
            tickbox.SetLabel(label);
            tickbox.IsTicked = HypergeoSettings.Get(key);
        };
        tickbox.Toggled += box => OnToggled(key, box);
        // Left, not Right: this panel runs to the right edge of the screen, so a tip
        // opening that way would have nowhere to go.
        tickbox.MouseEntered += () => NHoverTipSet.CreateAndShow(
            tickbox,
            NativeHoverTip.Create(label, description, $"HypergeoSetting:{key}"),
            HoverTipAlignment.Left);
        tickbox.MouseExited += () => NHoverTipSet.Remove(tickbox);
        return tickbox;
    }

    /// <summary>
    /// Write it down, then drop whatever the session had decided so the new default is
    /// read afresh. Without that, changing a default here would appear to do nothing
    /// until the game was restarted, because the screen had already made its mind up.
    ///
    /// The Draw Pile override needs no such thing: it is read at the moment the button
    /// acts rather than baked into a binding, so it takes effect immediately and leaves
    /// every input map untouched.
    /// </summary>
    private static void OnToggled(string key, NTickbox tickbox)
    {
        HypergeoSettings.Set(key, tickbox.IsTicked);
        if (key != HypergeoSettings.Keys.DrawPileTakeover)
            AllCardsSession.ResetPreferences();
    }
}
