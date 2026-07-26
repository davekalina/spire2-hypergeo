using HarmonyLib;
using MegaCrit.Sts2.Core.Nodes.Combat;

namespace Hypergeo.HypergeoCode;

[HarmonyPatch(typeof(NCombatUi))]
internal static class AllCardsCombatPatch
{
    private static readonly Dictionary<NCombatUi, AllCardsScreenView> Views = [];

    [HarmonyPostfix]
    [HarmonyPatch(nameof(NCombatUi._Ready))]
    private static void AfterReady(NCombatUi __instance)
    {
        if (Views.ContainsKey(__instance))
            return;
        var view = new AllCardsScreenView(__instance);
        Views.Add(__instance, view);
        view.Attach();
    }

    [HarmonyPrefix]
    [HarmonyPatch(nameof(NCombatUi._ExitTree))]
    private static void BeforeExitTree(NCombatUi __instance)
    {
        if (Views.Remove(__instance, out var view))
            view.Dispose();
    }
}
