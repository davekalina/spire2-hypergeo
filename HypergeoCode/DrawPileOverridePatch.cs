using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Screens;
using MegaCrit.Sts2.Core.Nodes.Screens.Capstones;

namespace Hypergeo.HypergeoCode;

/// <summary>
/// Points the Draw Pile button at the All Cards screen, when the player has asked for
/// that in Mod Settings.
///
/// This overrides what the button <em>does</em> rather than what it is bound to. The
/// game reaches this one method from every direction — a keyboard key, a controller
/// button, a Steam Input action, or a mouse click on the button itself — so overriding
/// it here covers all of them at once. Rewriting bindings instead would mean finding
/// and rewriting each input map separately, and would still leave the button's own
/// click behind.
///
/// It also leaves every binding alone. The Draw Pile keeps its key and its button, and
/// Settings keeps showing them; only the destination changes.
/// </summary>
[HarmonyPatch(typeof(NCombatCardPile), "OnRelease")]
internal static class DrawPileOverridePatch
{
    /// <summary>
    /// Returning false takes the button away from the game, so a failure here falls
    /// back to true: the player gets the draw pile they pressed for, rather than a
    /// button that does nothing.
    /// </summary>
    [HarmonyPrefix]
    private static bool BeforeOnRelease(NCombatCardPile __instance) => Guard.Run(
        "Opening All Cards from the Draw Pile button",
        () =>
        {
            // NDrawPileButton is the only pile this applies to, and OnRelease is
            // declared on the shared base, so the discard and exhaust piles have to be
            // let through.
            if (!HypergeoSettings.DrawPileTakeover || __instance is not NDrawPileButton)
                return true;
            if (!CombatManager.Instance.IsInProgress)
                return true;

            var capstone = NCapstoneContainer.Instance;
            if (capstone?.CurrentCapstoneScreen is NCardPileScreen open)
            {
                // Pressing it again closes, the way the pile buttons already behave.
                if (open.Name == AllCardsPileScreenCoordinator.ScreenName)
                    capstone.Close();
                return false;
            }
            AllCardsPileScreenCoordinator.OpenForLocalPlayer();
            return false;
        },
        onFailure: true);
}
