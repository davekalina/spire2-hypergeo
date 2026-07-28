using Godot;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Nodes.Cards.Holders;
using MegaCrit.Sts2.Core.Nodes.HoverTips;
using MegaCrit.Sts2.addons.mega_text;

namespace Hypergeo.HypergeoCode;

internal static class NativeHoverTip
{
    private static readonly System.Reflection.PropertyInfo TitleProperty =
        typeof(HoverTip).GetProperty(nameof(HoverTip.Title)) ??
        throw new MissingMemberException(typeof(HoverTip).FullName, nameof(HoverTip.Title));
    private static readonly System.Reflection.PropertyInfo DescriptionProperty =
        typeof(HoverTip).GetProperty(nameof(HoverTip.Description)) ??
        throw new MissingMemberException(
            typeof(HoverTip).FullName, nameof(HoverTip.Description));

    public static HoverTip Create(string title, string description, string id)
    {
        object boxedTip = new HoverTip(
            new LocString("gameplay_ui", "DRAW_PILE_INFO"));
        TitleProperty.SetValue(boxedTip, title);
        DescriptionProperty.SetValue(boxedTip, description);
        var tip = (HoverTip)boxedTip;
        tip.Id = id;
        return tip;
    }

    /// <summary>
    /// Lay the tip's rows out as a table, and return the panel it was written into so
    /// the caller can settle its height once the table has actually measured itself.
    /// </summary>
    public static Control? FormatLatestTable(
        NHoverTipSet tipSet,
        IReadOnlyList<(string Label, string Value)> rows)
    {
        var container = tipSet.GetNode<Control>("textHoverTipContainer");
        if (container.GetChildCount() == 0)
            return null;

        var tipPanel = container.GetChild<Control>(container.GetChildCount() - 1);
        var description = tipPanel.GetNode<MegaRichTextLabel>("%Description");

        // Native text hover tips are 360 px wide with a 320 px content area.
        // Build the table through RichTextLabel's API so its percentage column is
        // guaranteed to occupy the panel's right edge instead of shrinking beside
        // the row label.
        description.FitContent = false;
        description.CustomMinimumSize = new Vector2(320f, 0f);
        description.Size = new Vector2(320f, Math.Max(1f, description.Size.Y));
        description.Clear();
        description.PushTable(2);
        description.SetTableColumnExpand(0, true, 3, false);
        description.SetTableColumnExpand(1, true, 1, false);
        foreach (var row in rows)
        {
            description.PushCell();
            description.AddText(row.Label);
            description.Pop();
            description.PushCell();
            description.PushParagraph(HorizontalAlignment.Right);
            description.AddText(row.Value);
            description.Pop();
            description.Pop();
        }
        description.Pop();

        Fit(container, tipPanel, description);
        return tipPanel;
    }

    /// <summary>
    /// Re-measure the table once the engine has laid it out, then place the set again.
    ///
    /// A RichTextLabel does not know how tall a table is until it has been through a
    /// layout pass, so the height taken immediately after building one is a guess. The
    /// guess is usually too generous, and the panel then reserves room it does not
    /// draw — which reads as a gap between this tip and whatever the tip container
    /// stacks beneath it. Nothing shows it until a second tip lands below, so it only
    /// appears when several are on screen at once.
    ///
    /// Three passes: build, then measure honestly, then position the settled set. Each
    /// waits for the one before, because the container has to re-sort before its size
    /// means anything and the placement is read from that size.
    /// </summary>
    public static void SettleTable(
        NHoverTipSet tipSet, Control? tipPanel, NCardHolder holder)
    {
        if (tipPanel == null)
            return;
        Callable.From(() =>
        {
            if (!GodotObject.IsInstanceValid(tipSet) ||
                !GodotObject.IsInstanceValid(tipPanel) ||
                tipPanel.GetParent() is not Control container ||
                tipPanel.GetNodeOrNull<MegaRichTextLabel>("%Description") is not
                    { } description)
                return;
            Fit(container, tipPanel, description);
            Callable.From(() =>
            {
                if (GodotObject.IsInstanceValid(tipSet) &&
                    GodotObject.IsInstanceValid(holder))
                    tipSet.SetAlignmentForCardHolder(holder);
            }).CallDeferred();
        }).CallDeferred();
    }

    private static void Fit(
        Control container, Control tipPanel, MegaRichTextLabel description)
    {
        var contentHeight = description.GetContentHeight();
        if (contentHeight <= 0)
            return;
        description.CustomMinimumSize = new Vector2(320f, contentHeight);
        description.Size = new Vector2(320f, contentHeight);
        tipPanel.ResetSize();
        if (container is Container flow)
            flow.QueueSort();
    }
}
