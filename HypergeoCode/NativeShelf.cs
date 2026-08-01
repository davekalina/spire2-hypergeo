using Godot;
using MegaCrit.Sts2.Core.ControllerInput;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.HoverTips;
using MegaCrit.Sts2.Core.Nodes.Screens.CardLibrary;
using MegaCrit.Sts2.addons.mega_text;

namespace Hypergeo.HypergeoCode;

/// <summary>
/// The Card Library sidebar, rebuilt for the All Cards screen.
///
/// Every measurement, colour, and widget here is taken from
/// <c>res://scenes/screens/card_library/card_library.tscn</c> so the shelf reads as
/// the same surface the game already uses for browsing a grid of cards: a 288 px
/// panel with modules stacked from the top and view toggles pinned to the bottom.
/// </summary>
internal sealed class NativeShelf : IDisposable
{
    /// <summary>Sidebar width. The card grid is inset by the same amount.</summary>
    public const float Width = 288f;

    private const float Margin = 16f;
    private const float ShadowWidth = 4f;
    private const float ModuleSeparation = 4f;
    private const float ModuleSpacing = 18f;
    private const float BodyIndent = 8f;
    private const float SelectedOutlineBleed = 5f;

    /// <summary>Sized for the longest toggle label, so none of them has to shrink.</summary>
    private const int ToggleFontSize = 17;

    private static readonly Color PanelColor = new(0.182f, 0.2604f, 0.28f, 0.501961f);
    private static readonly Color ShadowColor = new(0.2346f, 0.325947f, 0.34f, 1f);
    private static readonly Color HeaderColor = new(0.937255f, 0.784314f, 0.317647f, 1f);

    private const string SortButtonScene = "screens/card_library/library_sort_button";
    private const string TickboxScene = "screens/card_library/card_library_tickbox";
    private const string TypeTickboxScene = "screens/card_library/card_type_tickbox";
    private const string CardLibraryScene = "screens/card_library/card_library";
    private const string HoverTipScene = "ui/hover_tip";
    private const string MarkerTileScene =
        "screens/main_menu/compendium_bottom_button";

    private readonly MegaLabel _fontSource;
    private readonly TextureRect _buttonTextureSource;
    private readonly TextureRect _outlineTextureSource;
    private readonly List<Node> _templates = [];

    public NativeShelf()
    {
        var sortButton = SceneHelper.Instantiate<Control>(SortButtonScene);
        var typeTickbox = SceneHelper.Instantiate<Control>(TypeTickboxScene);
        _templates.Add(sortButton);
        _templates.Add(typeTickbox);
        _fontSource = sortButton.GetNode<MegaLabel>("%Label");
        _buttonTextureSource = sortButton.GetNode<TextureRect>("%ButtonImage");
        _outlineTextureSource = typeTickbox.GetNode<TextureRect>("%Outline");

        Root = BuildRoot(out var top, out var bottom);
        Top = top;
        Bottom = bottom;
    }

    /// <summary>The sidebar. Add it to the screen; it anchors itself to the left edge.</summary>
    public Control Root { get; }

    /// <summary>Modules stack downward from the top of the shelf.</summary>
    public VBoxContainer Top { get; }

    /// <summary>View toggles sit against the bottom of the shelf.</summary>
    public VBoxContainer Bottom { get; }

    public void Dispose()
    {
        foreach (var template in _templates)
            if (GodotObject.IsInstanceValid(template))
                template.Free();
        _templates.Clear();
    }

