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
    private static void AfterReady(NCardPileScreen __instance) =>
        Guard.Run("Building the pile screen view", () =>
        {
            if (AllCardsPileScreenCoordinator.TryAttach(__instance))
                return;
            if (__instance.Pile.Type is not (PileType.Draw or PileType.Discard) ||
                Views.ContainsKey(__instance))
                return;
            var view = new DrawOddsView(__instance);
            Views.Add(__instance, view);
            view.Attach();
        });

    [HarmonyPrefix]
    [HarmonyPatch(nameof(NCardPileScreen._ExitTree))]
    private static void BeforeExitTree(NCardPileScreen __instance) =>
        Guard.Run("Tearing down the pile screen view", () =>
        {
            AllCardsPileScreenCoordinator.Detach(__instance);
            if (Views.Remove(__instance, out var view))
                view.Dispose();
        });

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
    /// <summary>
    /// Returning false suppresses the game's own hover tips in favour of the mod's, so
    /// a failure here has to fall back to true: better the native tip than none at all.
    /// </summary>
    [HarmonyPrefix]
    private static bool BeforeCreateHoverTips(NCardHolder __instance) =>
        Guard.Run(
            "Showing the draw chance hover tip",
            () => !CardPileScreenPatch.TryShowHoverTips(__instance),
            onFailure: true);
}
