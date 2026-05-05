using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Modding;

namespace ExitToDesktop;

[ModInitializer(nameof(Initialize))]
public partial class MainFile : Node
{
    public const string ModId = "ExitToDesktop";

    public static MegaCrit.Sts2.Core.Logging.Logger Logger { get; } =
        new(ModId, MegaCrit.Sts2.Core.Logging.LogType.Generic);

    public static void Initialize()
    {
        Logger.Info("ExitToDesktop: Initializing...");

        Harmony harmony = new(ModId);

        foreach (var type in typeof(MainFile).Assembly.GetTypes())
        {
            if (type.GetCustomAttributes(typeof(HarmonyPatch), false).Length == 0) continue;
            try { harmony.CreateClassProcessor(type).Patch(); }
            catch (Exception e) { Logger.Warn($"ExitToDesktop: Patch {type.Name} skipped — {e.Message}"); }
        }

        Logger.Info("ExitToDesktop: Initialized.");
    }
}
