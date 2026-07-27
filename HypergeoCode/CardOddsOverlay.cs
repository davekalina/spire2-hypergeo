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

    private readonly Dictionary<ulong, Control> _badges = [];

    /// <summary>Whether the badges are drawn at all.</summary>
    public bool Enabled { get; set; }

    /// <summary>Show <paramref name="text" /> on a holder, creating its badge on demand.</summary>
    public void Show(NCardHolder holder, string text)
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
        badge.GetNode<MegaRichTextLabel>("%Label").Text = $"[center]{text}";
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

        // The library's readout is a tall block sized for several stat lines. One
        // percentage needs a single band, so shorten it and leave the card art visible.
        var background = badge.GetNode<Control>("Bg");
        background.OffsetTop = -138f;
        background.OffsetBottom = -78f;

        holder.AddChild(badge);
        _badges[id] = badge;
        return badge;
    }
}
