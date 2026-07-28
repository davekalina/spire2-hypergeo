using Godot;
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

    private static readonly Color PanelColor = new(0.182f, 0.2604f, 0.28f, 0.501961f);
    private static readonly Color ShadowColor = new(0.2346f, 0.325947f, 0.34f, 1f);
    private static readonly Color HeaderColor = new(0.937255f, 0.784314f, 0.317647f, 1f);

    private const string SortButtonScene = "screens/card_library/library_sort_button";
    private const string TickboxScene = "screens/card_library/card_library_tickbox";
    private const string TypeTickboxScene = "screens/card_library/card_type_tickbox";
    private const string CardLibraryScene = "screens/card_library/card_library";

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
    /// A named value with a stepper: the label reads left, the controls sit right.
    /// Tighter than a caption over a centred row, which matters when several stack up.
    /// </summary>
    public ShelfStepper AddStepperRow(VBoxContainer parent, string label)
    {
        var row = new HBoxContainer { Name = $"{label}Stepper" };
        row.AddThemeConstantOverride("separation", 4);

        var caption = CreateText(label, 15);
        caption.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        caption.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
        caption.HorizontalAlignment = HorizontalAlignment.Left;
        caption.VerticalAlignment = VerticalAlignment.Center;
        row.AddChild(caption);

        var decrease = CreateButton("−", 36);
        var value = CreateDisplay(string.Empty, 46);
        var increase = CreateButton("+", 36);
        row.AddChild(decrease.Root);
        row.AddChild(value.Root);
        row.AddChild(increase.Root);

        parent.AddChild(row);
        return new ShelfStepper(row, decrease.Input, value.Label, increase.Input);
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
    /// A disabled button also leaves the focus graph, so controller travel steps over a
    /// stepper that has nothing left to give rather than stopping on it.
    /// </summary>
    public static void SetButtonState(Button input, bool enabled)
    {
        input.Disabled = !enabled;
        input.FocusMode = enabled
            ? Control.FocusModeEnum.All
            : Control.FocusModeEnum.None;
        if (input.GetParent()?.GetNodeOrNull<CanvasItem>("NativeVisual") is { } visual)
            visual.SelfModulate = enabled
                ? new Color(0.86f, 0.86f, 0.86f, 1)
                : new Color(0.52f, 0.52f, 0.52f, 0.85f);
    }

    private static void SetFocusOutline(Control visual, bool visible)
    {
        if (visual.GetNodeOrNull<Control>("NativeSelectedOutline") is { } outline)
            outline.Visible = visible;
    }

    private static void AnimateButton(Control visual, float scale, double seconds)
    {
        var tween = visual.CreateTween();
        tween.TweenProperty(visual, "scale", Vector2.One * scale, seconds);
    }

    internal sealed record ShelfModule(VBoxContainer Root, VBoxContainer Body);
    internal sealed record ShelfStepper(
        HBoxContainer Root, Button Decrease, MegaLabel Value, Button Increase);
    internal sealed record ShelfRow(HBoxContainer Root, MegaLabel Label, MegaLabel Value);
    internal sealed record ShelfButton(Control Root, Button Input, MegaLabel Label);
    internal sealed record ShelfDisplay(Control Root, Control Visual, MegaLabel Label);
}
