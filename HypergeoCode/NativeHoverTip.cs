using Godot;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
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

    public static void FormatLatestTable(
        NHoverTipSet tipSet,
        IReadOnlyList<(string Label, string Value)> rows)
    {
        var container = tipSet.GetNode<Control>("textHoverTipContainer");
        if (container.GetChildCount() == 0)
            return;

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

        var contentHeight = description.GetContentHeight();
        description.CustomMinimumSize = new Vector2(320f, contentHeight);
        description.Size = new Vector2(320f, contentHeight);
        tipPanel.ResetSize();
        if (container is Container flow)
            flow.QueueSort();
    }
}
