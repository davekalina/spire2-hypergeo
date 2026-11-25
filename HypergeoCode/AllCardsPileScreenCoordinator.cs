using MegaCrit.Sts2.Core.ControllerInput;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Nodes.Cards.Holders;
using MegaCrit.Sts2.Core.Nodes.Screens;

namespace Hypergeo.HypergeoCode;

internal static class AllCardsPileScreenCoordinator
{
    /// <summary>Names the screen so an open one can be recognised as ours.</summary>
    public const string ScreenName = "NCardPileScreen-AllCards";

    private static readonly Dictionary<NCardPileScreen, AllCardsPileScreenView> Views = [];
    private static Player? _openingPlayer;
    private static bool _isOpening;

    public static void OpenForLocalPlayer()
    {
        var players = MegaCrit.Sts2.Core.Combat.CombatManager.Instance
            .DebugOnlyGetState()?.Players;
        var player = players == null ? null : LocalPlayerResolver.Resolve(players);
        if (player?.PlayerCombatState != null)
            Open(player);
    }

    public static void Open(Player player)
    {
        _openingPlayer = player;
        _isOpening = true;
        try
        {
            // Listing the shortcut alongside back makes the screen close on the same
            // key that opened it, through the game's own close-hotkey mechanism.
            NCardPileScreen.ShowScreen(
                player.PlayerCombatState!.DrawPile, CloseHotkeys());
        }
        finally
        {
            _isOpening = false;
            _openingPlayer = null;
        }
    }

    /// <summary>
    /// When the Draw Pile button opens All Cards, its key closes it too, the way the game
    /// lists a pile button's own key among its screen's close keys. The button cannot do
    /// it itself: from 0.114 nothing behind an open screen receives a shortcut.
    /// </summary>
    private static string[] CloseHotkeys()
    {
        string[] keys = [MegaInput.pauseAndBack.ToString(), AllCardsHotkey.Action];
        return HypergeoSettings.DrawPileTakeover
            ? [.. keys, MegaInput.viewDrawPile.ToString()]
            : keys;
    }

    public static bool TryAttach(NCardPileScreen screen)
    {
        if (!_isOpening || _openingPlayer == null)
            return false;
        var view = new AllCardsPileScreenView(screen, _openingPlayer);
        Views.Add(screen, view);
        view.Attach();
        return true;
    }

    public static void Detach(NCardPileScreen screen)
    {
        if (Views.Remove(screen, out var view))
            view.Dispose();
    }

    public static bool TryShowHoverTips(NCardHolder holder)
    {
        foreach (var view in Views.Values)
            if (view.TryShowHoverTips(holder))
                return true;
        return false;
    }
}
