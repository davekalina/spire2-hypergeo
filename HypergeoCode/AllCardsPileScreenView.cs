using Godot;
using MegaCrit.Sts2.Core.Assets;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Cards;
using MegaCrit.Sts2.Core.Nodes.Cards.Holders;
using MegaCrit.Sts2.Core.Nodes.HoverTips;
using MegaCrit.Sts2.Core.Nodes.Screens;
using MegaCrit.Sts2.Core.Nodes.Screens.CardLibrary;
using MegaCrit.Sts2.addons.mega_text;

namespace Hypergeo.HypergeoCode;

internal sealed class AllCardsPileScreenView : IDisposable
{
    private readonly NCardPileScreen _screen;
    private readonly Player _player;
    private readonly PlayerCombatState _combatState;
    private readonly NCardGrid _grid;
    private readonly HashSet<CardModel> _selectedCards = [];
    private readonly Control _nativeButtonTemplate;
    private Control? _discardSpacer;
    private readonly VBoxContainer _controls;
    private readonly Control _controlsFrame;
    private readonly MegaRichTextLabel _bottomLabel;
    private readonly Label _drawCountLabel;
    private readonly Label _targetCountLabel;
    private readonly Label _selectedCountLabel;
    private readonly Button _anyButton;
    private readonly Button _allButton;
    private readonly HBoxContainer _targetRow;
    private readonly Button _targetDecrease;
    private readonly Button _targetIncrease;
    private readonly Godot.Timer _refreshTimer;
    private bool _anyMode = true;
    private int _naturalDrawCount;
    private int _chosenDrawCount;
    private int _targetHits = 1;

    public AllCardsPileScreenView(NCardPileScreen screen, Player player)
    {
        _screen = screen;
        _player = player;
        _combatState = player.PlayerCombatState ??
            throw new InvalidOperationException("All Cards requires active combat state.");
        _grid = screen.GetNode<NCardGrid>("CardGrid");
        _nativeButtonTemplate = CreateNativeButtonTemplate();
        _controls = CreateControls(
            _nativeButtonTemplate,
            out _anyButton,
            out _allButton,
            out var drawDecrease,
            out var drawIncrease,
            out _drawCountLabel,
            out var resetDrawCount,
            out _targetDecrease,
            out _targetIncrease,
            out _targetCountLabel,
            out _targetRow,
            out _selectedCountLabel);
        _controlsFrame = CreateControlsFrame(_controls);
        _bottomLabel = screen.GetNode<MegaRichTextLabel>("%BottomLabel");
        _refreshTimer = new Godot.Timer { WaitTime = 0.15, Autostart = true };

        _anyButton.Pressed += () => SetAnyMode(true);
        _allButton.Pressed += () => SetAnyMode(false);
        drawDecrease.Pressed += () => ChangeDrawCount(-1);
        drawIncrease.Pressed += () => ChangeDrawCount(1);
        resetDrawCount.Pressed += ResetDrawCount;
        _targetDecrease.Pressed += () => ChangeTargetCount(-1);
        _targetIncrease.Pressed += () => ChangeTargetCount(1);
        _refreshTimer.Timeout += RefreshPresentation;
    }

    public void Attach()
    {
        _screen.Name = "NCardPileScreen-AllCards";
        _screen.AddChild(_controlsFrame);
        _screen.AddChild(_refreshTimer);
        _bottomLabel.Visible = true;
        _grid.HolderPressed += OnHolderPressed;
        _grid.HolderAltPressed += OnHolderPressed;
        _combatState.DrawPile.ContentsChanged += Render;
        _combatState.DiscardPile.ContentsChanged += Render;
        _naturalDrawCount = ResolveNaturalDrawCount();
        _chosenDrawCount = _naturalDrawCount;
        Render();
    }

    public void Dispose()
    {
        _grid.HolderPressed -= OnHolderPressed;
        _grid.HolderAltPressed -= OnHolderPressed;
        _combatState.DrawPile.ContentsChanged -= Render;
        _combatState.DiscardPile.ContentsChanged -= Render;
        _refreshTimer.Timeout -= RefreshPresentation;
        if (_discardSpacer != null && GodotObject.IsInstanceValid(_discardSpacer))
            _discardSpacer.QueueFree();
        if (GodotObject.IsInstanceValid(_nativeButtonTemplate))
            _nativeButtonTemplate.Free();
    }

