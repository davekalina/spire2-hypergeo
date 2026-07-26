using MegaCrit.Sts2.Core.ControllerInput;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Nodes.Cards.Holders;
using MegaCrit.Sts2.Core.Nodes.Screens;

namespace Hypergeo.HypergeoCode;

internal static class AllCardsPileScreenCoordinator
{
    private static readonly Dictionary<NCardPileScreen, AllCardsPileScreenView> Views = [];
    private static Player? _openingPlayer;
    private static bool _isOpening;

    public static void Open(Player player)
    {
        _openingPlayer = player;
        _isOpening = true;
        try
        {
            NCardPileScreen.ShowScreen(
                player.PlayerCombatState!.DrawPile,
                [MegaInput.pauseAndBack.ToString()]);
        }
        finally
        {
            _isOpening = false;
            _openingPlayer = null;
        }
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
