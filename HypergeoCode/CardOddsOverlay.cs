using Godot;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Nodes.Cards.Holders;
using MegaCrit.Sts2.addons.mega_text;

namespace Hypergeo.HypergeoCode;

/// <summary>
/// Per-card draw chance printed onto the cards in the grid.
///
/// This reuses the Card Library's own on-card readout
/// (<c>res://scenes/screens/card_library/card_library_stats.tscn</c>), the widget the
/// game already shows over a grid card for its View Stats toggle. Its dark backing
/// rect is what keeps the number legible over card art.
///
/// Grid card holders come from a shared node pool, so every badge this creates must
/// be freed before the holders are recycled.
/// </summary>
internal sealed class CardOddsOverlay : IDisposable
{
    private const string StatsScene = "screens/card_library/card_library_stats";
    private const string BadgeName = "HypergeoOddsBadge";

    // Unscaled card pixels about the card's centre. From scenes/cards/card.tscn the
    // card runs -150..150 across and -211..211 down, and its art -125..125 and
    // -168..22, so the band sits inside the art, just below its top edge.
    private const float BandLeft = -120f;
    private const float BandRight = 120f;
    private const float BandCenterY = -88f;
    private const float BandMinHeight = 32f;
    private const float BandPadding = 4f;
    private const int CaptionFontSize = 20;

    private readonly Dictionary<ulong, Control> _badges = [];

    /// <summary>Whether the badges are drawn at all.</summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// Show a chance on a holder, creating its badge on demand. A caption names the
    /// query when the percentage answers something other than "this card on its own".
    /// </summary>
    public void Show(NCardHolder holder, string percent, string? caption = null)
    {
        if (!Enabled)
        {
            Hide(holder);
            return;
        }
        if (!GodotObject.IsInstanceValid(holder))
            return;

        var badge = Resolve(holder);
        if (badge == null)
            return;
        badge.Visible = true;

        var label = badge.GetNode<MegaRichTextLabel>("%Label");
        label.Text = caption == null
            ? $"[center]{percent}"
            : $"[center][font_size={CaptionFontSize}]{caption}[/font_size]\n{percent}";

        // Size the band to whatever the text actually measures rather than to a guess.
        // A caption and a percentage are set at different font sizes, so their combined
        // line heights are not something to hard-code, and a band that fits its content
        // leaves the text centred whichever way the label resolves its own alignment.
        var background = badge.GetNode<Control>("Bg");
        background.OffsetLeft = BandLeft;
        background.OffsetRight = BandRight;

        var height = Math.Max(
            BandMinHeight, label.GetContentHeight() + BandPadding);
        background.OffsetTop = BandCenterY - height * 0.5f;
        background.OffsetBottom = BandCenterY + height * 0.5f;
    }

    public void Hide(NCardHolder holder)
    {
        if (GodotObject.IsInstanceValid(holder) &&
            _badges.TryGetValue(holder.GetInstanceId(), out var badge) &&
            GodotObject.IsInstanceValid(badge))
            badge.Visible = false;
    }

    /// <summary>Free every badge. Call before the grid recycles its holders.</summary>
    public void Clear()
    {
        foreach (var badge in _badges.Values)
            if (GodotObject.IsInstanceValid(badge))
                badge.QueueFree();
        _badges.Clear();
    }

    public void Dispose() => Clear();

    private Control? Resolve(NCardHolder holder)
    {
        var id = holder.GetInstanceId();
        if (_badges.TryGetValue(id, out var existing) &&
            GodotObject.IsInstanceValid(existing) &&
            existing.GetParent() == holder)
            return existing;

        var badge = SceneHelper.Instantiate<Control>(StatsScene);
        badge.Name = BadgeName;
        badge.MouseFilter = Control.MouseFilterEnum.Ignore;
        // The library's readout auto-sizes its font to fill a block sized for several
        // stat lines. This band is short and sets its own sizes through bbcode, so the
        // caption would otherwise be rescaled out of proportion with the percentage.
        var label = badge.GetNode<MegaRichTextLabel>("%Label");
        label.AutoSizeEnabled = false;
        label.VerticalAlignment = VerticalAlignment.Center;

        holder.AddChild(badge);
        _badges[id] = badge;
        return badge;
    }
}
