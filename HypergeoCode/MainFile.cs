using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Modding;

namespace Hypergeo.HypergeoCode;

[ModInitializer(nameof(Initialize))]
public partial class MainFile : Node
{
    public const string ModId = "Hypergeo";
    public static MegaCrit.Sts2.Core.Logging.Logger Logger { get; } =
        new(ModId, MegaCrit.Sts2.Core.Logging.LogType.Generic);

    public static void Initialize()
    {
        new Harmony(ModId).PatchAll();
        Logger.Info("Hypergeometric Draw Odds v0.6.0 initialized.");
    }
}
