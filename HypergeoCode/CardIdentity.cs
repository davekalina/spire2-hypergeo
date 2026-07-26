using MegaCrit.Sts2.Core.Models;

namespace Hypergeo.HypergeoCode;

internal readonly record struct CardIdentity(
    string CardId,
    int UpgradeLevel,
    string? EnchantmentId,
    int EnchantmentAmount)
{
    public static CardIdentity From(CardModel card) => new(
        card.Id.Entry,
        card.CurrentUpgradeLevel,
        card.Enchantment?.Id.Entry,
        card.Enchantment?.Amount ?? 0);
}
