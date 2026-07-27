using Godot;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Cards;
using MegaCrit.Sts2.Core.Nodes.Cards.Holders;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.Screens;
using MegaCrit.Sts2.Core.Nodes.Screens.CardLibrary;
using MegaCrit.Sts2.addons.mega_text;

namespace Hypergeo.HypergeoCode;

/// <summary>
/// The All Cards screen: every card the next hand could reach, in draw order, beside
/// a Card Library shelf that builds the query and prints the result.
/// </summary>
internal sealed class AllCardsPileScreenView : IDisposable
{
    private const float CardPadding = 40f;

    private const string ReshuffleMarkerText =
        "RESHUFFLE\nDiscard Pile + Cards in Hand\n→";

    private const string RetainedMarkerText =
        "RETAINED\nStays in Hand\nNot Reshuffled";

    private readonly NCardPileScreen _screen;
    private readonly Player _player;
    private readonly NCardGrid _grid;
    private readonly MegaRichTextLabel _bottomLabel;
    private readonly HashSet<CardModel> _selectedCards = [];
    private readonly Godot.Timer _refreshTimer;

    private readonly NativeShelf _shelf;
    private readonly CardOddsOverlay _overlay = new();
    private readonly List<Control> _markers = [];
    private readonly Dictionary<CardIdentity, string> _overlayText = [];

    private readonly Button _drawDecrease;
    private readonly Button _drawIncrease;
    private readonly Button _drawReset;
    private readonly MegaLabel _drawCountLabel;
    private readonly Button _anyButton;
    private readonly Button _allButton;
    private readonly HBoxContainer _targetRow;
    private readonly Button _targetDecrease;
    private readonly Button _targetIncrease;
    private readonly MegaLabel _targetCountLabel;
    private readonly NativeShelf.ShelfRow _selectedRow;
    private readonly NativeShelf.ShelfRow _needRow;
    private readonly NativeShelf.ShelfRow _heldRow;
    private readonly NativeShelf.ShelfRow _chanceRow;
    private readonly MegaLabel _hintLabel;

    private DrawPools _pools;
    private bool _anyMode = true;
    private int _chosenDrawCount;
    private int _targetHits = 1;
    private Node? _raisedContainer;
    private int _raisedContainerIndex = -1;