    private static Control BuildRoot(out VBoxContainer top, out VBoxContainer bottom)
    {
        var root = new Control
        {
            Name = "HypergeoShelf",
            AnchorLeft = 0,
            AnchorTop = 0,
            AnchorRight = 0,
            AnchorBottom = 1,
            OffsetLeft = 0,
            OffsetTop = 0,
            OffsetRight = Width,
            OffsetBottom = 0,
            GrowVertical = Control.GrowDirection.Both,
        };

        var panel = new ColorRect
        {
            Name = "Panel",
            Color = PanelColor,
            CustomMinimumSize = new Vector2(Width, 0),
        };
        panel.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        root.AddChild(panel);

        var shadow = new ColorRect
        {
            Name = "Shadow",
            Color = ShadowColor,
            ShowBehindParent = true,
            CustomMinimumSize = new Vector2(ShadowWidth, 0),
            AnchorLeft = 1,
            AnchorTop = 0,
            AnchorRight = 1,
            AnchorBottom = 1,
            OffsetLeft = 0,
            OffsetTop = 0,
            OffsetRight = ShadowWidth,
            OffsetBottom = 0,
            GrowHorizontal = Control.GrowDirection.End,
            GrowVertical = Control.GrowDirection.Both,
        };
        panel.AddChild(shadow);

        var margin = new MarginContainer { Name = "MarginContainer" };
        margin.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        margin.AddThemeConstantOverride("margin_left", (int)Margin);
        margin.AddThemeConstantOverride("margin_top", (int)Margin);
        margin.AddThemeConstantOverride("margin_right", (int)Margin);
        margin.AddThemeConstantOverride("margin_bottom", (int)Margin);
        root.AddChild(margin);

        top = new VBoxContainer { Name = "TopVBox" };
        top.AddThemeConstantOverride("separation", 0);
        margin.AddChild(top);

        bottom = new VBoxContainer
        {
            Name = "BottomVBox",
            SizeFlagsVertical = Control.SizeFlags.ShrinkEnd,
        };
        margin.AddChild(bottom);
        return root;
    }

    /// <summary>
    /// A titled module: the Card Library's sorter bar over an indented body, followed
    /// by the same 18 px gap the library leaves between its own modules.
    /// </summary>
    public ShelfModule AddModule(VBoxContainer parent, string title)
    {
        var module = new VBoxContainer { Name = $"{title}Module" };
        module.AddThemeConstantOverride("separation", (int)ModuleSeparation);

        module.AddChild(CreateHeader(title));

        var indent = new MarginContainer();
        indent.AddThemeConstantOverride("margin_left", (int)BodyIndent);
        var body = new VBoxContainer { Name = "Body" };
        body.AddThemeConstantOverride("separation", (int)ModuleSeparation);
        indent.AddChild(body);
        module.AddChild(indent);

        module.AddChild(new Control
        {
            Name = "Spacer",
            CustomMinimumSize = new Vector2(0, ModuleSpacing),
        });

        parent.AddChild(module);
        return new ShelfModule(module, body);
    }

    private Control CreateHeader(string title)
    {
        var header = SceneHelper.Instantiate<NCardViewSortButton>(SortButtonScene);
        header.Name = $"{title}Header";
        header.FocusMode = Control.FocusModeEnum.None;
        header.MouseFilter = Control.MouseFilterEnum.Ignore;
        // The bar is a heading here, not a control: no sort direction to show.
        header.GetNode<Control>("%Image").Hide();
        var label = header.GetNode<MegaLabel>("%Label");
        label.Text = title;
        label.AddThemeColorOverride("font_color", HeaderColor);
        return header;
    }

    /// <summary>A label/value row. Labels read left, values align to the right edge.</summary>
    public ShelfRow AddRow(VBoxContainer parent, string label, float valueWidth = 88f)
    {
        var row = new HBoxContainer { Name = "Row" };
        row.AddThemeConstantOverride("separation", 6);

        var labelNode = CreateText(label, 19);
        labelNode.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        labelNode.HorizontalAlignment = HorizontalAlignment.Left;
        labelNode.VerticalAlignment = VerticalAlignment.Center;
        row.AddChild(labelNode);

        var valueNode = CreateText(string.Empty, 19);
        valueNode.CustomMinimumSize = new Vector2(valueWidth, 0);
        valueNode.HorizontalAlignment = HorizontalAlignment.Right;
        valueNode.VerticalAlignment = VerticalAlignment.Center;
        row.AddChild(valueNode);

        parent.AddChild(row);
        return new ShelfRow(row, labelNode, valueNode);
    }

