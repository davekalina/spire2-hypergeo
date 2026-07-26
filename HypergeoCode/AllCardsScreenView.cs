using Godot;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Nodes.Combat;
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
        count.SetTextAutoSize("ALL");
        count.PivotOffset = count.Size * 0.5f;
    }

    public void Dispose()
    {
        _inputButton.Pressed -= Open;
        _inputButton.MouseEntered -= OnMouseEntered;
        _inputButton.MouseExited -= OnMouseExited;
        if (GodotObject.IsInstanceValid(_visualButton))
            _visualButton.QueueFree();
    }

    private void Open()
    {
        var player = ResolvePlayer();
        if (player?.PlayerCombatState != null)
            AllCardsPileScreenCoordinator.Open(player);
    }

    private void OnMouseEntered()
    {
        var tween = _visualButton.CreateTween();
        tween.TweenProperty(_visualButton, "scale", Vector2.One * 1.1f, 0.08);
    }

    private void OnMouseExited()
    {
        var tween = _visualButton.CreateTween();
        tween.TweenProperty(_visualButton, "scale", Vector2.One, 0.18);
    }

    private static Player? ResolvePlayer()
    {
        var players = CombatManager.Instance.DebugOnlyGetState()?.Players;
        return players == null ? null : LocalPlayerResolver.Resolve(players);
    }
}