    private void Render()
    {
        if (!GodotObject.IsInstanceValid(_screen))
            return;
        var drawCards = _combatState.DrawPile.Cards.ToList();
        var discardCards = _combatState.DiscardPile.Cards.ToList();
        var allCards = drawCards.Concat(discardCards).ToList();
        _selectedCards.RemoveWhere(card => !allCards.Contains(card));
        var orderedCards = Sort(drawCards).Concat(Sort(discardCards)).ToList();
        _grid.SetCards(
            orderedCards,
            PileType.Draw,
            new List<SortingOrders> { SortingOrders.Ascending });
        UpdateAnalysis(drawCards, discardCards);
    }

    private void UpdateAnalysis(
        IReadOnlyList<CardModel>? drawCards = null,
        IReadOnlyList<CardModel>? discardCards = null)
    {
        drawCards ??= _combatState.DrawPile.Cards;
        discardCards ??= _combatState.DiscardPile.Cards;
        var selectedDraw = drawCards.Count(card => _selectedCards.Contains(card));
        var selectedDiscard = discardCards.Count(card => _selectedCards.Contains(card));
        var selectedTotal = selectedDraw + selectedDiscard;
        _targetHits = selectedTotal == 0
            ? 1
            : Math.Clamp(_targetHits, 1, selectedTotal);
        var requiredHits = _anyMode ? _targetHits : selectedTotal;
        var chance = selectedTotal == 0
            ? 0
            : DrawOddsCalculator.AtLeastAcrossPiles(
                drawCards.Count, selectedDraw,
                discardCards.Count, selectedDiscard,
                _chosenDrawCount, requiredHits);

        _drawCountLabel.Text = _chosenDrawCount.ToString();
        _targetCountLabel.Text = (_anyMode ? _targetHits : selectedTotal).ToString();
        _targetRow.Visible = _anyMode;
        _selectedCountLabel.Text = _anyMode
            ? $"of {selectedTotal} cards"
            : $"({selectedTotal}) Selected Cards";
        SetControlButtonState(_anyButton, enabled: !_anyMode, highlighted: _anyMode);
        SetControlButtonState(_allButton, enabled: _anyMode, highlighted: !_anyMode);
        SetControlButtonState(
            _targetDecrease,
            enabled: _anyMode && selectedTotal > 0 && _targetHits > 1);
        SetControlButtonState(
            _targetIncrease,
            enabled: _anyMode && selectedTotal > 0 && _targetHits < selectedTotal);
        _bottomLabel.Text = selectedTotal == 0
            ? "[center]Select one or more cards to calculate draw chance."
            : $"[center]Chance of drawing {(_anyMode ? "ANY" : "ALL")} {requiredHits} of {selectedTotal} selected cards " +
              $"(out of {_chosenDrawCount}): {Hypergeometric.FormatPercent(chance)}";
        RefreshPresentation();
    }

    private void RefreshPresentation()
    {
        if (!GodotObject.IsInstanceValid(_screen))
            return;
        var drawCards = _combatState.DrawPile.Cards.ToList();
        var discardCards = _combatState.DiscardPile.Cards.ToList();
        var orderedCards = Sort(drawCards).Concat(Sort(discardCards)).ToList();
        foreach (var holder in _grid.CurrentlyDisplayedCardHolders)
        {
            if (holder.CardModel is not { } card)
                continue;
            if (_selectedCards.Contains(card))
                _grid.HighlightCard(card);
            else
                _grid.UnhighlightCard(card);
        }
        UpdateDiscardSpacer(orderedCards, drawCards.Count, discardCards.Count);
    }