    public AllCardsPileScreenView(NCardPileScreen screen, Player player)
    {
        _screen = screen;
        _player = player;
        if (player.PlayerCombatState == null)
            throw new InvalidOperationException("All Cards requires active combat state.");
        _pools = DrawPools.Resolve(player);
        _chosenDrawCount = _pools.NaturalDrawCount;
        _grid = screen.GetNode<NCardGrid>("CardGrid");
        _bottomLabel = screen.GetNode<MegaRichTextLabel>("%BottomLabel");
        _refreshTimer = new Godot.Timer { WaitTime = 0.15, Autostart = true };
        _shelf = new NativeShelf();

        var draw = _shelf.AddModule(_shelf.Top, "DRAW");
        var drawRow = NativeShelf.CreateControlRow();
        var drawDecreaseControl = _shelf.CreateButton("−", 48);
        var drawCountControl = _shelf.CreateButton(
            string.Empty,
            52,
            "Restore the natural next-hand draw count after game effects.",
            "Natural Draw");
        var drawIncreaseControl = _shelf.CreateButton("+", 48);
        _drawDecrease = drawDecreaseControl.Input;
        _drawReset = drawCountControl.Input;
        _drawCountLabel = drawCountControl.Label;
        _drawIncrease = drawIncreaseControl.Input;
        drawRow.AddChild(drawDecreaseControl.Root);
        drawRow.AddChild(drawCountControl.Root);
        drawRow.AddChild(drawIncreaseControl.Root);
        draw.Body.AddChild(drawRow);
        _shelf.AddCaption(draw.Body, "cards next hand");

        var selection = _shelf.AddModule(_shelf.Top, "SELECTION");
        var modeRow = NativeShelf.CreateControlRow();
        var anyControl = _shelf.CreateButton(
            "ANY", 78, "Calculate the chance of drawing at least N selected cards.");
        var allControl = _shelf.CreateButton(
            "ALL", 78, "Calculate the chance of drawing every selected card.");
        _anyButton = anyControl.Input;
        _allButton = allControl.Input;
        modeRow.AddChild(anyControl.Root);
        modeRow.AddChild(allControl.Root);
        selection.Body.AddChild(modeRow);

        _targetRow = NativeShelf.CreateControlRow();
        var targetDecreaseControl = _shelf.CreateButton("−", 48);
        var targetCountControl = _shelf.CreateDisplay(string.Empty, 52);
        var targetIncreaseControl = _shelf.CreateButton("+", 48);
        _targetDecrease = targetDecreaseControl.Input;
        _targetCountLabel = targetCountControl.Label;
        _targetIncrease = targetIncreaseControl.Input;
        _targetRow.AddChild(targetDecreaseControl.Root);
        _targetRow.AddChild(targetCountControl.Root);
        _targetRow.AddChild(targetIncreaseControl.Root);
        selection.Body.AddChild(_targetRow);
        _hintLabel = _shelf.AddCaption(selection.Body, string.Empty);

        var result = _shelf.AddModule(_shelf.Top, "DRAW CHANCE");
        _selectedRow = _shelf.AddRow(result.Body, "Selected");
        _needRow = _shelf.AddRow(result.Body, "Need");
        _heldRow = _shelf.AddRow(result.Body, "Retained");
        _chanceRow = _shelf.AddRow(result.Body, "Chance");

        var overlayToggle = _shelf.AddToggle(_shelf.Bottom, "Show Odds on Cards", false);

        _anyButton.Pressed += () => SetAnyMode(true);
        _allButton.Pressed += () => SetAnyMode(false);
        _drawDecrease.Pressed += () => ChangeDrawCount(-1);
        _drawIncrease.Pressed += () => ChangeDrawCount(1);
        _drawReset.Pressed += ResetDrawCount;
        _targetDecrease.Pressed += () => ChangeTargetCount(-1);
        _targetIncrease.Pressed += () => ChangeTargetCount(1);
        overlayToggle.Toggled += OnOverlayToggled;
        _refreshTimer.Timeout += RefreshPresentation;
    }

    public void Attach()
    {
        _screen.Name = "NCardPileScreen-AllCards";
        RaiseAboveRunUi();
        // Behind the back button, exactly where the Card Library puts its sidebar.
        _screen.AddChild(_shelf.Root);
        _screen.MoveChild(_shelf.Root, _grid.GetIndex() + 1);
        _screen.AddChild(_refreshTimer);
        // The shelf owns the readout; the native strip would only repeat it.
        _bottomLabel.Visible = false;
        InsetGridForShelf();
        _grid.HolderPressed += OnHolderPressed;
        _grid.HolderAltPressed += OnHolderPressed;
        foreach (var pile in Piles())
            pile.ContentsChanged += Render;
        Render();
    }

    public void Dispose()
    {
        _grid.HolderPressed -= OnHolderPressed;
        _grid.HolderAltPressed -= OnHolderPressed;
        foreach (var pile in Piles())
            pile.ContentsChanged -= Render;
        _refreshTimer.Timeout -= RefreshPresentation;
        _overlay.Dispose();
        foreach (var marker in _markers)
            if (GodotObject.IsInstanceValid(marker))
                marker.QueueFree();
        _markers.Clear();
        _shelf.Dispose();
        RestoreRunUiOrder();
    }

    /// <summary>
    /// Draw over the run's top bar and relic inventory, the way the Card Library owns
    /// the whole screen.
    ///
    /// Both sit after the capstone container under GlobalUi, so they paint over every
    /// capstone screen and take its clicks — which is why the native pile screens
    /// inset their grid below the top bar instead of covering it. Ordering the
    /// container last lifts the screen above them for drawing and for input alike.
    /// Only one capstone screen can be open, so nothing else is affected, and the
    /// original order is restored when this screen closes.
    /// </summary>
    private void RaiseAboveRunUi()
    {
        if (_screen.GetParent() is not { } container ||
            container.GetParent() is not { } runUi)
            return;
        _raisedContainer = container;
        _raisedContainerIndex = container.GetIndex();
        runUi.MoveChild(container, runUi.GetChildCount() - 1);
    }

