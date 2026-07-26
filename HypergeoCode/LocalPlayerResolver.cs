using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Platform;

namespace Hypergeo.HypergeoCode;

internal static class LocalPlayerResolver
{
    public static Player? Resolve(IEnumerable<Player> players)
    {
        var list = players.ToList();
        try
        {
            var localId = PlatformUtil.GetLocalPlayerId(PlatformUtil.PrimaryPlatform);
            return list.FirstOrDefault(player => player.NetId == localId) ?? list.FirstOrDefault();
        }
        catch
        {
            return list.Count == 1 ? list[0] : null;
        }
    }
}