    private void UpdateDiscardSpacer(
        IReadOnlyList<CardModel> orderedCards,
        int drawCardCount,
        int discardCardCount)
    {
        if (discardCardCount == 0)
        {
            if (_discardSpacer != null)
                _discardSpacer.Visible = false;
            return;
        }

        var scrollContainer = _grid.GetNode<Control>("%ScrollContainer");
        var cardSize = NCard.defaultSize * NCardHolder.smallScale;
        const float cardPadding = 40;
        var columns = Math.Max(
            1,
            (int)((scrollContainer.Size.X + cardPadding) /
                  (cardSize.X + cardPadding)));
        var containedWidth =
            columns * cardSize.X + (columns - 1) * cardPadding;
        var origin = new Vector2(
                         (scrollContainer.Size.X - containedWidth) * 0.5f,
                         _grid.YOffset + 80) +
                     cardSize * 0.5f;

        static Vector2 SlotPosition(
            int index,
            int columns,
            Vector2 origin,
            Vector2 cardSize,
            float padding) =>
            origin + new Vector2(
                index % columns * (cardSize.X + padding),
                index / columns * (cardSize.Y + padding));

        foreach (var holder in _grid.CurrentlyDisplayedCardHolders)
        {
            if (holder.CardModel is not { } card)
                continue;
            var cardIndex = IndexOfCard(orderedCards, card);
            if (cardIndex < 0)
                continue;
            var displayIndex = cardIndex >= drawCardCount
                ? cardIndex + 1
                : cardIndex;
            holder.Position = SlotPosition(
                displayIndex, columns, origin, cardSize, cardPadding);
        }

        if (_discardSpacer == null ||
            !GodotObject.IsInstanceValid(_discardSpacer))
        {
            _discardSpacer = CreateDiscardSpacer(cardSize);
            scrollContainer.AddChild(_discardSpacer);
        }
        _discardSpacer.Visible = true;
        _discardSpacer.CustomMinimumSize = cardSize;
        _discardSpacer.Size = cardSize;
        _discardSpacer.Position = SlotPosition(
            drawCardCount, columns, origin, cardSize, cardPadding) -
            cardSize * 0.5f;

        var rows = (int)Math.Ceiling((orderedCards.Count + 1d) / columns);
        var containedHeight =
            rows * cardSize.Y + Math.Max(0, rows - 1) * cardPadding;
        var requiredHeight = containedHeight + 400 + _grid.YOffset;
        if (scrollContainer.Size.Y < requiredHeight)
            scrollContainer.Size = new Vector2(
                scrollContainer.Size.X, requiredHeight);
    }

    private Control CreateDiscardSpacer(Vector2 cardSize)
    {
        var root = new Control
        {
            Name = "DrawOddsDiscardSpacer",
            CustomMinimumSize = cardSize,
            Size = cardSize,
            MouseFilter = Control.MouseFilterEnum.Ignore,
            ZIndex = 40,
        };

        var label = CreateNativeText(
            _nativeButtonTemplate, "DISCARD PILE\n→", 17);
        label.Name = "DiscardPileLabel";
        label.SetAnchorsPreset(Control.LayoutPreset.TopLeft);
        label.Position = Vector2.Zero;
        label.Size = cardSize;
        label.PivotOffset = Vector2.Zero;
        label.HorizontalAlignment = HorizontalAlignment.Center;
        label.VerticalAlignment = VerticalAlignment.Center;
        label.ZIndex = 1;
        root.AddChild(label);

        return root;
    }

    private static int IndexOfCard(
        IReadOnlyList<CardModel> cards, CardModel target)
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

