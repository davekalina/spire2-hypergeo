using Godot;
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
/// binding matches. Settings → Input is built from three lists of remappable actions and
/// edits those same dictionaries.
///
/// So this action joins the game's system rather than working around it. The action
/// itself is registered with Godot (with no key event of its own — the manager supplies
/// the input), given a row title, added to the remappable lists so the settings screen
/// builds a row for it, and given a keyboard default through
/// <see cref="InputSettingsPatch" />. That order matters — see <see cref="Install" />.
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

    public static void Install()
    {
        // No key event of its own: NInputManager supplies the input, and a second
        // source would fire the shortcut twice and ignore any rebinding.
        if (!InputMap.HasAction(Action))
            InputMap.AddAction(Action);

        // Title first, and give up if it will not take. The remappable lists are what
        // make the settings screen build a row, and every row indexes the title table
        // unconditionally — so listing the action without a title does not mean "no
        // row", it means the row throws in its own _Ready and stops halfway, leaving
        // the four signals it had not yet connected to be disconnected on the way out.
        // Being absent from Settings is the acceptable failure here; breaking the
        // panel for the game's own bindings is not.
        if (!AddSettingsRowTitle())
            return;

        // Three lists since 0.110: the game split keyboard bindings into a
        // mouse-and-keyboard set and a keyboard-only set, each with its own column in
        // Settings. The shortcut belongs in every one of them.
        AddToRemappable(NInputManager.remappableMKbInputs);
        AddToRemappable(NInputManager.remappableKbOnlyInputs);
        AddToRemappable(NInputManager.remappableControllerInputs);
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
    /// Every settings row looks its title up in this table and would throw on a missing
    /// entry, so the action needs one even though <see cref="InputSettingsPatch" />
    /// writes the visible label itself. Since 0.111 the rebind path indexes it too, to
    /// name the binding in its "cannot remap" toast.
    ///
    /// The table was <c>private static _commandToLocTitle</c> until 0.111 renamed it and
    /// made it public; reflecting for the old name is what quietly stopped working.
    /// </summary>
    /// <returns>Whether the title is in place.</returns>
    private static bool AddSettingsRowTitle()
    {
        var titles = NInputSettingsEntry.commandToLocTitle;
        if (titles is null)
        {
            MainFile.Logger.Warn(
                "Settings input title table not found. The All Cards shortcut will not " +
                "appear in Settings.");
            return false;
        }
        titles[Action] = Action;
        return true;
    }
}
