using System.Reflection;
using HarmonyLib;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Modding;
using STS2RitsuLib;
using STS2RitsuLib.Interop;

namespace TomAndJerryCardImage.scripts;

// 注册入口，字符串与初始化方法同名
[ModInitializer(nameof(Init))]
public static class Entry
{
    public const string ModId = "TomAndJerryCardImage";

    public static Logger Logger { get; private set; } = null!;

    public static void Init()
    {
        var assembly = Assembly.GetExecutingAssembly();
        Logger = RitsuLibFramework.CreateLogger(ModId);
        ModTypeDiscoveryHub.RegisterModAssembly(ModId, assembly);

        CardImageConfig.Register();

        new Harmony("sts2.tomandjerry.cardimage").PatchAll();
        Logger.Info("initialized");
    }
}