    /// <summary>A centred caption line inside a module body.</summary>
    public MegaLabel AddCaption(VBoxContainer parent, string text, int fontSize = 17)
    {
        var label = CreateText(text, fontSize);
        label.HorizontalAlignment = HorizontalAlignment.Center;
        label.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        // Long text wraps rather than running off the edge of a 288 px shelf.
        label.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        parent.AddChild(label);
        return label;
    }

    /// <summary>
    /// The Card Library's search bar, text field and clear button together.
    ///
    /// It is built inline in the library's own scene rather than being a scene of its
    /// own, so the only way to have one is to instantiate the library and take a copy.
    /// The library is never added to the tree, so none of it runs.
    /// </summary>
    public NSearchBar AddSearchBar(VBoxContainer parent)
    {
        var cardLibrary = SceneHelper.Instantiate<Control>(CardLibraryScene);
        var searchBar = (NSearchBar)cardLibrary.GetNode<NSearchBar>("%SearchBar").Duplicate();
        cardLibrary.Free();

        searchBar.Name = "HypergeoSearchBar";
        searchBar.CustomMinimumSize = new Vector2(0, 48);
        parent.AddChild(searchBar);
        parent.AddChild(new Control
        {
            Name = "SearchSpacer",
            CustomMinimumSize = new Vector2(0, 12),
        });
        return searchBar;
    }

    /// <summary>
    /// A named row of controls: the name reads left, the controls sit against the right
    /// edge. Tighter than a caption over a centred row, which matters when several stack
    /// up, and it keeps the name beside the number it belongs to.
    ///
    /// The caller fills <c>Controls</c>, so this is the one shape used for both the
    /// calculator's steppers and the combat view's draw and hits rows.
    /// </summary>
    public ShelfControlRow AddControlRow(
        VBoxContainer parent,
        string label,
        int fontSize = 19,
        int separation = 6,
        string? hoverDescription = null)
    {
        var row = new HBoxContainer
        {
            Name = $"{label}Row",
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
        };
        row.AddThemeConstantOverride("separation", separation);

        var caption = CreateText(label, fontSize);
        caption.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        caption.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
        caption.HorizontalAlignment = HorizontalAlignment.Left;
        caption.VerticalAlignment = VerticalAlignment.Center;
        if (hoverDescription != null)
        {
            // CreateText makes labels ignore the mouse, which a hovered one cannot.
            caption.MouseFilter = Control.MouseFilterEnum.Stop;
            caption.MouseEntered += () => NHoverTipSet.CreateAndShow(
                caption,
                NativeHoverTip.Create(
                    label, hoverDescription, $"HypergeoCalculator:{label}"),
                HoverTipAlignment.Right);
            caption.MouseExited += () => NHoverTipSet.Remove(caption);
        }
        row.AddChild(caption);

        // Anything qualifying the row goes under the controls rather than under the
        // whole shelf, so it reads as belonging to the number it describes. The column
        // shrinks to its widest child, which is the run of controls: text added here
        // wraps and centres on them rather than stretching back under the name.
        var column = new VBoxContainer
        {
            Name = "Column",
            SizeFlagsHorizontal = Control.SizeFlags.ShrinkEnd,
        };
        column.AddThemeConstantOverride("separation", 2);
        var controls = new HBoxContainer { Name = "Controls" };
        controls.AddThemeConstantOverride("separation", separation);
        column.AddChild(controls);
        row.AddChild(column);

        parent.AddChild(row);
        return new ShelfControlRow(row, caption, controls, column);
    }

