using System.Reflection;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Modding;
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
    // take the bottom strip and the description gives up the room.
    private const float DescriptionBottom = 780f;
    private const float ControlsTop = 796f;

    [HarmonyPostfix]
    [HarmonyPatch(nameof(NModInfoContainer.Fill))]
    private static void AfterFill(NModInfoContainer __instance, Mod mod)
    {
        var isThisMod = mod.manifest?.id == MainFile.ModId;
        var controls = Resolve(__instance, isThisMod);
        if (controls != null)
            controls.Visible = isThisMod;
        if (__instance.GetNodeOrNull<Control>("ModDescription") is { } description)
            description.OffsetBottom = isThisMod ? DescriptionBottom : 886f;
    }

    [HarmonyPostfix]
    [HarmonyPatch(nameof(NModInfoContainer.Clear))]
    private static void AfterClear(NModInfoContainer __instance)
    {
        if (__instance.GetNodeOrNull<Control>(ContainerName) is { } controls)
            controls.Visible = false;
    }

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

        var takeover = SceneHelper.Instantiate<NLibraryStatTickbox>(TickboxScene);
        takeover.Name = "DrawPileTakeover";
        takeover.FocusNeighborTop = new NodePath();
        takeover.FocusNeighborBottom = new NodePath();
        takeover.CustomMinimumSize = new Vector2(0, 42);
        takeover.Ready += () =>
        {
            takeover.SetLabel("All Cards on the Draw Pile button");
            takeover.IsTicked = HypergeoSettings.DrawPileTakeover;
        };
        takeover.Toggled += OnDrawPileTakeoverToggled;
        controls.AddChild(takeover);

        panel.AddChild(controls);
        return controls;
    }

    /// <summary>
    /// Write the choice through to the live controller map as well as to disk, so it
    /// takes effect without a restart. The map the game rebuilds on its next load runs
    /// through the same helper, so the two cannot disagree.
    /// </summary>
    private static void OnDrawPileTakeoverToggled(NTickbox tickbox)
    {
        HypergeoSettings.DrawPileTakeover = tickbox.IsTicked;
        var manager = NInputManager.Instance;
        if (manager == null)
            return;
        var field = typeof(NInputManager).GetField(
            "_controllerInputMap", BindingFlags.Instance | BindingFlags.NonPublic);
        if (field?.GetValue(manager) is not Dictionary<StringName, StringName> map)
            return;
        if (!tickbox.IsTicked)
            // Give the draw pile its button back; the game's defaults are the record of
            // where it belongs.
            map[MegaCrit.Sts2.Core.ControllerInput.MegaInput.viewDrawPile] =
                AllCardsHotkey.TakeoverButton;
        AllCardsHotkey.ApplyDrawPileTakeover(map);
    }
}
