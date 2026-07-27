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
        "Reshuffle\nDiscard Pile + Cards in Hand\n→";

    private const string RetainedMarkerText =
        "Retained\nStays in Hand\nNot Reshuffled";

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
    private readonly Button _targetDecrease;
    private readonly Button _targetIncrease;
    private readonly MegaLabel _targetCountLabel;
    private readonly NativeShelf.ShelfRow _needRow;
    private readonly NativeShelf.ShelfRow _heldRow;
    private readonly NativeShelf.ShelfRow _chanceRow;
    private readonly MegaLabel _hintLabel;
    private readonly MegaLabel _drawNote;
    private readonly MegaLabel _queryNote;

    private DrawPools _pools;
    private int _chosenDrawCount;
    private int _targetHits = 1;
    private Node? _raisedContainer;
    private int _raisedContainerIndex = -1;
    private string? _selectionPercent;
    private string? _selectionCaption;

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

        var draw = _shelf.AddModule(_shelf.Top, "Draw");
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
        _drawNote = _shelf.AddNote(draw.Body, string.Empty, 13);

        var selection = _shelf.AddModule(_shelf.Top, "Selection");
        var targetRow = NativeShelf.CreateControlRow();
        var targetDecreaseControl = _shelf.CreateButton("−", 48);
        var targetCountControl = _shelf.CreateDisplay(string.Empty, 52);
        var targetIncreaseControl = _shelf.CreateButton("+", 48);
        _targetDecrease = targetDecreaseControl.Input;
        _targetCountLabel = targetCountControl.Label;
        _targetIncrease = targetIncreaseControl.Input;
        targetRow.AddChild(targetDecreaseControl.Root);
        targetRow.AddChild(targetCountControl.Root);
        targetRow.AddChild(targetIncreaseControl.Root);
        selection.Body.AddChild(targetRow);
        _hintLabel = _shelf.AddCaption(selection.Body, string.Empty);

        var result = _shelf.AddModule(_shelf.Top, "Draw Chance");
        _queryNote = _shelf.AddNote(result.Body, string.Empty);
        _needRow = _shelf.AddRow(result.Body, "Need");
        _heldRow = _shelf.AddRow(result.Body, "Retained");
        _chanceRow = _shelf.AddRow(result.Body, "Chance");

        var overlayToggle = _shelf.AddToggle(_shelf.Bottom, "Show Odds on Cards", false);
        AddAboutRow();

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
    /// The mod's own footer, in the corner the Card Library leaves for its settings.
    /// </summary>
    private void AddAboutRow()
    {
        var row = NativeShelf.CreateFullWidthRow(4);
        var label = _shelf.CreateText($"{MainFile.ModName}\n{MainFile.Version}", 13);
        label.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        label.HorizontalAlignment = HorizontalAlignment.Left;
        label.VerticalAlignment = VerticalAlignment.Center;
        label.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        label.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
        row.AddChild(label);

        var help = _shelf.CreateButton("?", 36, HelpText, MainFile.ModName);
        help.Root.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
        row.AddChild(help.Root);
        _shelf.Bottom.AddChild(row);
    }

    private static string HelpText =>
        "Odds that next turn's hand contains the cards you pick.\n\n" +
        "Click cards to select them, then set how many of them you need. One is " +
        "'any of these'; all of them is 'every one of these'.\n\n" +
        "The draw pile is drawn first. Your discard pile and hand return together " +
        "on the reshuffle, so they share one section. Retained cards never leave " +
        "your hand and cannot be drawn.\n\n" +
        "The draw count is what next turn will deal, after relics, powers, retain, " +
        "and hand size. Use − and + to ask about a different number; click the " +
        "count to restore the real one.";

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

    /// <summary>
    /// Lay the grid out exactly as the Card Library lays out its own: inset from the
    /// left by the width of the shelf, and otherwise the full viewport.
    ///
    /// NCardPileScreen._Ready calls InsetForTopBar, which drops the grid 80 px so it
    /// clears the run's top bar. This screen covers that bar, so the inset only served
    /// to strand the grid's top fade in open space partway down the screen. At full
    /// height the fade sits on the screen edge, which is what it is drawn for.
    /// </summary>
    private void InsetGridForShelf()
    {
        _grid.OffsetLeft = NativeShelf.Width;
        _grid.OffsetTop = 0f;
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
        var requiredHits = selectedTotal == 0
            ? 1
            : Math.Clamp(_targetHits, 1, selectedTotal);
        _targetHits = requiredHits;
        var chance = selectedTotal == 0
            ? 0
            : _pools.ChanceOfAtLeast(_selectedCards.Contains, _chosenDrawCount, requiredHits);

        _drawCountLabel.Text = _chosenDrawCount.ToString();
        _targetCountLabel.Text = requiredHits.ToString();
        _hintLabel.Text = selectedTotal == 0
            ? "select cards in the grid"
            : $"of {selectedTotal} selected";

        _drawNote.Text = DescribeDrawCount();
        _queryNote.Text = DescribeQuery(selectedTotal, requiredHits);
        _needRow.Root.Visible = selectedTotal > 0;
        _needRow.Value.Text = $"{requiredHits} of {selectedTotal}";
        _heldRow.Root.Visible = retainedSelected > 0;
        _heldRow.Value.Text = retainedSelected.ToString();
        _chanceRow.Value.Text = selectedTotal == 0
            ? "—"
            : Hypergeometric.FormatPercent(chance);

        NativeShelf.SetButtonState(_drawDecrease, enabled: _chosenDrawCount > 0);
        NativeShelf.SetButtonState(
            _drawIncrease, enabled: _chosenDrawCount < _pools.ReachableCount);
        NativeShelf.SetButtonState(_drawReset, enabled: true);
        NativeShelf.SetButtonState(
            _targetDecrease, enabled: selectedTotal > 0 && requiredHits > 1);
        NativeShelf.SetButtonState(
            _targetIncrease, enabled: selectedTotal > 0 && requiredHits < selectedTotal);

        RebuildOverlayText(selectedTotal, requiredHits, chance);
        RefreshPresentation();
    }

    /// <summary>
    /// Name the effects behind a draw count that is not the base of five, so the number
    /// can be checked rather than trusted. While the count is overridden by hand the
    /// modifiers no longer describe it, so the real value is offered instead.
    /// </summary>
    private string DescribeDrawCount()
    {
        if (_chosenDrawCount != _pools.NaturalDrawCount)
            return $"set by hand — next turn deals {_pools.NaturalDrawCount}";
        return _pools.DrawModifiers.Count == 0
            ? string.Empty
            : string.Join(", ", _pools.DrawModifiers);
    }

    /// <summary>
    /// State the question the percentage answers, naming the cards that were picked.
    /// </summary>
    private string DescribeQuery(int selectedTotal, int requiredHits)
    {
        if (selectedTotal == 0)
            return "Select cards in the grid.";

        const int maxNames = 3;
        var names = _selectedCards
            .OrderBy(card => card.Rarity)
            .ThenBy(card => card.Title, StringComparer.CurrentCulture)
            .Select(card => card.Title)
            .Distinct()
            .ToList();
        var shown = names.Take(maxNames).ToList();
        var wantsEveryOne = requiredHits == selectedTotal;
        var joiner = wantsEveryOne ? " and " : " or ";
        var listed = shown.Count == 1
            ? shown[0]
            : string.Join(", ", shown.Take(shown.Count - 1)) + joiner + shown[^1];
        if (names.Count > shown.Count)
            listed += $" +{names.Count - shown.Count} more";

        return requiredHits > 1 && !wantsEveryOne
            ? $"Chance to draw {requiredHits} of {listed}:"
            : $"Chance to draw {listed}:";
    }

    /// <summary>
    /// What the on-card badges say.
    ///
    /// With nothing selected, each card carries its own any-copy chance, matching the
    /// headline row of the native Draw Chance hover tip. Once cards are selected the
    /// question has changed: the grid stops answering per card and marks only the
    /// selection, every badge carrying the one joint chance the shelf reports, captioned
    /// with the query so the number is not mistaken for that card's own odds.
    /// </summary>
    private void RebuildOverlayText(int selectedTotal, int requiredHits, double chance)
    {
        _overlayText.Clear();
        _selectionPercent = null;
        _selectionCaption = null;
        if (!_overlay.Enabled)
            return;

        if (selectedTotal > 0)
        {
            _selectionPercent = Hypergeometric.FormatPercent(chance);
            _selectionCaption = selectedTotal == 1
                ? "This card"
                : requiredHits == 1
                    ? $"Any of {selectedTotal}"
                    : requiredHits == selectedTotal
                        ? $"All {selectedTotal}"
                        : $"{requiredHits} of {selectedTotal}";
            return;
        }

        foreach (var identity in _pools.Draw
                     .Concat(_pools.Discard)
                     .Concat(_pools.Hand)
                     .Select(CardIdentity.From)
                     .Distinct())
        {
            var anyCopy = _pools.ChanceOfAny(
                card => CardIdentity.From(card) == identity, _chosenDrawCount);
            _overlayText[identity] = Hypergeometric.FormatPercent(anyCopy);
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
            var isSelected = _selectedCards.Contains(card);
            if (isSelected)
                _grid.HighlightCard(card);
            else
                _grid.UnhighlightCard(card);

            if (_selectionPercent is { } selectionPercent)
                if (isSelected)
                    _overlay.Show(holder, selectionPercent, _selectionCaption);
                else
                    _overlay.Hide(holder);
            else if (_overlayText.TryGetValue(CardIdentity.From(card), out var anyCopy))
                _overlay.Show(holder, anyCopy);
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

    private void ChangeDrawCount(int delta)
    {
        _chosenDrawCount = Math.Clamp(
            _chosenDrawCount + delta, 0, _pools.ReachableCount);
        UpdateAnalysis();
    }

    private void ChangeTargetCount(int delta)
    {
        if (_selectedCards.Count == 0)
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
