using Godot;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Helpers;
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
    private readonly HashSet<CardModel> _selectedCards = AllCardsSession.SelectedCards;
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
    private readonly NSearchBar _searchBar;
    private readonly List<Control> _combatModules;
    private List<Control> _calculatorModules = [];
    private NativeShelf.ShelfStepper _population = null!;
    private NativeShelf.ShelfStepper _sample = null!;
    private NativeShelf.ShelfStepper _successes = null!;
    private NativeShelf.ShelfStepper _wanted = null!;
    private NativeShelf.ShelfRow _exactlyRow = null!;
    private NativeShelf.ShelfRow _atLeastRow = null!;
    private NativeShelf.ShelfRow _atMostRow = null!;
    private NativeShelf.ShelfRow _expectedRow = null!;

    private DrawPools _pools;
    private int _chosenDrawCount;
    private Node? _raisedContainer;
    private int _raisedContainerIndex = -1;
    private string? _selectionPercent;
    private string? _selectionCaption;

    /// <summary>How many of the selection the hand needs. Outlives the screen.</summary>
    private int TargetHits
    {
        get => AllCardsSession.TargetHits;
        set => AllCardsSession.TargetHits = value;
    }

    public AllCardsPileScreenView(NCardPileScreen screen, Player player)
    {
        _screen = screen;
        _player = player;
        if (player.PlayerCombatState == null)
            throw new InvalidOperationException("All Cards requires active combat state.");
        AllCardsSession.SyncToCombat(CombatManager.Instance.DebugOnlyGetState());
        // The calculator opens on the run's deck, which is the deck a player is asking
        // questions about when they reach for it.
        AllCardsSession.SeedCalculator(player.Deck.Cards.Count);
        _pools = DrawPools.Resolve(player);
        _chosenDrawCount = AllCardsSession.ResolveDrawCount(_pools.NaturalDrawCount);
        _overlay.Enabled = AllCardsSession.ShowOddsOnCards;
        _grid = screen.GetNode<NCardGrid>("CardGrid");
        _bottomLabel = screen.GetNode<MegaRichTextLabel>("%BottomLabel");
        _refreshTimer = new Godot.Timer { WaitTime = 0.15, Autostart = true };
        _shelf = new NativeShelf();

        _searchBar = _shelf.AddSearchBar(_shelf.Top);

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

        _combatModules = [draw.Root, selection.Root, result.Root];
        AddCalculatorModules();

        var overlayToggle = _shelf.AddToggle(
            _shelf.Bottom, "Show Odds on Cards", AllCardsSession.ShowOddsOnCards);
        var rawdogToggle = _shelf.AddToggle(
            _shelf.Bottom, "Rawdog Mode", AllCardsSession.RawdogMode);
        rawdogToggle.Toggled += OnRawdogToggled;
        AddAboutRow();

        _drawDecrease.Pressed += () => ChangeDrawCount(-1);
        _drawIncrease.Pressed += () => ChangeDrawCount(1);
        _drawReset.Pressed += ResetDrawCount;
        _targetDecrease.Pressed += () => ChangeTargetCount(-1);
        _targetIncrease.Pressed += () => ChangeTargetCount(1);
        overlayToggle.Toggled += OnOverlayToggled;
        _searchBar.QueryChanged += OnSearchChanged;
        _refreshTimer.Timeout += RefreshPresentation;
    }

    public void Attach()
    {
        _screen.Name = AllCardsPileScreenCoordinator.ScreenName;
        RaiseAboveRunUi();
        // Behind the back button, exactly where the Card Library puts its sidebar.
        _screen.AddChild(_shelf.Root);
        _screen.MoveChild(_shelf.Root, _grid.GetIndex() + 1);
        _screen.AddChild(_refreshTimer);
        // The shelf owns the readout; the native strip would only repeat it.
        _bottomLabel.Visible = false;
        InsetGridForShelf();
        ApplyShelfMode();
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
        _searchBar.QueryChanged -= OnSearchChanged;
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
    /// The plain hypergeometric calculator, shown in place of the combat query when
    /// Rawdog Mode is on.
    ///
    /// The combat modules answer a question about this turn; these four numbers answer
    /// the same question about any deck at all, which is the one worth asking while
    /// building one. The grid is left alone underneath — it is still worth looking at.
    /// </summary>
    private void AddCalculatorModules()
    {
        var inputs = _shelf.AddModule(_shelf.Top, "Calculator");
        _population = _shelf.AddStepperRow(inputs.Body, "Deck");
        _sample = _shelf.AddStepperRow(inputs.Body, "Draw");
        _successes = _shelf.AddStepperRow(inputs.Body, "Hits in deck");
        _wanted = _shelf.AddStepperRow(inputs.Body, "Hits wanted");

        var results = _shelf.AddModule(_shelf.Top, "Odds");
        _exactlyRow = _shelf.AddRow(results.Body, "Exactly");
        _atLeastRow = _shelf.AddRow(results.Body, "At least");
        _atMostRow = _shelf.AddRow(results.Body, "At most");
        _expectedRow = _shelf.AddRow(results.Body, "Expected");
        _calculatorModules = [inputs.Root, results.Root];

        Step(_population, delta => AllCardsSession.Population += delta);
        Step(_sample, delta => AllCardsSession.Sample += delta);
        Step(_successes, delta => AllCardsSession.Successes += delta);
        Step(_wanted, delta => AllCardsSession.Wanted += delta);
    }

    private void Step(NativeShelf.ShelfStepper stepper, Action<int> change)
    {
        stepper.Decrease.Pressed += () => { change(-1); UpdateAnalysis(); };
        stepper.Increase.Pressed += () => { change(1); UpdateAnalysis(); };
    }

    /// <summary>
    /// Clamp the four numbers into a shape the maths can answer, then read it out.
    /// A sample cannot exceed its population, successes cannot exceed the population
    /// either, and no more hits can be wanted than could possibly be drawn.
    /// </summary>
    private void UpdateCalculator()
    {
        var population = Math.Clamp(AllCardsSession.Population, 1, 999);
        var sample = Math.Clamp(AllCardsSession.Sample, 0, population);
        var successes = Math.Clamp(AllCardsSession.Successes, 0, population);
        var wanted = Math.Clamp(AllCardsSession.Wanted, 0, Math.Min(successes, sample));
        AllCardsSession.Population = population;
        AllCardsSession.Sample = sample;
        AllCardsSession.Successes = successes;
        AllCardsSession.Wanted = wanted;

        _population.Value.Text = population.ToString();
        _sample.Value.Text = sample.ToString();
        _successes.Value.Text = successes.ToString();
        _wanted.Value.Text = wanted.ToString();

        _exactlyRow.Label.Text = $"Exactly {wanted}";
        _atLeastRow.Label.Text = $"At least {wanted}";
        _atMostRow.Label.Text = $"At most {wanted}";
        _exactlyRow.Value.Text = Hypergeometric.FormatPercent(
            Hypergeometric.Exactly(population, successes, sample, wanted));
        _atLeastRow.Value.Text = Hypergeometric.FormatPercent(
            Hypergeometric.AtLeast(population, successes, sample, wanted));
        _atMostRow.Value.Text = Hypergeometric.FormatPercent(
            Hypergeometric.AtMost(population, successes, sample, wanted));
        _expectedRow.Value.Text = Hypergeometric
            .ExpectedHits(population, successes, sample)
            .ToString("0.00", System.Globalization.CultureInfo.InvariantCulture);

        NativeShelf.SetButtonState(_population.Decrease, enabled: population > 1);
        NativeShelf.SetButtonState(_population.Increase, enabled: population < 999);
        NativeShelf.SetButtonState(_sample.Decrease, enabled: sample > 0);
        NativeShelf.SetButtonState(_sample.Increase, enabled: sample < population);
        NativeShelf.SetButtonState(_successes.Decrease, enabled: successes > 0);
        NativeShelf.SetButtonState(_successes.Increase, enabled: successes < population);
        NativeShelf.SetButtonState(_wanted.Decrease, enabled: wanted > 0);
        NativeShelf.SetButtonState(
            _wanted.Increase, enabled: wanted < Math.Min(successes, sample));
    }

    private void OnRawdogToggled(NTickbox tickbox)
    {
        AllCardsSession.RawdogMode = tickbox.IsTicked;
        ApplyShelfMode();
        UpdateAnalysis();
    }

    private void ApplyShelfMode()
    {
        foreach (var module in _combatModules)
            module.Visible = !AllCardsSession.RawdogMode;
        foreach (var module in _calculatorModules)
            module.Visible = AllCardsSession.RawdogMode;
    }

    /// <summary>
    /// The mod's own footer, in the corner the Card Library leaves for its settings.
    /// </summary>
    private void AddAboutRow()
    {
        var row = NativeShelf.CreateFullWidthRow(4);
        var label = _shelf.CreateText($"{MainFile.ModName}\n{MainFile.Version} by realtruegravy", 13);
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
        "Select cards to calculate the chance they are drawn in the next N cards.";
        // "on the reshuffle, so they share one section. Retained cards never leave " +
        // "your hand and cannot be drawn.\n\n" +
        // "The draw count is what next turn will deal, after relics, powers, retain, " +
        // "and hand size. Use − and + to ask about a different number; click the " +
        // "count to restore the real one.";

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

        // Prune against every card on the screen, not the filtered view: search hides
        // cards, it does not deselect them or take them out of the odds.
        var present = _pools.Draw
            .Concat(_pools.Reshuffle)
            .Concat(_pools.Retained)
            .ToList();
        _selectedCards.RemoveWhere(card => !present.Contains(card));
        _grid.SetCards(
            BuildSections().SelectMany(section => section.Cards).ToList(),
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
        var sections = new List<GridSection> { new(Shown(_pools.Draw), MarkerText: null) };
        var reshuffle = Shown(_pools.Reshuffle);
        if (reshuffle.Count > 0)
            sections.Add(new(reshuffle, ReshuffleMarkerText));
        var retained = Shown(_pools.Retained);
        if (retained.Count > 0)
            sections.Add(new(retained, RetainedMarkerText));
        return sections;
    }

    private List<CardModel> Shown(IEnumerable<CardModel> pile) =>
        Sort(pile).Where(Matches).ToList();

    /// <summary>
    /// The Card Library's own search behaviour: match the card's name or the text of
    /// its description, and let a rarity name stand for every card of that rarity.
    ///
    /// This only decides what the grid draws. Selections, populations and odds are all
    /// taken from the full pools, so searching narrows the view without moving a number.
    /// </summary>
    private bool Matches(CardModel card)
    {
        var query = _searchBar.Text;
        if (string.IsNullOrWhiteSpace(query))
            return true;
        query = query.ToLowerInvariant();
        if (Enum.TryParse<CardRarity>(query, ignoreCase: true, out var rarity) &&
            card.Rarity == rarity)
            return true;
        var description = card.GetDescriptionForPile(PileType.None).StripBbCode();
        return NSearchBar
            .Normalize($"{card.Title} {NSearchBar.RemoveHtmlTags(description)}")
            .Contains(query);
    }

    private void OnSearchChanged(string _) => Render();

    private void UpdateAnalysis()
    {
        var selectedTotal = _selectedCards.Count;
        var retainedSelected = _selectedCards.Count(_pools.Retained.Contains);
        var requiredHits = selectedTotal == 0
            ? 1
            : Math.Clamp(TargetHits, 1, selectedTotal);
        TargetHits = requiredHits;
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

        UpdateCalculator();
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
        AllCardsSession.ShowOddsOnCards = tickbox.IsTicked;
        _overlay.Enabled = tickbox.IsTicked;
        if (!_overlay.Enabled)
            _overlay.Clear();
        UpdateAnalysis();
    }

    private void ChangeDrawCount(int delta)
    {
        _chosenDrawCount = Math.Clamp(
            _chosenDrawCount + delta, 0, _pools.ReachableCount);
        AllCardsSession.SetChosenDrawCount(_chosenDrawCount, _pools.NaturalDrawCount);
        UpdateAnalysis();
    }

    private void ChangeTargetCount(int delta)
    {
        if (_selectedCards.Count == 0)
            return;
        TargetHits = Math.Clamp(TargetHits + delta, 1, _selectedCards.Count);
        UpdateAnalysis();
    }

    private void ResetDrawCount()
    {
        _pools = DrawPools.Resolve(_player);
        _chosenDrawCount = _pools.NaturalDrawCount;
        AllCardsSession.ClearChosenDrawCount();
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
