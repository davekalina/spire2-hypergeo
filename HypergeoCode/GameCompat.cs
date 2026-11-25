using System.Reflection;
using Godot;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Nodes.CommonUi;

namespace Hypergeo.HypergeoCode;

/// <summary>
/// The places where the game's recent builds disagree, answered for all of them, so one
/// DLL loads on 0.111 (main) and 0.114 (the current beta) alike, and on 0.113 (the
/// previous beta) too.
///
/// Each member here exists because a direct call compiled against one build fails on
/// the other. That failure is not a compile error anyone sees. It is a
/// <c>MissingMethodException</c> thrown the first time the calling method runs, inside
/// <see cref="Guard" />, so all anyone sees is a feature that quietly stopped working.
/// Anything that differs between builds goes through here, by name, and takes whichever
/// form the running game has.
/// </summary>
internal static class GameCompat
{
    // ---- HoverTip.Id ------------------------------------------------------------------
    //
    // 0.111's HoverTip.Id has a setter; 0.113 made it get-only. A compiled `tip.Id = …`
    // calls set_Id, which no longer exists on 0.113. Both builds keep it as an
    // auto-property, so the compiler-generated backing field is there either way; the
    // setter is preferred while it exists.

    private static readonly MethodInfo? TipIdSetter =
        typeof(HoverTip).GetProperty(nameof(HoverTip.Id))?.GetSetMethod(nonPublic: true);
    private static readonly FieldInfo? TipIdField =
        typeof(HoverTip).GetField($"<{nameof(HoverTip.Id)}>k__BackingField",
            BindingFlags.Instance | BindingFlags.NonPublic);

    /// <summary>
    /// Sets the Id on a boxed <see cref="HoverTip" />. The Id only decides which tips
    /// a tip list treats as duplicates, so when neither form is found the tip keeps the
    /// Id its constructor gave it and still shows.
    /// </summary>
    public static void SetHoverTipId(object boxedTip, string id)
    {
        if (TipIdSetter is not null)
            TipIdSetter.Invoke(boxedTip, [id]);
        else
            TipIdField?.SetValue(boxedTip, id);
    }

    // ---- Hotkey bindings --------------------------------------------------------------
    //
    // NHotkeyManager's binding methods have had three shapes:
    //   0.111  void Push…Binding(string hotkey, Action action)
    //          void Remove…Binding(string hotkey, Action action)
    //   0.113  void Push…Binding(string hotkey, Action action, long sequence = 0)
    //          long Remove…Binding(string hotkey, Action action)
    //   0.114  void Push…Binding(string hotkey, Node owner, Action action, long sequence = 0)
    //          long Remove…Binding(string hotkey, Node owner, Action action)
    // Optional parameters are filled in by the compiler, not the runtime, so a call
    // compiled against any one of them is a MissingMethodException on the others.
    //
    // On 0.114 a binding fires only while its owner is in the tree, visible, and not
    // behind an open capstone screen, and Remove matches on owner and action together.
    // So the owner is the node the shortcut belongs to, and Remove must be given the same
    // one. Builds without an owner parameter ignore it.

    private static readonly MethodInfo? PushReleased = Binding("PushHotkeyReleasedBinding");
    private static readonly MethodInfo? RemoveReleased = Binding("RemoveHotkeyReleasedBinding");

    public static void PushHotkeyReleasedBinding(
        NHotkeyManager hotkeys, string hotkey, Node owner, Action action) =>
        InvokeBinding(PushReleased, "PushHotkeyReleasedBinding", hotkeys, hotkey, owner, action);

    public static void RemoveHotkeyReleasedBinding(
        NHotkeyManager hotkeys, string hotkey, Node owner, Action action) =>
        InvokeBinding(RemoveReleased, "RemoveHotkeyReleasedBinding", hotkeys, hotkey, owner, action);

    /// <summary>
    /// The public overload whose parameters start <c>(string, Action, …)</c> or
    /// <c>(string, Node, Action, …)</c>, fewest parameters first.
    /// </summary>
    private static MethodInfo? Binding(string name) =>
        typeof(NHotkeyManager)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Where(m => m.Name == name && IsBindingShape(m.GetParameters()))
            .OrderBy(m => m.GetParameters().Length)
            .FirstOrDefault();

    private static bool IsBindingShape(ParameterInfo[] parameters) =>
        parameters.Length >= 2 &&
        parameters[0].ParameterType == typeof(string) &&
        (parameters[1].ParameterType == typeof(Action) ||
         parameters.Length >= 3 &&
         parameters[1].ParameterType == typeof(Node) &&
         parameters[2].ParameterType == typeof(Action));

    /// <summary>
    /// Arguments are filled by type: the string is the hotkey, the Node the owner, the
    /// Action the action, and anything else gets its type's default. For the sequence
    /// that is <c>noSequence</c> (0), meaning "a fresh binding at top priority", which is
    /// exactly what the two-argument call meant before the parameter existed.
    /// </summary>
    private static void InvokeBinding(MethodInfo? method, string name, NHotkeyManager hotkeys,
        string hotkey, Node owner, Action action)
    {
        if (method is null)
            throw new MissingMethodException(nameof(NHotkeyManager), name);
        var parameters = method.GetParameters();
        var args = new object?[parameters.Length];
        for (var i = 0; i < parameters.Length; i++)
        {
            var type = parameters[i].ParameterType;
            args[i] =
                type == typeof(string) ? hotkey :
                type == typeof(Node) ? owner :
                type == typeof(Action) ? action :
                type.IsValueType ? Activator.CreateInstance(type) : null;
        }
        method.Invoke(hotkeys, args);
    }
}

/// <summary>
/// Game art the mod borrows, under every path the supported builds keep it at.
///
/// 0.113 renamed the <c>compressed</c> atlas to <c>compressed_atlas</c>, so
/// <c>compressed.sprites/back_button_x.tres</c> became
/// <c>compressed_atlas.sprites/back_button_x.tres</c>. A path that does not exist loads
/// as null, which leaves the search field's clear button blank, so each texture lists
/// the newest path first and the older ones after it.
///
/// Only the path is remembered. The texture is not: the engine's resource cache frees
/// textures on scene teardown, and a held wrapper would come back disposed.
/// <c>CacheMode.Reuse</c> keeps the repeat loads cheap.
/// </summary>
internal sealed class GameArt(params string[] paths)
{
    /// <summary>The ✕ on the card library's clear-search button.</summary>
    public static readonly GameArt BackButtonX = new(
        "res://images/atlases/compressed_atlas.sprites/back_button_x.tres",
        "res://images/atlases/compressed.sprites/back_button_x.tres");

    private string? _resolved;

    public Texture2D? Load()
    {
        _resolved ??= paths.FirstOrDefault(p => ResourceLoader.Exists(p)) ?? paths[0];
        return ResourceLoader.Load<Texture2D>(_resolved, null, ResourceLoader.CacheMode.Reuse);
    }
}
