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
        DrawPools pools,
        int cardsDrawn)
    {
        var identity = CardIdentity.From(card);
        var inHand = pools.Hand.Contains(card);
        var isRetained = inHand && pools.IsRetained(card);
        var anyChance = pools.ChanceOfAny(
            candidate => CardIdentity.From(candidate) == identity, cardsDrawn);
        var thisChance = pools.ChanceOfAny(
            candidate => ReferenceEquals(candidate, card), cardsDrawn);
        var anyPercent = Hypergeometric.FormatPercent(anyChance);
        var thisPercent = Hypergeometric.FormatPercent(thisChance);
        var rows = new List<(string Label, string Value)>
        {
            ($"Any {card.Title}:", anyPercent),
        };
        if (!string.Equals(anyPercent, thisPercent, StringComparison.Ordinal))
            rows.Add(($"This {card.Title}:", thisPercent));

        var location = pools.Discard.Contains(card)
            ? " (Discard Pile)"
            : isRetained
                ? " (Retained)"
                : inHand
                    ? " (In Hand)"
                    : string.Empty;
        var description = string.Join(
            "\n", rows.Select(row => $"{row.Label} {row.Value}"));
        if (isRetained)
            description += "\nRetained cards stay in hand and are never drawn.";
        var analysisTip = NativeHoverTip.Create(
            $"Draw Chance{location}",
            description,
            $"DrawOdds:{card.Id.Entry}:{card.GetHashCode()}");

        var tipSet = NHoverTipSet.CreateAndShow(
            holder,
            card.HoverTips.Concat<IHoverTip>([analysisTip]));
        if (tipSet is null)
            return;

        var tipPanel = NativeHoverTip.FormatLatestTable(tipSet, rows);
        tipSet.SetAlignmentForCardHolder(holder);
        // The table's real height is only knowable after a layout pass, and the tips
        // stack in a flow container that wraps into columns, so both the panel's height
        // and the set's placement are settled once the engine has measured them.
        NativeHoverTip.SettleTable(tipSet, tipPanel, holder);
    }
}
