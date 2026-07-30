using System.Runtime.CompilerServices;
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

    private readonly NCardPileScreen _screen;
    private readonly Player _player;
    private readonly NCardGrid _grid;
    private readonly MegaRichTextLabel _bottomLabel;
    private readonly HashSet<CardModel> _selectedCards = AllCardsSession.SelectedCards;
    private readonly Godot.Timer _refreshTimer;

    private readonly NativeShelf _shelf;
    private readonly CardOddsOverlay _overlay = new();
    private readonly List<NativeShelf.ShelfMarker> _markers = [];
    private readonly Dictionary<CardIdentity, string> _overlayText = [];

    private readonly Button _drawDecrease;
    private readonly Button _drawIncrease;
    private readonly Button _drawReset;
    private readonly MegaLabel _drawCountLabel;
    private readonly Button _targetDecrease;
    private readonly Button _targetIncrease;
    private readonly Button _selectionReset;
    private readonly HBoxContainer _targetRow;
    private readonly HBoxContainer _resetRow;
    private readonly MegaLabel _targetCountLabel;
    private readonly NativeShelf.ShelfRow _needRow;
    private readonly NativeShelf.ShelfRow _heldRow;
    private readonly NativeShelf.ShelfRow _chanceRow;
    private readonly MegaLabel _hintLabel;
    private readonly MegaLabel _drawNote;
    private readonly MegaLabel _queryNote;
    private readonly NSearchBar _searchBar;
    private readonly NLibraryStatTickbox _overlayToggle;
    private readonly NLibraryStatTickbox _rawdogToggle;
    private readonly NLibraryStatTickbox _handToggle;
    private Button _helpButton = null!;
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
    private IReadOnlyList<IReadOnlyList<Control>> _shelfRows = [];
    private Control? _shelfEntry;

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

        var selection = _shelf.AddModule(_shelf.Top, "Hits");
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
        _targetRow = targetRow;
        _hintLabel = _shelf.AddCaption(selection.Body, string.Empty);

        var result = _shelf.AddModule(_shelf.Top, "Draw Chance");
        _queryNote = _shelf.AddNote(result.Body, string.Empty);
        _needRow = _shelf.AddRow(result.Body, "Need");
        _heldRow = _shelf.AddRow(result.Body, "In hand");
        _chanceRow = _shelf.AddRow(result.Body, "Chance");

        _resetRow = NativeShelf.CreateControlRow();
        var resetControl = _shelf.CreateButton(
            "Reset", 110, "Clear every selected card.");
        _selectionReset = resetControl.Input;
        _resetRow.AddChild(resetControl.Root);
        result.Body.AddChild(_resetRow);

        _combatModules = [draw.Root, selection.Root, result.Root];
        AddCalculatorModules();

        _overlayToggle = _shelf.AddToggle(
            _shelf.Bottom, "Show Odds on Cards", AllCardsSession.ShowOddsOnCards);
        _handToggle = _shelf.AddToggle(
            _shelf.Bottom,
            "Include Hand in Reshuffle",
            AllCardsSession.IncludeHandInReshuffle);
        _handToggle.Toggled += OnIncludeHandToggled;
        _rawdogToggle = _shelf.AddToggle(
            _shelf.Bottom, "Rawdog Mode", AllCardsSession.RawdogMode);
        _rawdogToggle.Toggled += OnRawdogToggled;
        AddAboutRow();

        _drawDecrease.Pressed += () => ChangeDrawCount(-1);
        _drawIncrease.Pressed += () => ChangeDrawCount(1);
        _drawReset.Pressed += ResetDrawCount;
        _targetDecrease.Pressed += () => ChangeTargetCount(-1);
        _targetIncrease.Pressed += () => ChangeTargetCount(1);
        _selectionReset.Pressed += ClearSelection;
        _overlayToggle.Toggled += OnOverlayToggled;
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
        TrackShelfFocus();
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
            if (GodotObject.IsInstanceValid(marker.Root))
                marker.Root.QueueFree();
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
        // The rows are named for cards; the hover tips name the same things for anyone
        // reading a statistics text alongside.
        var inputs = _shelf.AddModule(_shelf.Top, "Calculator");
        _population = _shelf.AddStepperRow(inputs.Body, "Deck", "Population");
        _sample = _shelf.AddStepperRow(inputs.Body, "Draw", "Sample Size");
        _successes = _shelf.AddStepperRow(
            inputs.Body, "Hits in deck", "Successes in population");
        _wanted = _shelf.AddStepperRow(
            inputs.Body, "Hits wanted", "Successes in sample");

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

    /// <summary>
    /// Whether the hand joins the reshuffle changes the populations themselves, so the
    /// pools are rebuilt rather than merely redrawn.
    /// </summary>
    private void OnIncludeHandToggled(NTickbox tickbox)
    {
        AllCardsSession.IncludeHandInReshuffle = tickbox.IsTicked;
        Render();
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
        WireShelfFocus();
    }

    /// <summary>
    /// Name every focus neighbour in the shelf, top to bottom, skipping whichever set of
    /// modules the current mode has hidden. Rebuilt on a mode change because the rows
    /// between the search bar and the toggles are swapped wholesale.
    /// </summary>
    private void WireShelfFocus()
    {
        if (!_shelf.Root.IsInsideTree())
            return;
        var rows = new List<IReadOnlyList<Control>>
        {
            new[]
            {
                _searchBar.GetNode<Control>("TextArea"),
                _searchBar.GetNode<Control>("ClearButton"),
            },
        };
        if (AllCardsSession.RawdogMode)
            foreach (var stepper in new[] { _population, _sample, _successes, _wanted })
                rows.Add(new[] { stepper.Decrease, stepper.Increase });
        else
        {
            rows.Add(new[] { _drawDecrease, _drawReset, _drawIncrease });
            rows.Add(new[] { _targetDecrease, _targetIncrease });
            rows.Add(new Control[] { _selectionReset });
        }
        rows.Add(new Control[] { _overlayToggle });
        rows.Add(new Control[] { _handToggle });
        rows.Add(new Control[] { _rawdogToggle });
        rows.Add(new Control[] { _helpButton });

        NativeShelf.WireFocusRows(rows);
        _shelfRows = rows;
    }

    /// <summary>
    /// Remember which shelf control focus was last on, so returning from the grid lands
    /// there. Every shelf control is wired once; the mode switch only reorders them.
    /// </summary>
    private void TrackShelfFocus()
    {
        foreach (var control in new Control[]
                 {
                     _searchBar.GetNode<Control>("TextArea"),
                     _searchBar.GetNode<Control>("ClearButton"),
                     _drawDecrease, _drawReset, _drawIncrease,
                     _targetDecrease, _targetIncrease, _selectionReset,
                     _population.Decrease, _population.Increase,
                     _sample.Decrease, _sample.Increase,
                     _successes.Decrease, _successes.Increase,
                     _wanted.Decrease, _wanted.Increase,
                     _overlayToggle, _handToggle, _rawdogToggle, _helpButton,
                 })
        {
            var tracked = control;
            tracked.FocusEntered += () => _shelfEntry = tracked;
        }
    }

    /// <summary>
    /// Navigate the grid by the layout on screen rather than by the grid's own idea of
    /// its rows.
    ///
    /// NCardGrid numbers holders in the order it was handed them and wires focus from
    /// that. This screen then repositions them around the section markers, so the two
    /// disagree by however many markers precede a card — and focus jumps somewhere the
    /// card visibly is not, which reads as identical cards being confused for each
    /// other. Wiring from the slots the cards were actually placed in keeps focus
    /// travelling the way the grid looks. Marker slots hold no card, so travel steps
    /// over them.
    /// </summary>
    private void WireGridFocus(
        Dictionary<int, NCardHolder> bySlot, int columns, int lastSlot)
    {
        NCardHolder? Seek(int from, int step, Func<int, bool> withinBounds)
        {
            for (var slot = from; withinBounds(slot); slot += step)
                if (bySlot.TryGetValue(slot, out var found))
                    return found;
            return null;
        }

        var leftEdge = new List<NCardHolder>();
        foreach (var (slot, holder) in bySlot)
        {
            var rowStart = slot - slot % columns;
            var left = Seek(slot - 1, -1, candidate => candidate >= rowStart);
            var right = Seek(slot + 1, 1, candidate => candidate <= rowStart + columns - 1);
            var up = Seek(slot - columns, -columns, candidate => candidate >= 0);
            var down = Seek(slot + columns, columns, candidate => candidate <= lastSlot);

            // Pointing at itself parks focus, which is what the edges of the grid want.
            holder.FocusNeighborRight = (right ?? holder).GetPath();
            holder.FocusNeighborTop = (up ?? holder).GetPath();
            holder.FocusNeighborBottom = (down ?? holder).GetPath();
            if (left != null)
                holder.FocusNeighborLeft = left.GetPath();
            else
                leftEdge.Add(holder);
        }
        WireShelfSeam(leftEdge);
    }

    /// <summary>
    /// Hand focus across the gap between the shelf and the grid, in both directions.
    ///
    /// Neither side can be wired once and left: the grid rebuilds its holders, and which
    /// card sits at the left edge changes with the column count, so both ends are
    /// reapplied from the refresh tick.
    /// </summary>
    private void WireShelfSeam(IReadOnlyList<NCardHolder> leftEdge)
    {
        if (leftEdge.Count == 0 || _shelfRows.Count == 0)
            return;
        // Into the shelf: whichever control was last used, so leaving and returning
        // lands where it left off rather than at the top every time.
        var entry = GodotObject.IsInstanceValid(_shelfEntry)
            ? _shelfEntry!
            : _shelfRows[0][0];
        foreach (var holder in leftEdge)
            holder.FocusNeighborLeft = entry.GetPath();
        // Out of the shelf: the nearest left-edge card to each row, by height, so the
        // grid is entered beside whatever was being looked at.
        foreach (var row in _shelfRows)
        {
            var last = row[^1];
            var centre = last.GlobalPosition.Y + last.Size.Y * 0.5f;
            var nearest = leftEdge
                .OrderBy(holder => Math.Abs(holder.GlobalPosition.Y - centre))
                .First();
            last.FocusNeighborRight = nearest.GetPath();
        }
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
        _helpButton = help.Input;
        row.AddChild(help.Root);
        _shelf.Bottom.AddChild(row);
    }

    private static string HelpText =>
        "\nSelect cards to calculate the chance they will be drawn in the next N cards.\n\nUse 'W' to show the All Cards view during combat.\n\nOptionally replace the Draw Pile view with this mod's All Cards view via the Mod Settings (only accessible from the main menu).";
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
        // Any card played, drawn or exhausted moves the board the selection was made
        // against, and drops it.
        AllCardsSession.SyncToPiles(PileFingerprint());
        // The grid is about to recycle its pooled holders, taking any badge with it.
        _overlay.Clear();

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
        var sections = new List<GridSection> { new(Shown(_pools.Draw), null, null) };
        var reshuffle = Shown(_pools.Reshuffle);
        if (reshuffle.Count > 0)
            sections.Add(new(
                reshuffle,
                "Reshuffle",
                // The hand only joins the reshuffle when the turn is going to end.
                AllCardsSession.IncludeHandInReshuffle
                    ? "Discard Pile + Cards in Hand"
                    : "Discard Pile"));
        var inHand = Shown(_pools.HandOutsideReshuffle);
        if (inHand.Count > 0)
            sections.Add(new(
                inHand,
                "In Hand",
                "Staying in hand while you draw this turn, so not reshuffled"));
        var retained = Shown(_pools.Retained);
        if (retained.Count > 0)
            sections.Add(new(
                retained, "Retained", "Stays in hand, not reshuffled"));
        return sections;
    }

    private List<CardModel> Shown(IEnumerable<CardModel> pile) =>
        Sort(pile).Where(Matches).ToList();

    /// <summary>
    /// Which cards are in which pile, by identity rather than by name, so two copies of
    /// a card swapping places still counts as a change. Order within a pile is ignored:
    /// a shuffle moves no card between piles and changes no odds.
    /// </summary>
    private string PileFingerprint()
    {
        static string Pile(IEnumerable<CardModel> cards) =>
            string.Join(
                ',',
                cards.Select(RuntimeHelpers.GetHashCode).OrderBy(id => id));
        return string.Join(
            '|', Pile(_pools.Draw), Pile(_pools.Discard), Pile(_pools.Hand));
    }

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
        // Counts both reasons a selected card cannot be drawn: an effect holding it,
        // and a hand that is staying put while drawing this turn.
        var retainedSelected = _selectedCards.Count(
            card => _pools.Retained.Contains(card) ||
                    _pools.HandOutsideReshuffle.Contains(card));
        // Zero is a question, not an empty state: the chance of drawing none of them.
        var requiredHits = selectedTotal == 0
            ? 0
            : Math.Clamp(TargetHits, 0, selectedTotal);
        TargetHits = requiredHits;
        var chance = selectedTotal == 0
            ? 0
            : requiredHits == 0
                ? _pools.ChanceOfNone(_selectedCards.Contains, _chosenDrawCount)
                : _pools.ChanceOfAtLeast(
                    _selectedCards.Contains, _chosenDrawCount, requiredHits);

        _drawCountLabel.Text = _chosenDrawCount.ToString();
        _targetCountLabel.Text = requiredHits.ToString();
        // With nothing picked the row is a prompt, not a control: a stepper that can
        // only read zero of zero is noise.
        _hintLabel.Text = selectedTotal == 0
            ? "Select cards in the grid."
            : $"of {selectedTotal} selected";
        _targetRow.Visible = selectedTotal > 0;
        _resetRow.Visible = selectedTotal > 0;

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
            _targetDecrease, enabled: selectedTotal > 0 && requiredHits > 0);
        NativeShelf.SetButtonState(
            _targetIncrease, enabled: selectedTotal > 0 && requiredHits < selectedTotal);
        NativeShelf.SetButtonState(_selectionReset, enabled: selectedTotal > 0);

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
        // Nothing selected needs no sentence — the row above already says to pick cards,
        // and the blank chance says the rest.
        if (selectedTotal == 0)
            return string.Empty;

        // Every name, however many. A selection is worth naming in full; the note wraps.
        var names = _selectedCards
            .OrderBy(card => card.Rarity)
            .ThenBy(card => card.Title, StringComparer.CurrentCulture)
            .Select(card => card.Title)
            .Distinct()
            .ToList();
        var wantsEveryOne = requiredHits == selectedTotal;
        var joiner = requiredHits == 0 || wantsEveryOne ? " and " : " or ";
        var listed = names.Count == 1
            ? names[0]
            : string.Join(", ", names.Take(names.Count - 1)) + joiner + names[^1];

        if (requiredHits == 0)
            return $"Chance to draw none of {listed}:";
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
        var markerSlots = new List<(int Index, string Heading, string Description)>();
        foreach (var section in sections)
        {
            if (section.MarkerHeading != null)
            {
                markerSlots.Add((
                    slots.Count,
                    section.MarkerHeading,
                    section.MarkerDescription ?? string.Empty));
                slots.Add(null);
            }
            slots.AddRange(section.Cards);
        }

        var bySlot = new Dictionary<int, NCardHolder>();
        foreach (var holder in _grid.CurrentlyDisplayedCardHolders)
        {
            if (holder.CardModel is not { } card)
                continue;
            var slot = IndexOfCard(slots, card);
            if (slot < 0)
                continue;
            holder.Position = SlotPosition(slot);
            bySlot[slot] = holder;
        }
        WireGridFocus(bySlot, columns, slots.Count - 1);

        for (var index = 0; index < markerSlots.Count; index++)
        {
            var (slot, heading, description) = markerSlots[index];
            var marker = ResolveMarker(index, scrollContainer, cardSize);
            marker.Heading.Text = heading;
            marker.Description = description;
            marker.Root.Visible = true;
            marker.Root.CustomMinimumSize = cardSize;
            marker.Root.Size = cardSize;
            marker.Root.Position = SlotPosition(slot) - cardSize * 0.5f;
            // A square as wide as a card, centred in the taller card slot: enough of a
            // box to read as a label without pretending to be another card.
            var side = new Vector2(cardSize.X, cardSize.X);
            marker.Box.CustomMinimumSize = side;
            marker.Box.Size = side;
            marker.Box.Position = (cardSize - side) * 0.5f;
        }
        for (var index = markerSlots.Count; index < _markers.Count; index++)
            _markers[index].Root.Visible = false;

        var rows = (int)Math.Ceiling(slots.Count / (double)columns);
        var containedHeight =
            rows * cardSize.Y + Math.Max(0, rows - 1) * CardPadding;
        var requiredHeight = containedHeight + 400 + _grid.YOffset;
        if (scrollContainer.Size.Y < requiredHeight)
            scrollContainer.Size = new Vector2(
                scrollContainer.Size.X, requiredHeight);
    }

    private NativeShelf.ShelfMarker ResolveMarker(
        int index, Control parent, Vector2 cardSize)
    {
        if (index < _markers.Count && GodotObject.IsInstanceValid(_markers[index].Root))
            return _markers[index];

        // Leave the z index at 0. The game's hover tips sit at an absolute z_index of
        // 0, so any positive value here paints the marker over every tooltip. At 0 the
        // marker sorts by tree order: below the card holders, which is right, since its
        // own slot is always empty.
        var marker = _shelf.CreateSectionMarker();
        marker.Root.Name = $"HypergeoSectionMarker{index}";
        marker.Root.CustomMinimumSize = cardSize;
        marker.Root.Size = cardSize;
        parent.AddChild(marker.Root);

        if (index < _markers.Count)
            _markers[index] = marker;
        else
            _markers.Add(marker);
        return marker;
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
        var wasEmpty = _selectedCards.Count == 0;
        if (!_selectedCards.Add(card))
            _selectedCards.Remove(card);
        // Opening a selection asks the ordinary question first. Zero stays available,
        // but only once it has been chosen deliberately.
        if (wasEmpty && _selectedCards.Count > 0)
            TargetHits = 1;
        UpdateAnalysis();
    }

    private void ClearSelection()
    {
        _selectedCards.Clear();
        TargetHits = 0;
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
        TargetHits = Math.Clamp(TargetHits + delta, 0, _selectedCards.Count);
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
        IReadOnlyList<CardModel> Cards, string? MarkerHeading, string? MarkerDescription);
}
