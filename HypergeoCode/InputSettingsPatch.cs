using System.Reflection;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.Screens.Settings;
using MegaCrit.Sts2.addons.mega_text;

namespace Hypergeo.HypergeoCode;

/// <summary>
/// Teaches the game's input system about the All Cards shortcut: a keyboard default,
/// a readable label in Settings → Input, and a safe first controller binding.
/// </summary>
[HarmonyPatch(typeof(NInputManager))]
internal static class InputSettingsPatch
{
    private static readonly FieldInfo? ControllerMapField = typeof(NInputManager)
        .GetField("_controllerInputMap", BindingFlags.Instance | BindingFlags.NonPublic);

    /// <summary>
    /// Put the shortcut in the defaults rather than injecting it after load. The
    /// defaults are the base every saved mapping is layered onto and the exact thing
    /// Reset to Default restores, so this is the one place that covers a fresh profile,
    /// a returning one, and a reset alike.
    /// </summary>
    [HarmonyPostfix]
    [HarmonyPatch("DefaultHotkeyInputMap", MethodType.Getter)]
    private static void AfterDefaultHotkeyInputMap(
        Dictionary<StringName, Key> __result) =>
        __result[AllCardsHotkey.Action] = AllCardsHotkey.DefaultKey;

    /// <summary>
    /// Keyboard-only mode keeps a second set of bindings, so the default belongs in both.
    /// </summary>
    [HarmonyPostfix]
    [HarmonyPatch("DefaultKbOnlyInputMap", MethodType.Getter)]
    private static void AfterDefaultKbOnlyInputMap(
        Dictionary<StringName, Key> __result) =>
        __result[AllCardsHotkey.Action] = AllCardsHotkey.DefaultKey;

    /// <summary>
    /// Rebinding a controller button swaps it with whatever held it, by giving that
    /// input the button the rebound action used to have. The shortcut starts with no
    /// controller button at all, so there is nothing to hand over and the game would
    /// throw looking for it.
    ///
    /// Free the button instead: whatever held it becomes unbound, which is visible in
    /// the same list and is the honest outcome when a nineteen-button pad gains a
    /// twentieth action. This only runs for a shortcut that is currently unbound, a
    /// state the game's own actions are never in.
    /// </summary>
    [HarmonyPrefix]
    [HarmonyPatch(nameof(NInputManager.ModifyControllerButton))]
    private static void BeforeModifyControllerButton(
        NInputManager __instance, StringName input, StringName controllerInput)
    {
        if (input.ToString() != AllCardsHotkey.Action)
            return;
        if (ControllerMapField?.GetValue(__instance) is not
            Dictionary<StringName, StringName> map)
            return;
        if (map.ContainsKey(input))
            return;
        foreach (var held in map
                     .Where(binding => binding.Value == controllerInput)
                     .Select(binding => binding.Key)
                     .ToList())
            map.Remove(held);
    }
}

/// <summary>Gives the shortcut's settings row a readable name.</summary>
[HarmonyPatch(typeof(NInputSettingsEntry))]
internal static class InputSettingsEntryPatch
{
    /// <summary>
    /// Every other row takes its title from the game's localisation tables, which a mod
    /// cannot add to, so this one is written directly after the row builds itself.
    /// </summary>
    [HarmonyPostfix]
    [HarmonyPatch(nameof(NInputSettingsEntry._Ready))]
    private static void AfterReady(NInputSettingsEntry __instance)
    {
        if (__instance.InputName?.ToString() != AllCardsHotkey.Action)
            return;
        // A MegaLabel since 0.110; it was a MegaRichTextLabel before, and asking for the
        // wrong one throws rather than returning null.
        __instance.GetNode<MegaLabel>("%InputLabel").Text =
            AllCardsHotkey.SettingsTitle;
    }
}
