using Godot;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.HoverTips;
using MegaCrit.Sts2.Core.Nodes.Screens;
using MegaCrit.Sts2.Core.Nodes.Screens.Capstones;
using MegaCrit.Sts2.addons.mega_text;

namespace Hypergeo.HypergeoCode;

/// <summary>Native-looking combat entry cloned from the Discard pile control.</summary>
internal sealed class AllCardsScreenView : IDisposable
{
    private readonly NCombatUi _combatUi;
    private readonly NDrawPileButton _visualButton;
    private readonly Button _inputButton;

    public AllCardsScreenView(NCombatUi combatUi)
    {
        _combatUi = combatUi;
        _visualButton = (NDrawPileButton)combatUi.DrawPile.Duplicate();
        _visualButton.Name = "DrawOddsAllCardsButton";
        _inputButton = new Button
        {
            Flat = true,
            MouseFilter = Control.MouseFilterEnum.Stop,
            FocusMode = Control.FocusModeEnum.None,
            ZIndex = 200,
        };
        _inputButton.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        _inputButton.Pressed += Open;
        _inputButton.MouseEntered += OnMouseEntered;
        _inputButton.MouseExited += OnMouseExited;
    }

    public void Attach()
    {
        var draw = _combatUi.DrawPile;
        draw.GetParent().AddChild(_visualButton);
        _visualButton.Position = draw.Position + new Vector2(121, 0);
        _visualButton.AddChild(_inputButton);
        var count = _visualButton.GetNode<MegaLabel>("CountContainer/Count");
        count.SetTextAutoSize("%");
        count.PivotOffset = count.Size * 0.5f;
        // Live only while combat has a UI, the same lifetime as the button itself.
        // The screen registers the same key as one of its close hotkeys, and the
        // hotkey manager runs the most recent binding, so it toggles.
        NHotkeyManager.Instance?.PushHotkeyReleasedBinding(
            AllCardsHotkey.Action, OpenFromHotkey);
    }

    public void Dispose()
    {
        _inputButton.Pressed -= Open;
        _inputButton.MouseEntered -= OnMouseEntered;
        _inputButton.MouseExited -= OnMouseExited;
        NHotkeyManager.Instance?.RemoveHotkeyReleasedBinding(
            AllCardsHotkey.Action, OpenFromHotkey);
        if (GodotObject.IsInstanceValid(_visualButton))
            _visualButton.QueueFree();
    }

    private void Open()
    {
        var player = ResolvePlayer();
        if (player?.PlayerCombatState != null)
            AllCardsPileScreenCoordinator.Open(player);
    }

    /// <summary>
    /// The button ignores clicks while combat is not accepting them; the shortcut has
    /// to check for itself. A screen that is asking the player for something is left
    /// alone — the pile screens are the only ones worth switching away from.
    /// </summary>
    private void OpenFromHotkey()
    {
        if (!CombatManager.Instance.IsInProgress)
            return;
        if (NCapstoneContainer.Instance is { InUse: true } capstone &&
            capstone.CurrentCapstoneScreen is not NCardPileScreen)
            return;
        Open();
    }

    private void OnMouseEntered()
    {
        var tween = _visualButton.CreateTween();
        tween.TweenProperty(_visualButton, "scale", Vector2.One * 1.1f, 0.08);
        ShowHoverTip();
    }

    /// <summary>
    /// The tip sits directly above the button, measured from its own height rather than
    /// dropped at a fixed offset. The combat UI runs along the bottom edge, so a tip
    /// has to open upwards, and its height depends on how far the description wraps.
    ///
    /// The column is moved, not the set: CreateAndShow has already lifted the column to
    /// keep it on screen, and moving the set as well would apply that lift twice — which
    /// is what left this tip floating well above the button.
    ///
    /// The shortcut is named from its current binding rather than its default, so a
    /// rebound key is the one the tip reports.
    /// </summary>
    private void ShowHoverTip()
    {
        var key = NInputManager.Instance?.GetShortcutKey(AllCardsHotkey.Action) ??
                  Key.None;
        var shortcut = key == Key.None ? "unbound" : key.ToString();
        var tipSet = NHoverTipSet.CreateAndShow(
            _inputButton,
            NativeHoverTip.Create(
                $"{MainFile.ModName} / All Cards ({shortcut})",
                "Show view of all cards - draw pile, reshuffle, and cards-in-hand. " +
                "Select cards to calculate draw chance.",
                "Hypergeo:AllCardsButton"));
        if (tipSet?.GetNodeOrNull<Control>("textHoverTipContainer") is not { } column)
            return;
        var button = _visualButton.GetGlobalRect();
        column.GlobalPosition = new Vector2(
            button.Position.X, button.Position.Y - column.Size.Y - 12f);
    }

    private void OnMouseExited()
    {
        NHoverTipSet.Remove(_inputButton);
        var tween = _visualButton.CreateTween();
        tween.TweenProperty(_visualButton, "scale", Vector2.One, 0.18);
    }

    private static Player? ResolvePlayer()
    {
        var players = CombatManager.Instance.DebugOnlyGetState()?.Players;
        return players == null ? null : LocalPlayerResolver.Resolve(players);
    }
}
