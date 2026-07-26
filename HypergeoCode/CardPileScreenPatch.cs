using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Nodes.Cards.Holders;
using MegaCrit.Sts2.Core.Nodes.Screens;

namespace Hypergeo.HypergeoCode;

[HarmonyPatch(typeof(NCardPileScreen))]
internal static class CardPileScreenPatch
{
    private static readonly Dictionary<NCardPileScreen, DrawOddsView> Views = [];

    [HarmonyPostfix]
    [HarmonyPatch(nameof(NCardPileScreen._Ready))]
    private static void AfterReady(NCardPileScreen __instance)
    {
        if (AllCardsPileScreenCoordinator.TryAttach(__instance))
            return;
        if (__instance.Pile.Type is not (PileType.Draw or PileType.Discard) ||
            Views.ContainsKey(__instance))
            return;
        var view = new DrawOddsView(__instance);
        Views.Add(__instance, view);
        view.Attach();
    }

    [HarmonyPrefix]
    [HarmonyPatch(nameof(NCardPileScreen._ExitTree))]
    private static void BeforeExitTree(NCardPileScreen __instance)
    {
        AllCardsPileScreenCoordinator.Detach(__instance);
        if (Views.Remove(__instance, out var view))
            view.Dispose();
    }

    public static bool TryShowHoverTips(NCardHolder holder)
    {
        if (AllCardsPileScreenCoordinator.TryShowHoverTips(holder))
            return true;
        foreach (var view in Views.Values)
            if (view.TryShowHoverTips(holder))
                return true;
        return false;
    }
}

[HarmonyPatch(typeof(NCardHolder), "CreateHoverTips")]
internal static class DrawOddsHoverTipPatch
{
    [HarmonyPrefix]
    private static bool BeforeCreateHoverTips(NCardHolder __instance) =>
        !CardPileScreenPatch.TryShowHoverTips(__instance);
}
