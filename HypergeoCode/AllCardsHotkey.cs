using System.Reflection;
using Godot;
using MegaCrit.Sts2.Core.ControllerInput;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.Screens.Settings;

namespace Hypergeo.HypergeoCode;

/// <summary>
/// The shortcut that opens the All Cards screen, listed in Settings → Input beside the
/// game's own bindings so it can be rebound to a key or a controller button.
///
/// The game drives its shortcuts through <c>NInputManager</c> rather than through
/// Godot's input map: the manager holds action → key and action → controller-button
/// dictionaries, watches raw input, and synthesises an <c>InputEventAction</c> when a
/// binding matches. Settings → Input is built from two lists of remappable actions and
/// edits those same dictionaries.
///
/// So this action joins the game's system rather than working around it. The action
/// itself is registered with Godot (with no key event of its own — the manager supplies
/// the input), added to both remappable lists so the settings screen builds a row for
/// it, and given a keyboard default through <see cref="InputSettingsPatch" />.
/// </summary>
internal static class AllCardsHotkey
{
    /// <summary>Godot input action name. Namespaced so it cannot collide.</summary>
    public const string Action = "hypergeo_view_all_cards";

    /// <summary>The row's label in Settings → Input.</summary>
    public const string SettingsTitle = "View All Cards";

    /// <summary>
    /// The key the shortcut starts on, and returns to under Reset to Default.
    ///
    /// Combat already uses A for the draw pile, S for the discard pile, D for the deck,
    /// X for the exhaust pile, M for the map, E to accept, Space to peek, and 1-0 to
    /// select cards; W is free and sits in the same cluster as the pile keys.
    /// </summary>
    public const Key DefaultKey = Key.W;

    /// <summary>
    /// The controller button the shortcut takes over when the player asks for it in
    /// Mod Settings. This is the game's Draw Pile button, which the All Cards screen
    /// supersedes — it shows the draw pile and then some.
    /// </summary>
    public static readonly StringName TakeoverButton = Controller.leftTrigger;

    /// <summary>
    /// Point the Draw Pile button at this shortcut, if the player has asked for that.
    ///
    /// Under Steam Input the physical binding belongs to Steam and the game disables
    /// its own controller rebinding, but Steam only owns the first hop: a press becomes
    /// a Steam action, which the game turns into a <c>Controller.*</c> input, which
    /// <em>this</em> map turns into a game action. That last hop is the game's own, so
    /// redirecting it works whether or not Steam is in the picture.
    ///
    /// Both bindings would otherwise fire together, so the draw pile gives its button
    /// up rather than sharing it.
    /// </summary>
    public static void ApplyDrawPileTakeover(Dictionary<StringName, StringName> map)
    {
        if (!HypergeoSettings.DrawPileTakeover)
        {
            map.Remove(Action);
            return;
        }
        foreach (var bound in map
                     .Where(binding => binding.Value == TakeoverButton)
                     .Select(binding => binding.Key)
                     .ToList())
            map.Remove(bound);
        map[Action] = TakeoverButton;
    }

    public static void Install()
    {
        // No key event of its own: NInputManager supplies the input, and a second
        // source would fire the shortcut twice and ignore any rebinding.
        if (!InputMap.HasAction(Action))
            InputMap.AddAction(Action);

        AddToRemappable(NInputManager.remappableKeyboardInputs);
        AddToRemappable(NInputManager.remappableControllerInputs);
        AddSettingsRowTitle();
    }

    /// <summary>
    /// Settings → Input builds one row per action across these two lists, and uses them
    /// again to decide whether a key press or a controller button may rebind a row.
    /// Being in both is what makes the shortcut bindable either way.
    /// </summary>
    private static void AddToRemappable(IReadOnlyList<StringName> inputs)
    {
        if (inputs.Any(existing => existing.ToString() == Action))
            return;
        if (inputs is List<StringName> list)
            list.Add(Action);
        else
            MainFile.Logger.Warn(
                $"Remappable input list is {inputs.GetType().Name}, not a mutable list. " +
                "The All Cards shortcut will not appear in Settings.");
    }

    /// <summary>
    /// Every settings row looks its title up in a private table and would throw on a
    /// missing entry, so the action needs one even though
    /// <see cref="InputSettingsPatch" /> writes the visible label itself.
    /// </summary>
    private static void AddSettingsRowTitle()
    {
        var field = typeof(NInputSettingsEntry).GetField(
            "_commandToLocTitle", BindingFlags.Static | BindingFlags.NonPublic);
        if (field?.GetValue(null) is not Dictionary<StringName, string> titles)
        {
            MainFile.Logger.Warn(
                "Settings input title table not found. The All Cards shortcut will not " +
                "appear in Settings.");
            return;
        }
        titles[Action] = Action;
    }
}