    /// <summary>A named value with a stepper, at the calculator's smaller size.</summary>
    public ShelfStepper AddStepperRow(
        VBoxContainer parent, string label, string? hoverDescription = null)
    {
        var row = AddControlRow(parent, label, 15, 4, hoverDescription);

        var decrease = CreateButton("−", 36);
        var value = CreateDisplay(string.Empty, 46);
        var increase = CreateButton("+", 36);
        row.Controls.AddChild(decrease.Root);
        row.Controls.AddChild(value.Root);
        row.Controls.AddChild(increase.Root);

        return new ShelfStepper(row.Root, decrease.Input, value.Label, increase.Input);
    }

    /// <summary>
    /// A card-sized marker separating one run of the grid from the next.
    ///
    /// The section name sits on the same bar the shelf uses for its own headings, so a
    /// separator reads as a heading rather than as loose text over the backdrop, and the
    /// grid and the shelf are visibly labelled by the same hand. What the section holds,
    /// and an arrow into it, sit underneath.
    /// </summary>
    public ShelfMarker CreateSectionMarker()
    {
        var root = new Control { MouseFilter = Control.MouseFilterEnum.Ignore };

        // The Compendium's bottom tile, borrowed for its plaque: a separator is a label
        // for the run of cards after it, and this is a frame the game already uses to
        // put a name under a picture.
        //
        // It is a button in the main menu and a label here, so its own text and icon
        // stay hidden and it takes no focus. Its BgPanel fills the root exactly, which
        // is why the heading can be centred on the root and land on the art — the hover
        // tip panel this replaced set negative margins that put its frame somewhere
        // else entirely, and no amount of arithmetic against it centred the text.
        var box = SceneHelper.Instantiate<Control>(MarkerTileScene);
        box.Name = "MarkerBox";
        box.FocusMode = Control.FocusModeEnum.None;
        box.GetNodeOrNull<Control>("Label")?.Hide();
        box.GetNodeOrNull<Control>("Icon")?.Hide();
        root.AddChild(box);

        var heading = CreateText(string.Empty, 22);
        heading.Name = "MarkerHeading";
        heading.AddThemeColorOverride("font_color", HeaderColor);
        heading.HorizontalAlignment = HorizontalAlignment.Center;
        heading.VerticalAlignment = VerticalAlignment.Center;
        // No wrapping. With it on, the label's minimum width collapses to a single
        // pixel, and its minimum height becomes the height of the text wrapped one
        // character per line — 306 px for a one-word heading. A control cannot be
        // smaller than its minimum, so that height won every attempt to give the label
        // the frame's 240, and the text centred in a box far taller than the one drawn.
        heading.AutowrapMode = TextServer.AutowrapMode.Off;
        heading.MouseFilter = Control.MouseFilterEnum.Ignore;
        // The heading goes in a frame of its own, anchored to fill it, rather than
        // being given a rect directly: a MegaLabel does not keep an assigned height —
        // it reported 306 px tall after being set to 240 — and centring text in a box
        // taller than the one drawn is what dropped it below the middle. Anchors are
        // maintained by Godot against the parent's rect, so the label cannot drift
        // from the frame no matter what it decides its own height should be.
        var headingFrame = new Control
        {
            Name = "MarkerHeadingFrame",
            MouseFilter = Control.MouseFilterEnum.Ignore,
            ClipContents = true,
        };
        root.AddChild(headingFrame);
        headingFrame.AddChild(heading);
        heading.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);

