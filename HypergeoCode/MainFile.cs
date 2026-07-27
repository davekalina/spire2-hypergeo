using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Modding;

namespace Hypergeo.HypergeoCode;

[ModInitializer(nameof(Initialize))]
public partial class MainFile : Node
{
    public const string ModId = "Hypergeo";
    public const string ModName = "Hypergeometric Draw Odds";

    /// <summary>Keep in sync with the <c>version</c> field in Hypergeo.json.</summary>
    public const string Version = "v0.7.0";

    public static MegaCrit.Sts2.Core.Logging.Logger Logger { get; } =
        new(ModId, MegaCrit.Sts2.Core.Logging.LogType.Generic);

    public static void Initialize()
    {
        new Harmony(ModId).PatchAll();
        AllCardsHotkey.Install();
        Logger.Info($"{ModName} {Version} initialized.");
    }
}
