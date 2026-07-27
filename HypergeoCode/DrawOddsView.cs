using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Cards;
using MegaCrit.Sts2.Core.Nodes.Cards.Holders;
using MegaCrit.Sts2.Core.Nodes.Screens;
using MegaCrit.Sts2.addons.mega_text;

namespace Hypergeo.HypergeoCode;

/// <summary>
/// Draw-chance analysis on the game's own Draw Pile and Discard Pile screens.
/// Selections group identical copies wherever they sit, and the odds come from the
/// same <see cref="DrawPools" /> the All Cards screen uses.
/// </summary>
internal sealed class DrawOddsView : IDisposable
{
    private readonly NCardPileScreen _screen;
    private readonly NCardGrid _grid;
    private readonly MegaRichTextLabel _bottomLabel;
    private readonly HashSet<CardIdentity> _selectedGroups = [];
    private DrawPools? _pools;

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

        var pools = _pools ?? DrawPools.TryResolveForLocalPlayer();
        if (pools == null)
            return false;
        DrawChanceHoverTip.Show(holder, card, pools, pools.NaturalDrawCount);
        return true;
    }

    private void Refresh()
    {
        if (!Godot.GodotObject.IsInstanceValid(_screen))
            return;
        _pools = DrawPools.TryResolveForLocalPlayer();
        var cards = _screen.Pile.Cards;
        var availableGroups = cards.Select(CardIdentity.From).ToHashSet();
        _selectedGroups.RemoveWhere(group => !availableGroups.Contains(group));

        foreach (var holder in _grid.CurrentlyDisplayedCardHolders)
        {
            if (holder.CardModel is not { } card)
                continue;
            if (_selectedGroups.Contains(CardIdentity.From(card)))
                _grid.HighlightCard(card);
            else
                _grid.UnhighlightCard(card);
        }
        RenderSummary();
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

    private void RenderSummary()
    {
        if (_pools is not { } pools)
        {
            _bottomLabel.Text =
                "[center]Select one or more cards to calculate draw chance.";
            return;
        }
        if (_selectedGroups.Count == 0)
        {
            _bottomLabel.Text =
                "[center]Select one or more cards to calculate draw chance.";
            return;
        }
        bool IsSelected(CardModel card) =>
            _selectedGroups.Contains(CardIdentity.From(card));
        // Count every reachable copy, not just the ones on this screen, so the
        // sentence and the probability describe the same set of cards.
        var selectedCopies =
            pools.Draw.Count(IsSelected) + pools.Reshuffle.Count(IsSelected);
        var probability = pools.ChanceOfAny(IsSelected, pools.NaturalDrawCount);
        _bottomLabel.Text =
            $"[center]Chance of drawing any of {selectedCopies} selected cards " +
            $"(out of {pools.NaturalDrawCount}): {Hypergeometric.FormatPercent(probability)}";
    }
}