    private void RestoreRunUiOrder()
    {
        if (_raisedContainer == null ||
            !GodotObject.IsInstanceValid(_raisedContainer) ||
            _raisedContainerIndex < 0)
            return;
        if (_raisedContainer.GetParent() is { } runUi)
            runUi.MoveChild(_raisedContainer, _raisedContainerIndex);
        _raisedContainer = null;
    }

    private IEnumerable<CardPile> Piles()
    {
        var combatState = _player.PlayerCombatState!;
        yield return combatState.DrawPile;
        yield return combatState.DiscardPile;
        yield return combatState.Hand;
    }

    /// <summary>Give the shelf its column, matching the Card Library's own grid inset.</summary>
    private void InsetGridForShelf()
    {
        _grid.OffsetLeft = NativeShelf.Width;
        var scrollContainer = _grid.GetNode<Control>("%ScrollContainer");
        scrollContainer.OffsetLeft = 50f;
        scrollContainer.OffsetRight = -150f;
    }

    private void Render()
    {
        if (!GodotObject.IsInstanceValid(_screen))
            return;
        _pools = DrawPools.Resolve(_player);
        // The grid is about to recycle its pooled holders, taking any badge with it.
        _overlay.Clear();

        var sections = BuildSections();
        var allCards = sections.SelectMany(section => section.Cards).ToList();
        _selectedCards.RemoveWhere(card => !allCards.Contains(card));
        _grid.SetCards(
            allCards,
            PileType.Draw,
            new List<SortingOrders> { SortingOrders.Ascending });
        UpdateAnalysis();
    }

    /// <summary>
    /// The two stages the next hand draws from, in order, and then anything it cannot
    /// reach at all.
    ///
    /// The discard pile and the cards in hand are one section because they are one
    /// population: the end of the turn puts the hand in the discard, and the reshuffle
    /// returns them together, so the grid sorts them as a single run. Retained cards
    /// are pulled out into a trailing section — they are not reshuffled until they are
    /// played and leave the hand, so no upcoming draw can reach them.
    /// </summary>
    private List<GridSection> BuildSections()
    {
        var sections = new List<GridSection>
        {
            new(Sort(_pools.Draw), MarkerText: null),
        };
        if (_pools.Reshuffle.Count > 0)
            sections.Add(new(Sort(_pools.Reshuffle), ReshuffleMarkerText));
        if (_pools.Retained.Count > 0)
            sections.Add(new(Sort(_pools.Retained), RetainedMarkerText));
        return sections;
    }

    private void UpdateAnalysis()
    {
        var selectedTotal = _selectedCards.Count;
        var retainedSelected = _selectedCards.Count(_pools.Retained.Contains);
        _targetHits = selectedTotal == 0 ? 1 : Math.Clamp(_targetHits, 1, selectedTotal);
        var requiredHits = _anyMode ? _targetHits : selectedTotal;
        var chance = selectedTotal == 0
            ? 0
            : _pools.ChanceOfAtLeast(_selectedCards.Contains, _chosenDrawCount, requiredHits);

        _drawCountLabel.Text = _chosenDrawCount.ToString();
        _targetCountLabel.Text = (_anyMode ? _targetHits : selectedTotal).ToString();
        _targetRow.Visible = _anyMode;
        _hintLabel.Text = selectedTotal == 0
            ? "select cards in the grid"
            : _anyMode
                ? $"of {selectedTotal} selected"
                : $"all {selectedTotal} selected";

        _selectedRow.Value.Text = selectedTotal.ToString();
        _needRow.Value.Text = selectedTotal == 0 ? "—" : requiredHits.ToString();
        _heldRow.Root.Visible = retainedSelected > 0;
        _heldRow.Value.Text = retainedSelected.ToString();
        _chanceRow.Value.Text = selectedTotal == 0
            ? "—"
            : Hypergeometric.FormatPercent(chance);

        NativeShelf.SetButtonState(_anyButton, enabled: !_anyMode, highlighted: _anyMode);
        NativeShelf.SetButtonState(_allButton, enabled: _anyMode, highlighted: !_anyMode);
        NativeShelf.SetButtonState(_drawDecrease, enabled: _chosenDrawCount > 0);
        NativeShelf.SetButtonState(
            _drawIncrease, enabled: _chosenDrawCount < _pools.ReachableCount);
        NativeShelf.SetButtonState(_drawReset, enabled: true);
        NativeShelf.SetButtonState(
            _targetDecrease,
            enabled: _anyMode && selectedTotal > 0 && _targetHits > 1);
        NativeShelf.SetButtonState(
            _targetIncrease,
            enabled: _anyMode && selectedTotal > 0 && _targetHits < selectedTotal);

        RebuildOverlayText();
        RefreshPresentation();
    }

