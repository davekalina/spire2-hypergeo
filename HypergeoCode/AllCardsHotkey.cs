using Godot;

namespace Hypergeo.HypergeoCode;

/// <summary>
/// The keyboard shortcut that opens the All Cards screen.
///
/// The game's own combat panels are driven by <c>NInputManager</c>, which keeps its
/// remappable actions in Settings and translates raw keys into them itself. A mod
/// cannot add a row to that settings screen, so this action carries a real key event
/// on Godot's input map instead: <see cref="NHotkeyManager" /> dispatches whatever
/// actions it has bindings for, not only the game's own, so the shortcut works without
/// touching anything the game owns.
///
/// The cost of staying out of the game's input system is that the key cannot be
/// rebound in game. Change <see cref="Key" /> below and rebuild to move it.
/// </summary>
internal static class AllCardsHotkey
{
    /// <summary>
    /// The key that opens the All Cards screen, and closes it again while it is open.
    ///
    /// Combat already uses A for the draw pile, S for the discard pile, D for the deck,
    /// X for the exhaust pile, M for the map, E to accept, Space to peek, and 1-0 to
    /// select cards; W is free and sits in the same cluster as the pile keys.
    /// </summary>
    public const Key Key = Godot.Key.W;

    /// <summary>Godot input action name. Namespaced so it cannot collide.</summary>
    public const string Action = "hypergeo_view_all_cards";

    /// <summary>
    /// Put the action on Godot's input map. Safe to call more than once, and it leaves
    /// an existing action alone so a reload does not stack duplicate key events.
    /// </summary>
    public static void EnsureRegistered()
    {
        if (InputMap.HasAction(Action))
            return;
        InputMap.AddAction(Action);
        // Physical, so the shortcut stays on the same physical key across layouts —
        // the same choice the game makes for its own bindings.
        InputMap.ActionAddEvent(Action, new InputEventKey { PhysicalKeycode = Key });
        MainFile.Logger.Info($"All Cards shortcut bound to {Key}.");
    }
}
