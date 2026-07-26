using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Cards;
using MegaCrit.Sts2.Core.Nodes.Cards.Holders;
using MegaCrit.Sts2.Core.Nodes.Screens;
using MegaCrit.Sts2.addons.mega_text;

namespace Hypergeo.HypergeoCode;

internal sealed class DrawOddsView : IDisposable
{
    private readonly NCardPileScreen _screen;
    private readonly NCardGrid _grid;
    private readonly MegaRichTextLabel _bottomLabel;
    private readonly HashSet<CardIdentity> _selectedGroups = [];
    private int _naturalDrawCount;
    private int _drawCount;

    public DrawOddsView(NCardPileScreen screen)
    {
        _screen = screen;
        _grid = screen.GetNode<NCardGrid>("CardGrid");
        _bottomLabel = screen.GetNode<MegaRichTextLabel>("%BottomLabel");
    }

    public void Attach()
    {
        _bottomLabel.Visible = true;
        _grid.HolderPressed += OnHolderPressed;
        _grid.HolderAltPressed += OnHolderPressed;
        _screen.Pile.ContentsChanged += Refresh;
        Refresh();
    }

    public void Dispose()
    {
        _grid.HolderPressed -= OnHolderPressed;
        _grid.HolderAltPressed -= OnHolderPressed;
        _screen.Pile.ContentsChanged -= Refresh;
    }

    public bool TryShowHoverTips(NCardHolder holder)
    {
        if (!_grid.CurrentlyDisplayedCardHolders.Any(
                candidate => candidate.GetInstanceId() == holder.GetInstanceId()) ||
            holder.CardModel is not { } card)
            return false;

        var player = ResolvePlayer();
        if (player?.PlayerCombatState is not { } combatState)
            return false;
        DrawChanceHoverTip.Show(
            holder,
            card,
            combatState.DrawPile.Cards,
            combatState.DiscardPile.Cards,
            _naturalDrawCount);
        return true;
    }

    private void Refresh()
    {
        if (!Godot.GodotObject.IsInstanceValid(_screen))
            return;
        var cards = _screen.Pile.Cards;
        var availableGroups = cards.Select(CardIdentity.From).ToHashSet();
        _selectedGroups.RemoveWhere(group => !availableGroups.Contains(group));
        _naturalDrawCount = ResolveNaturalDrawCount();
        _drawCount = ResolveDrawsFromViewedPile(cards.Count, _naturalDrawCount);

        foreach (var holder in _grid.CurrentlyDisplayedCardHolders)
        {
            if (holder.CardModel is not { } card)
                continue;
            if (_selectedGroups.Contains(CardIdentity.From(card)))
                _grid.HighlightCard(card);
            else
                _grid.UnhighlightCard(card);
        }
        RenderSummary(cards.Count);
    }

    private void OnHolderPressed(NCardHolder holder)
    {
        if (holder.CardModel is not { } card)
            return;
        var identity = CardIdentity.From(card);
        if (!_selectedGroups.Add(identity))
            _selectedGroups.Remove(identity);
        Refresh();
    }

    private int ResolveNaturalDrawCount()
    {
        var state = CombatManager.Instance.DebugOnlyGetState();
        var player = state == null ? null : LocalPlayerResolver.Resolve(state.Players);
        if (state == null || player?.PlayerCombatState == null)
            return _screen.Pile.Type == PileType.Draw
                ? Math.Min(CombatManager.baseHandDrawCount, _screen.Pile.Cards.Count)
                : 0;
        if (!Hook.ShouldDraw(state, player, fromHandDraw: true, out _))
            return 0;

        var modified = Hook.ModifyHandDraw(
            state, player, CombatManager.baseHandDrawCount, out _);
        var retainedCards = Hook.ShouldFlush(state, player)
            ? player.PlayerCombatState.Hand.Cards.Count(card => card.ShouldRetainThisTurn)
            : player.PlayerCombatState.Hand.Cards.Count;
        var handSpace = Math.Max(0, CardPile.MaxCardsInHand - retainedCards);
        return Math.Min(handSpace, Math.Max(0, (int)modified));
    }

    private int ResolveDrawsFromViewedPile(int population, int naturalDrawCount)
    {
        if (_screen.Pile.Type == PileType.Draw)
            return Math.Min(population, naturalDrawCount);
        var drawPileCount = ResolvePlayer()?.PlayerCombatState?.DrawPile.Cards.Count ?? 0;
        return Math.Min(population, Math.Max(0, naturalDrawCount - drawPileCount));
    }

    private Player? ResolvePlayer()
    {
        var players = CombatManager.Instance.DebugOnlyGetState()?.Players;
        return players == null ? null : LocalPlayerResolver.Resolve(players);
    }

    private void RenderSummary(int population)
    {
        var selectedCopies = _screen.Pile.Cards.Count(card =>
            _selectedGroups.Contains(CardIdentity.From(card)));
        var probability = Hypergeometric.AtLeastOne(
            population, selectedCopies, _drawCount);
        _bottomLabel.Text = selectedCopies == 0
            ? "[center]Select one or more cards to calculate draw chance."
            : $"[center]Chance of drawing any of {selectedCopies} selected cards " +
              $"(out of {_naturalDrawCount}): {Hypergeometric.FormatPercent(probability)}";
    }
}