    /// <summary>
    /// One any-copy chance per distinct card, so the per-card badges agree with the
    /// headline row of the native Draw Chance hover tip.
    /// </summary>
    private void RebuildOverlayText()
    {
        _overlayText.Clear();
        if (!_overlay.Enabled)
            return;
        foreach (var identity in _pools.Draw
                     .Concat(_pools.Discard)
                     .Concat(_pools.Hand)
                     .Select(CardIdentity.From)
                     .Distinct())
        {
            var chance = _pools.ChanceOfAny(
                card => CardIdentity.From(card) == identity, _chosenDrawCount);
            _overlayText[identity] = Hypergeometric.FormatPercent(chance);
        }
    }

    private void RefreshPresentation()
    {
        if (!GodotObject.IsInstanceValid(_screen))
            return;
        foreach (var holder in _grid.CurrentlyDisplayedCardHolders)
        {
            if (holder.CardModel is not { } card)
                continue;
            if (_selectedCards.Contains(card))
                _grid.HighlightCard(card);
            else
                _grid.UnhighlightCard(card);
            if (_overlay.Enabled &&
                _overlayText.TryGetValue(CardIdentity.From(card), out var text))
                _overlay.Show(holder, text);
            else
                _overlay.Hide(holder);
        }
        UpdateSectionLayout(BuildSections());
    }

    /// <summary>
    /// Reflow the grid so each section starts after a card-sized marker slot, and
    /// place the markers in those slots.
    /// </summary>
    private void UpdateSectionLayout(IReadOnlyList<GridSection> sections)
    {
        var scrollContainer = _grid.GetNode<Control>("%ScrollContainer");
        var cardSize = NCard.defaultSize * NCardHolder.smallScale;
        var columns = Math.Max(
            1,
            (int)((scrollContainer.Size.X + CardPadding) / (cardSize.X + CardPadding)));
        var containedWidth = columns * cardSize.X + (columns - 1) * CardPadding;
        var origin = new Vector2(
                         (scrollContainer.Size.X - containedWidth) * 0.5f,
                         _grid.YOffset + 80) +
                     cardSize * 0.5f;

        Vector2 SlotPosition(int index) =>
            origin + new Vector2(
                index % columns * (cardSize.X + CardPadding),
                index / columns * (cardSize.Y + CardPadding));

        // A null slot is a section marker; everything else is a card in draw order.
        var slots = new List<CardModel?>();
        var markerSlots = new List<(int Index, string Text)>();
        foreach (var section in sections)
        {
            if (section.MarkerText != null)
            {
                markerSlots.Add((slots.Count, section.MarkerText));
                slots.Add(null);
            }
            slots.AddRange(section.Cards);
        }

        foreach (var holder in _grid.CurrentlyDisplayedCardHolders)
        {
            if (holder.CardModel is not { } card)
                continue;
            var slot = IndexOfCard(slots, card);
            if (slot < 0)
                continue;
            holder.Position = SlotPosition(slot);
        }

        for (var index = 0; index < markerSlots.Count; index++)
        {
            var marker = ResolveMarker(index, scrollContainer, cardSize);
            marker.GetNode<MegaLabel>("MarkerLabel").Text = markerSlots[index].Text;
            marker.Visible = true;
            marker.CustomMinimumSize = cardSize;
            marker.Size = cardSize;
            marker.Position = SlotPosition(markerSlots[index].Index) - cardSize * 0.5f;
        }
        for (var index = markerSlots.Count; index < _markers.Count; index++)
            _markers[index].Visible = false;

        var rows = (int)Math.Ceiling(slots.Count / (double)columns);
        var containedHeight =
            rows * cardSize.Y + Math.Max(0, rows - 1) * CardPadding;
        var requiredHeight = containedHeight + 400 + _grid.YOffset;
        if (scrollContainer.Size.Y < requiredHeight)
            scrollContainer.Size = new Vector2(
                scrollContainer.Size.X, requiredHeight);
    }

