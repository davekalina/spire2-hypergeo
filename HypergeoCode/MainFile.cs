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
    public const string Version = "v1.0.1";

    public static MegaCrit.Sts2.Core.Logging.Logger Logger { get; } =
        new(ModId, MegaCrit.Sts2.Core.Logging.LogType.Generic);

    public static void Initialize()
    {
        // PatchAll is deliberately not guarded. A patch whose target has moved means
        // this build does not fit this game, and the loader's own "failed to
        // initialize" is the honest report — better than a mod that loads and then
        // misbehaves in ways the player has to diagnose. Everything after it is
        // guarded, because a shortcut that cannot be registered is worth losing on its
        // own rather than taking the rest of the mod with it.
        new Harmony(ModId).PatchAll();
        Guard.Run("Registering the All Cards shortcut", AllCardsHotkey.Install);
        Logger.Info($"{ModName} {Version} initialized.");
    }
}
