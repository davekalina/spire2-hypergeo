using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Cards.Holders;
using MegaCrit.Sts2.Core.Nodes.HoverTips;

namespace Hypergeo.HypergeoCode;

internal static class DrawChanceHoverTip
{
    public static void Show(
        NCardHolder holder,
        CardModel card,
        IReadOnlyList<CardModel> drawCards,
        IReadOnlyList<CardModel> discardCards,
        int cardsDrawn)
    {
        var inDiscardPile = discardCards.Contains(card);
        var identity = CardIdentity.From(card);
        var matchingDrawCards = drawCards.Count(
            candidate => CardIdentity.From(candidate) == identity);
        var matchingDiscardCards = discardCards.Count(
            candidate => CardIdentity.From(candidate) == identity);
        var anyChance = DrawOddsCalculator.AtLeastOneAcrossPiles(
            drawCards.Count,
            matchingDrawCards,
            discardCards.Count,
            matchingDiscardCards,
            cardsDrawn);
        var thisChance = DrawOddsCalculator.AtLeastOneAcrossPiles(
            drawCards.Count,
            drawCards.Contains(card) ? 1 : 0,
            discardCards.Count,
            inDiscardPile ? 1 : 0,
            cardsDrawn);
        var anyPercent = Hypergeometric.FormatPercent(anyChance);
        var thisPercent = Hypergeometric.FormatPercent(thisChance);
        var rows = new List<(string Label, string Value)>
        {
            ($"Any {card.Title}:", anyPercent),
        };
        if (!string.Equals(anyPercent, thisPercent, StringComparison.Ordinal))
            rows.Add(($"This {card.Title}:", thisPercent));
        var analysisTip = NativeHoverTip.Create(
            $"Draw Chance{(inDiscardPile ? " (Discard Pile)" : "")}",
            string.Join(
                "\n",
                rows.Select(row => $"{row.Label} {row.Value}")),
            $"DrawOdds:{card.Id.Entry}:{card.GetHashCode()}");

        var tipSet = NHoverTipSet.CreateAndShow(
            holder,
            card.HoverTips.Concat<IHoverTip>([analysisTip]));
        if (tipSet is null)
            return;

        NativeHoverTip.FormatLatestTable(tipSet, rows);
        tipSet.SetAlignmentForCardHolder(holder);
        // Expanding the analysis table can cause the native VFlowContainer to
        // create another tooltip column after its first placement pass. Align
        // once more after layout so the completed set stays inside the viewport.
        tipSet.CallDeferred(
            NHoverTipSet.MethodName.SetAlignmentForCardHolder,
            holder);
    }

}