    private Control ResolveMarker(int index, Control parent, Vector2 cardSize)
    {
        if (index < _markers.Count && GodotObject.IsInstanceValid(_markers[index]))
            return _markers[index];

        // Leave both z indices at 0. The game's hover tips sit at an absolute
        // z_index of 0, so any positive value here paints the marker over every
        // tooltip. At 0 the marker sorts by tree order: below the card holders,
        // which is right, since its own slot is always empty.
        var root = new Control
        {
            Name = $"HypergeoSectionMarker{index}",
            CustomMinimumSize = cardSize,
            Size = cardSize,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        var label = _shelf.CreateText(string.Empty, 17);
        label.Name = "MarkerLabel";
        label.Size = cardSize;
        label.HorizontalAlignment = HorizontalAlignment.Center;
        label.VerticalAlignment = VerticalAlignment.Center;
        root.AddChild(label);
        parent.AddChild(root);

        if (index < _markers.Count)
            _markers[index] = root;
        else
            _markers.Add(root);
        return root;
    }

    private static int IndexOfCard(IReadOnlyList<CardModel?> cards, CardModel target)
    {
        for (var index = 0; index < cards.Count; index++)
            if (ReferenceEquals(cards[index], target))
                return index;
        return -1;
    }

    private void OnHolderPressed(NCardHolder holder)
    {
        if (holder.CardModel is not { } card)
            return;
        if (!_selectedCards.Add(card))
            _selectedCards.Remove(card);
        UpdateAnalysis();
    }

    public bool TryShowHoverTips(NCardHolder holder)
    {
        if (!_grid.CurrentlyDisplayedCardHolders.Any(
                candidate => candidate.GetInstanceId() == holder.GetInstanceId()) ||
            holder.CardModel is not { } card)
            return false;

        DrawChanceHoverTip.Show(holder, card, _pools, _chosenDrawCount);
        return true;
    }

    private void OnOverlayToggled(NTickbox tickbox)
    {
        _overlay.Enabled = tickbox.IsTicked;
        if (!_overlay.Enabled)
            _overlay.Clear();
        UpdateAnalysis();
    }

    private void SetAnyMode(bool anyMode)
    {
        _anyMode = anyMode;
        UpdateAnalysis();
    }

    private void ChangeDrawCount(int delta)
    {
        _chosenDrawCount = Math.Clamp(
            _chosenDrawCount + delta, 0, _pools.ReachableCount);
        UpdateAnalysis();
    }

    private void ChangeTargetCount(int delta)
    {
        if (!_anyMode || _selectedCards.Count == 0)
            return;
        _targetHits = Math.Clamp(_targetHits + delta, 1, _selectedCards.Count);
        UpdateAnalysis();
    }

    private void ResetDrawCount()
    {
        _pools = DrawPools.Resolve(_player);
        _chosenDrawCount = _pools.NaturalDrawCount;
        UpdateAnalysis();
    }

    private static List<CardModel> Sort(IEnumerable<CardModel> cards) =>
        cards.OrderBy(card => card.Rarity)
            .ThenBy(card => card.Id.Entry, StringComparer.Ordinal)
            .ToList();

    /// <summary>A run of cards in the grid, optionally opened by a marker slot.</summary>
    private sealed record GridSection(
        IReadOnlyList<CardModel> Cards, string? MarkerText);
}
