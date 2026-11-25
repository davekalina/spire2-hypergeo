using System.Reflection;
using HarmonyLib;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;

namespace Hypergeo.HypergeoCode;

/// <summary>
/// Keeps the cloned All Cards button from registering the Draw Pile's hotkeys.
///
/// The clone is a duplicate of the game's own <c>NDrawPileButton</c>, so on entering
/// the tree it registers the same key the Draw Pile does. The hotkey manager fires the
/// most recent binding, which was the clone's, and the clone has no pile to open — so
/// the Draw Pile key did nothing. On 0.111 that lasted until the real button next
/// re-registered on being re-enabled. From 0.114 a re-enabled button gets its old
/// priority back instead of a new one, so the clone kept the key for the whole combat.
///
/// Every registration path goes through <c>RegisterHotkeys</c>: the parameterless
/// form on 0.111, and on 0.114 the private <c>RegisterHotkeys(bool restorePriority)</c>
/// that the parameterless one forwards to. Every overload is patched, so both builds
/// are covered. The clone's own shortcut is pushed separately and is not affected.
/// </summary>
[HarmonyPatch]
internal static class CloneHotkeysPatch
{
    private static readonly HashSet<NButton> Clones = [];

    public static void Register(NButton clone) => Clones.Add(clone);

    public static void Forget(NButton clone) => Clones.Remove(clone);

    [HarmonyTargetMethods]
    private static IEnumerable<MethodBase> TargetMethods() =>
        AccessTools.GetDeclaredMethods(typeof(NButton)).Where(m => m.Name == "RegisterHotkeys");

    /// <summary>A failure lets the registration through, as the game would have it.</summary>
    [HarmonyPrefix]
    private static bool BeforeRegisterHotkeys(NButton __instance) => Guard.Run(
        "Keeping the All Cards button off the Draw Pile key",
        () => !Clones.Contains(__instance),
        onFailure: true);
}