        var marker = new ShelfMarker
        {
            Root = root,
            Box = box,
            Heading = heading,
            HeadingFrame = headingFrame,
        };
        // Pass, not Stop: the marker sits in the card grid, which reads mouse events of
        // its own to drag-scroll, and swallowing them would make it a dead patch.
        box.MouseFilter = Control.MouseFilterEnum.Pass;
        // A heading may be broken over two lines to sit better in the box; the tip
        // wants the name as one line.
        box.MouseEntered += () => NHoverTipSet.CreateAndShow(
            box,
            NativeHoverTip.Create(
                marker.Heading.Text.Replace('\n', ' '),
                marker.Description,
                $"HypergeoSection:{marker.Heading.Text.Replace('\n', ' ')}"),
            HoverTipAlignment.Right);
        box.MouseExited += () => NHoverTipSet.Remove(box);
        return marker;
    }

    /// <summary>A wrapped note line, for text that will not fit one row.</summary>
    public MegaLabel AddNote(VBoxContainer parent, string text, int fontSize = 15)
    {
        var label = CreateText(text, fontSize);
        label.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        label.HorizontalAlignment = HorizontalAlignment.Center;
        label.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        parent.AddChild(label);
        return label;
    }

    /// <summary>A full-width row, unlike the centred rows of shelf controls.</summary>
    public static HBoxContainer CreateFullWidthRow(int separation = 6)
    {
        var row = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        row.AddThemeConstantOverride("separation", separation);
        return row;
    }

    /// <summary>The Card Library's bottom-of-sidebar view toggle.</summary>
    public NLibraryStatTickbox AddToggle(VBoxContainer parent, string label, bool ticked)
    {
        var tickbox = SceneHelper.Instantiate<NLibraryStatTickbox>(TickboxScene);
        tickbox.Name = $"{label.Replace(" ", string.Empty)}Toggle";
        // The scene's focus neighbours point at nodes that only exist inside the
        // Card Library. Left alone they resolve to nothing when focus moves.
        tickbox.FocusNeighborTop = new NodePath();
        tickbox.FocusNeighborBottom = new NodePath();
        tickbox.CustomMinimumSize = new Vector2(0, 42);
        // SetLabel needs the node references _Ready builds, so wait for them.
        tickbox.Ready += () =>
        {
            // The native label shrinks whatever will not fit, which leaves toggles at
            // different sizes beside each other. Capping the size low enough for the
            // longest of them means none has to shrink and they all match.
            if (tickbox.GetNodeOrNull<MegaLabel>("Label") is { } text)
                text.MaxFontSize = ToggleFontSize;
            tickbox.SetLabel(label);
            tickbox.IsTicked = ticked;
        };
        parent.AddChild(tickbox);
        return tickbox;
    }

    /// <summary>A horizontal row of shelf controls, centred like the library's togglers.</summary>
    public static HBoxContainer CreateControlRow(int separation = 6)
    {
        var row = new HBoxContainer
        {
            Alignment = BoxContainer.AlignmentMode.Center,
            SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter,
        };
        row.AddThemeConstantOverride("separation", separation);
        return row;
    }

    public ShelfButton CreateButton(
        string text,
        float width,
        string? hoverDescription = null,
        string? hoverTitle = null)
    {
        var display = CreateDisplay(text, width);
        var input = new Button
        {
            Flat = true,
            // Focusable, so a controller can reach the shelf at all. The game's own
            // focus travel is Godot's, so joining the focus graph is the whole trick.
            FocusMode = Control.FocusModeEnum.All,
            MouseFilter = Control.MouseFilterEnum.Stop,
        };
        input.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        // The default focus box is a grey rectangle that looks nothing like the game.
        input.AddThemeStyleboxOverride("focus", new StyleBoxEmpty());
        // A Godot button fires on ui_accept, which the game binds to E and the north
        // face button. Confirm in this game is select — Enter and the south face button
        // — the action every native control acts on, so this one has to as well.
        input.GuiInput += inputEvent =>
        {
            if (!inputEvent.IsActionReleased(MegaInput.select))
                return;
            input.AcceptEvent();
            if (!input.Disabled)
                input.EmitSignal(BaseButton.SignalName.Pressed);
        };
        input.MouseEntered += () => AnimateButton(display.Visual, 1.04f, 0.06);
        input.MouseExited += () => AnimateButton(display.Visual, 1f, 0.16);
        // Focus wears the same outline the native tickboxes use for selection, which
        // is otherwise unused now, plus the scale nudge NCardViewSortButton gives.
        input.FocusEntered += () =>
        {
            SetFocusOutline(display.Visual, visible: true);
            AnimateButton(display.Visual, 1.05f, 0.06);
        };
        input.FocusExited += () =>
        {
            SetFocusOutline(display.Visual, visible: false);
            AnimateButton(display.Visual, 1f, 0.16);
        };
        if (hoverDescription != null)
        {
            input.MouseEntered += () => NHoverTipSet.CreateAndShow(
                input,
                NativeHoverTip.Create(
                    hoverTitle ?? text,
                    hoverDescription,
                    $"HypergeoShelf:{hoverTitle ?? text}"),
                HoverTipAlignment.Right);
            input.MouseExited += () => NHoverTipSet.Remove(input);
        }
        display.Root.AddChild(input);
        return new ShelfButton(display.Root, input, display.Label);
    }

    public ShelfDisplay CreateDisplay(string text, float width)
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

        var background = new TextureRect
        {
            Name = "NativeBackground",
            Texture = _buttonTextureSource.Texture,
            Material = _buttonTextureSource.Material,
            Modulate = _buttonTextureSource.Modulate,
            SelfModulate = _buttonTextureSource.SelfModulate,
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
        var label = CreateText(text, fontSize);
        label.Name = "NativeLabel";
        label.HorizontalAlignment = HorizontalAlignment.Center;
        label.VerticalAlignment = VerticalAlignment.Center;
        visual.AddChild(label);

        var selectedOutline = new NinePatchRect
        {
            Name = "NativeSelectedOutline",
            Texture = _outlineTextureSource.Texture,
            Material = _outlineTextureSource.Material,
            Modulate = _outlineTextureSource.Modulate,
            SelfModulate = _outlineTextureSource.SelfModulate,
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
            visual.PivotOffset = visual.Size * 0.5f;
            background.Position = Vector2.Zero;
            background.Size = root.Size;
            label.Position = Vector2.Zero;
            label.Size = root.Size;
            selectedOutline.Position = new Vector2(
                -SelectedOutlineBleed, -SelectedOutlineBleed);
            selectedOutline.Size = root.Size + Vector2.One * SelectedOutlineBleed * 2f;
        };
        root.AddChild(visual);
        return new ShelfDisplay(root, visual, label);
    }

    /// <summary>A label in the game's shelf font, sized and coloured like the library's.</summary>
    public MegaLabel CreateText(string text, int fontSize)
    {
        var label = new MegaLabel
        {
            AutoSizeEnabled = false,
            Text = text,
            Scale = Vector2.One,
            Rotation = 0,
            CustomMinimumSize = Vector2.Zero,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        label.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.TopLeft);
        label.Position = Vector2.Zero;
        label.PivotOffset = Vector2.Zero;
        label.AddThemeFontOverride("font", _fontSource.GetThemeFont("font"));
        label.AddThemeFontSizeOverride("font_size", fontSize);
        label.AddThemeColorOverride(
            "font_color", _fontSource.GetThemeColor("font_color"));
        label.AddThemeColorOverride(
            "font_outline_color", _fontSource.GetThemeColor("font_outline_color"));
        label.AddThemeConstantOverride(
            "outline_size", _fontSource.GetThemeConstant("outline_size"));
        label.AddThemeConstantOverride(
            "shadow_outline_size", _fontSource.GetThemeConstant("shadow_outline_size"));
        return label;
    }

    /// <summary>
    /// Wire an explicit focus chain down the shelf, row by row.
    ///
    /// Godot's automatic search cannot be trusted here: it looks across the whole
    /// viewport, so a press towards the shelf finds the run's relic inventory sitting
    /// behind the screen rather than the shelf itself. Naming every neighbour keeps
    /// focus inside the screen. The edges point back at their own control, which parks
    /// focus rather than letting it escape; the caller overrides the right edge to hand
    /// focus back to the card grid.
    ///
    /// A neighbour that cannot take focus is not a dead end — Godot walks on in the same
    /// direction — so a disabled stepper in the chain is simply stepped over.
    /// </summary>
    public static void WireFocusRows(IReadOnlyList<IReadOnlyList<Control>> rows)
    {
        for (var index = 0; index < rows.Count; index++)
        {
            var row = rows[index];
            var above = rows[Math.Max(0, index - 1)];
            var below = rows[Math.Min(rows.Count - 1, index + 1)];
            for (var column = 0; column < row.Count; column++)
            {
                var control = row[column];
                control.FocusNeighborLeft = column > 0
                    ? row[column - 1].GetPath()
                    : control.GetPath();
                control.FocusNeighborRight = column < row.Count - 1
                    ? row[column + 1].GetPath()
                    : control.GetPath();
                control.FocusNeighborTop =
                    above[Math.Min(column, above.Count - 1)].GetPath();
                control.FocusNeighborBottom =
                    below[Math.Min(column, below.Count - 1)].GetPath();
            }
        }
    }

    /// <summary>
    /// A disabled button keeps its place in the focus graph. Dropping out of it would
    /// mean a whole row vanishing from controller travel whenever its controls happened
    /// to be spent — the Selection row disappears entirely before anything is selected —
    /// which reads as a section being skipped rather than as a control being inert.
    /// </summary>
    public static void SetButtonState(Button input, bool enabled)
    {
        input.Disabled = !enabled;
        if (input.GetParent()?.GetNodeOrNull<CanvasItem>("NativeVisual") is { } visual)
            SetVisualState(visual, enabled);
    }

    /// <summary>
    /// Dim a read-only display to match the disabled buttons beside it. Shares its
    /// colours with <see cref="SetButtonState" /> so a stepper greys out as one piece.
    /// </summary>
    public static void SetVisualState(CanvasItem visual, bool enabled) =>
        visual.SelfModulate = enabled
            ? new Color(0.86f, 0.86f, 0.86f, 1)
            : new Color(0.52f, 0.52f, 0.52f, 0.85f);

    private static void SetFocusOutline(Control visual, bool visible)
    {
        if (visual.GetNodeOrNull<Control>("NativeSelectedOutline") is not { } outline)
            return;
        // Size it here as well as on resize. Focus can arrive before the control has
        // ever been resized, and a zero-sized outline is an invisible one.
        outline.Position = new Vector2(-SelectedOutlineBleed, -SelectedOutlineBleed);
        outline.Size = visual.Size + Vector2.One * SelectedOutlineBleed * 2f;
        outline.Visible = visible;
    }

    private static void AnimateButton(Control visual, float scale, double seconds)
    {
        var tween = visual.CreateTween();
        tween.TweenProperty(visual, "scale", Vector2.One * scale, seconds);
    }

    internal sealed record ShelfModule(VBoxContainer Root, VBoxContainer Body);
    /// <summary>
    /// A grid separator. The description is read when the marker is hovered rather than
    /// captured up front, so a section can change what it holds without rebuilding.
    /// </summary>
    internal sealed class ShelfMarker
    {
        public required Control Root { get; init; }
        public required Control Box { get; init; }
        public required MegaLabel Heading { get; init; }

        /// <summary>Sized to the drawn frame; the heading is anchored to fill it.</summary>
        public required Control HeadingFrame { get; init; }
        public string Description { get; set; } = string.Empty;
    }
    internal sealed record ShelfStepper(
        HBoxContainer Root, Button Decrease, MegaLabel Value, Button Increase);
    internal sealed record ShelfControlRow(
        HBoxContainer Root,
        MegaLabel Label,
        HBoxContainer Controls,
        VBoxContainer Column);
    internal sealed record ShelfRow(HBoxContainer Root, MegaLabel Label, MegaLabel Value);
    internal sealed record ShelfButton(Control Root, Button Input, MegaLabel Label);
    internal sealed record ShelfDisplay(Control Root, Control Visual, MegaLabel Label);
}