        DrawChanceHoverTip.Show(
            holder,
            card,
            _combatState.DrawPile.Cards,
            _combatState.DiscardPile.Cards,
            _chosenDrawCount);
        return true;
    }

    private void SetAnyMode(bool anyMode)
    {
        _anyMode = anyMode;
        UpdateAnalysis();
    }

    private void ChangeDrawCount(int delta)
    {
        var total = _combatState.DrawPile.Cards.Count +
                    _combatState.DiscardPile.Cards.Count;
        _chosenDrawCount = Math.Clamp(_chosenDrawCount + delta, 0, total);
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
        _naturalDrawCount = ResolveNaturalDrawCount();
        _chosenDrawCount = _naturalDrawCount;
        UpdateAnalysis();
    }

    private int ResolveNaturalDrawCount()
    {
        var state = CombatManager.Instance.DebugOnlyGetState();
        if (state == null || !Hook.ShouldDraw(state, _player, fromHandDraw: true, out _))
            return 0;
        var modified = Hook.ModifyHandDraw(
            state, _player, CombatManager.baseHandDrawCount, out _);
        var retained = Hook.ShouldFlush(state, _player)
            ? _combatState.Hand.Cards.Count(card => card.ShouldRetainThisTurn)
            : _combatState.Hand.Cards.Count;
        return Math.Min(
            Math.Max(0, CardPile.MaxCardsInHand - retained),
            Math.Max(0, (int)modified));
    }

    private static VBoxContainer CreateControls(
        Control nativeButtonTemplate,
        out Button any,
        out Button all,
        out Button drawDecrease,
        out Button drawIncrease,
        out Label drawCount,
        out Button resetDrawCount,
        out Button targetDecrease,
        out Button targetIncrease,
        out Label targetCount,
        out HBoxContainer targetRow,
        out Label selectedCount)
    {
        var controls = new VBoxContainer
        {
            Alignment = BoxContainer.AlignmentMode.Center,
            SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
        };
        controls.AddThemeConstantOverride("separation", 8);
        var drawTitle = NativeLabel(nativeButtonTemplate, "Draw", 19);
        drawTitle.HorizontalAlignment = HorizontalAlignment.Center;
        drawTitle.SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter;
        controls.AddChild(drawTitle);
        var drawRow = CreateCounterRow(nativeButtonTemplate,
            out drawDecrease,
            out drawCount,
            out drawIncrease,
            out var drawCountInput,
            "Natural Draw",
            "Restore the natural next-hand draw count after game effects.");
        resetDrawCount = drawCountInput ??
                         throw new InvalidOperationException(
                             "Draw count reset input was not created.");
        drawRow.SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter;
        controls.AddChild(drawRow);
        var selectTitle = NativeLabel(nativeButtonTemplate, "to select…", 17);
        selectTitle.HorizontalAlignment = HorizontalAlignment.Center;
        selectTitle.SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter;
        controls.AddChild(selectTitle);

        var modeRow = new HBoxContainer();
        modeRow.Alignment = BoxContainer.AlignmentMode.Center;
        modeRow.SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter;
        modeRow.AddThemeConstantOverride("separation", 6);
        var anyControl = NativeButton(
            nativeButtonTemplate,
            "ANY",
            78,
            "Calculate the chance of drawing at least N selected cards.");
        any = anyControl.Input;
        var allControl = NativeButton(
            nativeButtonTemplate,
            "ALL",
            78,
            "Calculate the chance of drawing every selected card.");
        all = allControl.Input;
        modeRow.AddChild(anyControl.Root);
        modeRow.AddChild(allControl.Root);
        controls.AddChild(modeRow);
        targetRow = CreateCounterRow(nativeButtonTemplate,
            out targetDecrease,
            out targetCount,
            out targetIncrease,
            out _);
        targetRow.SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter;
        controls.AddChild(targetRow);
        selectedCount = NativeLabel(nativeButtonTemplate, "", 17);
        selectedCount.HorizontalAlignment = HorizontalAlignment.Center;
        selectedCount.SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter;
        controls.AddChild(selectedCount);
        return controls;
    }

    private static Control CreateNativeButtonTemplate()
    {
        var cardLibrary = PreloadManager.Cache
            .GetScene(SceneHelper.GetScenePath("screens/card_library/card_library"))
            .Instantiate<Control>(PackedScene.GenEditState.Disabled);
        var template = (Control)cardLibrary
            .GetNode<Control>("%CardTypeSorter")
            .Duplicate();
        var selectedOutline = (Control)cardLibrary
            .GetNode<Control>("%AttackType")
            .GetNode<Control>("%Outline")
            .Duplicate();
        selectedOutline.Name = "DrawOddsSelectedOutlineTemplate";
        selectedOutline.Hide();
        template.AddChild(selectedOutline);
        cardLibrary.Free();
        return template;
    }

    private static Control CreateControlsFrame(VBoxContainer controls)
    {
        var frame = PreloadManager.Cache
            .GetScene("res://scenes/ui/hover_tip.tscn")
            .Instantiate<Control>(PackedScene.GenEditState.Disabled);
        frame.Name = "DrawOddsControlsFrame";
        frame.MouseFilter = Control.MouseFilterEnum.Pass;
        frame.AnchorLeft = 0;
        frame.AnchorRight = 0;
        frame.AnchorTop = 0.20f;
        frame.AnchorBottom = 0.20f;
        frame.OffsetLeft = 16;
        frame.OffsetRight = 236;
        frame.OffsetTop = 0;
        frame.OffsetBottom = 274;
        frame.CustomMinimumSize = Vector2.Zero;
        frame.GetNodeOrNull<Control>("%Title")?.Hide();
        frame.GetNodeOrNull<Control>("%Description")?.Hide();
        frame.GetNodeOrNull<Control>("%Icon")?.Hide();

        var margin = new MarginContainer();
        margin.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        margin.OffsetLeft = -8;
        margin.OffsetRight = -8;
        margin.AddThemeConstantOverride("margin_left", 10);
        margin.AddThemeConstantOverride("margin_right", 26);
        margin.AddThemeConstantOverride("margin_top", 18);
        margin.AddThemeConstantOverride("margin_bottom", 18);
        margin.AddChild(controls);
        frame.AddChild(margin);
        return frame;
    }

    private static HBoxContainer CreateCounterRow(
        Control nativeButtonTemplate,
        out Button decrease,
        out Label count,
        out Button increase,
        out Button? countInput,
        string? countHoverTitle = null,
        string? countHoverDescription = null)
    {
        var row = new HBoxContainer();
        row.Alignment = BoxContainer.AlignmentMode.Center;
        row.AddThemeConstantOverride("separation", 6);
        var decreaseControl = NativeButton(nativeButtonTemplate, "−", 48);
        decrease = decreaseControl.Input;
        Control countRoot;
        if (countHoverDescription is null)
        {
            var countControl = NativeDisplay(nativeButtonTemplate, "", 52);
            count = countControl.Label;
            countRoot = countControl.Root;
            countInput = null;
        }
        else
        {
            var countControl = NativeButton(
                nativeButtonTemplate,
                "",
                52,
                countHoverDescription,
                countHoverTitle);
            count = countControl.Label;
            countRoot = countControl.Root;
            countInput = countControl.Input;
        }
        var increaseControl = NativeButton(nativeButtonTemplate, "+", 48);
        increase = increaseControl.Input;
        row.AddChild(decreaseControl.Root);
        row.AddChild(countRoot);
        row.AddChild(increaseControl.Root);
        return row;
    }

    private static NativeControlButton NativeButton(
        Control template,
        string text,
        float width,
        string? hoverDescription = null,
        string? hoverTitle = null)
    {
        var display = NativeDisplay(template, text, width);
        var input = new Button
        {
            Flat = true,
            FocusMode = Control.FocusModeEnum.None,
            MouseFilter = Control.MouseFilterEnum.Stop,
        };
        input.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        input.MouseEntered += () => AnimateNativeButton(display.Visual, 1.04f, 0.06);
        input.MouseExited += () => AnimateNativeButton(display.Visual, 1f, 0.16);
        if (hoverDescription != null)
        {
            input.MouseEntered += () => NHoverTipSet.CreateAndShow(
                input,
                NativeHoverTip.Create(
                    hoverTitle ?? text,
                    hoverDescription,
                    $"DrawOddsControl:{hoverTitle ?? text}"),
                HoverTipAlignment.Right);
            input.MouseExited += () => NHoverTipSet.Remove(input);
        }
        display.Root.AddChild(input);
        return new NativeControlButton(display.Root, input, display.Label);
    }

    private static NativeControlDisplay NativeDisplay(
        Control template, string text, float width)
    {
        var root = new Control
        {
            CustomMinimumSize = new Vector2(width, 40),
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        var visual = new Control
        {
            Name = "NativeVisual",
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        visual.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        var backgroundSource = template.GetNode<TextureRect>("%ButtonImage");
        var background = new TextureRect
        {
            Name = "NativeBackground",
            Texture = backgroundSource.Texture,
            Material = backgroundSource.Material,
            Modulate = backgroundSource.Modulate,
            SelfModulate = backgroundSource.SelfModulate,
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.Scale,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        visual.AddChild(background);

        var fontSize = text.Length switch
        {
            > 10 => 13,
            > 5 => 15,
            _ => 19,
        };
        var label = CreateNativeText(template, text, fontSize);
        label.Name = "NativeLabel";
        label.SetAnchorsPreset(Control.LayoutPreset.TopLeft);
        label.Position = Vector2.Zero;
        label.PivotOffset = Vector2.Zero;
        label.HorizontalAlignment = HorizontalAlignment.Center;
        label.VerticalAlignment = VerticalAlignment.Center;
        visual.AddChild(label);

        var outlineSource = template.GetNode<TextureRect>(
            "DrawOddsSelectedOutlineTemplate");
        var selectedOutline = new NinePatchRect
        {
            Name = "NativeSelectedOutline",
            Texture = outlineSource.Texture,
            Material = outlineSource.Material,
            Modulate = outlineSource.Modulate,
            SelfModulate = outlineSource.SelfModulate,
            DrawCenter = false,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        selectedOutline.SetPatchMargin(Side.Left, 14);
        selectedOutline.SetPatchMargin(Side.Top, 14);
        selectedOutline.SetPatchMargin(Side.Right, 14);
        selectedOutline.SetPatchMargin(Side.Bottom, 14);
        selectedOutline.Hide();
        visual.AddChild(selectedOutline);

        root.Resized += () =>
        {
            const float selectedOutlineBleed = 5f;
            visual.PivotOffset = visual.Size * 0.5f;
            background.Position = Vector2.Zero;
            background.Size = root.Size;
            label.Position = Vector2.Zero;
            label.Size = root.Size;
            selectedOutline.Position = new Vector2(
                -selectedOutlineBleed, -selectedOutlineBleed);
            selectedOutline.Size = root.Size +
                                   Vector2.One * selectedOutlineBleed * 2f;
        };
        root.AddChild(visual);
        return new NativeControlDisplay(root, visual, label);
    }

    private static MegaLabel NativeLabel(Control template, string text, int size)
    {
        var label = CreateNativeText(template, text, size);
        label.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.TopLeft);
        label.Position = Vector2.Zero;
        label.PivotOffset = Vector2.Zero;
        return label;
    }

    private static MegaLabel CreateNativeText(
        Control template, string text, int fontSize)
    {
        var source = template.GetNode<MegaLabel>("%Label");
        var label = new MegaLabel
        {
            AutoSizeEnabled = false,
            Text = text,
            Scale = Vector2.One,
            Rotation = 0,
            CustomMinimumSize = Vector2.Zero,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        label.AddThemeFontOverride("font", source.GetThemeFont("font"));
        label.AddThemeFontSizeOverride("font_size", fontSize);
        label.AddThemeColorOverride(
            "font_color", source.GetThemeColor("font_color"));
        label.AddThemeColorOverride(
            "font_outline_color",
            source.GetThemeColor("font_outline_color"));
        label.AddThemeConstantOverride(
            "outline_size", source.GetThemeConstant("outline_size"));
        label.AddThemeConstantOverride(
            "shadow_outline_size",
            source.GetThemeConstant("shadow_outline_size"));
        return label;
    }

    private static void SetControlButtonState(
        Button input, bool enabled, bool highlighted = false)
    {
        input.Disabled = !enabled;
        if (input.GetParent()?.GetNodeOrNull<CanvasItem>("NativeVisual") is { } visual)
        {
            visual.SelfModulate = highlighted
                ? Colors.White
                : enabled
                    ? new Color(0.86f, 0.86f, 0.86f, 1)
                    : new Color(0.52f, 0.52f, 0.52f, 0.85f);
            if (visual.GetNodeOrNull<Control>(
                    "NativeSelectedOutline") is { } selectedOutline)
                selectedOutline.Visible = highlighted;
        }
    }

    private static void AnimateNativeButton(Control visual, float scale, double seconds)
    {
        var tween = visual.CreateTween();
        tween.TweenProperty(visual, "scale", Vector2.One * scale, seconds);
    }

    private static void SetMouseFilterRecursive(
        Control control, Control.MouseFilterEnum mouseFilter)
    {
        control.MouseFilter = mouseFilter;
        foreach (var child in control.GetChildren())
            if (child is Control childControl)
                SetMouseFilterRecursive(childControl, mouseFilter);
    }

    private static void ResetMinimumSizeRecursive(Control control)
    {
        control.CustomMinimumSize = Vector2.Zero;
        foreach (var child in control.GetChildren())
            if (child is Control childControl)
                ResetMinimumSizeRecursive(childControl);
    }

    private sealed record NativeControlButton(
        Control Root, Button Input, MegaLabel Label);
    private sealed record NativeControlDisplay(
        Control Root, Control Visual, MegaLabel Label);

    private static List<CardModel> Sort(IEnumerable<CardModel> cards) =>
        cards.OrderBy(card => card.Rarity)
            .ThenBy(card => card.Id.Entry, StringComparer.Ordinal)
            .ToList();

    private static void SetRegion(
        Control control, float left, float top, float right, float bottom)
    {
        control.AnchorLeft = left;
        control.AnchorTop = top;
        control.AnchorRight = right;
        control.AnchorBottom = bottom;
        control.OffsetLeft = 0;
        control.OffsetTop = 0;
        control.OffsetRight = 0;
        control.OffsetBottom = 0;
    }

}
