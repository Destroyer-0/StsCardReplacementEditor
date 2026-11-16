using System;
using STS2RitsuLib;
using STS2RitsuLib.Data;
using STS2RitsuLib.Settings;
using STS2RitsuLib.Utils.Persistence;

namespace TomAndJerryCardImage.scripts;

/// <summary>卡面替换开关：每个卡池一个 bool。</summary>
public sealed class CardImageSettings
{
    public bool Ironclad { get; set; } = true;
    public bool Silent { get; set; } = true;
    public bool Defect { get; set; } = true;
    public bool Necrobinder { get; set; } = true;
    public bool Regent { get; set; } = true;
    public bool Fool { get; set; } = true;
    public bool Colorless { get; set; } = true;
    public bool Curse { get; set; } = true;
    public bool Status { get; set; } = true;
    public bool Token { get; set; } = true;
    public bool Event { get; set; } = true;
    public bool Quest { get; set; } = true;
}

/// <summary>设置页注册 + 运行时开关查询。</summary>
public static class CardImageConfig
{
    private const string DataKey = "settings";

    // 卡池名 + 显示名用的原版本地化键（原版无键的自己出）
    private const string SettingsTable = "settings_ui";

    public static CardImageSettings Settings =>
        RitsuLibFramework.GetDataStore(Entry.ModId).Get<CardImageSettings>(DataKey);

    public static void Register()
    {
        using (RitsuLibFramework.BeginModDataRegistration(Entry.ModId))
        {
            RitsuLibFramework.GetDataStore(Entry.ModId).Register(
                key: DataKey,
                fileName: "card_image_settings.json",
                scope: SaveScope.Global,
                defaultFactory: () => new CardImageSettings(),
                autoCreateIfMissing: true);
        }

        RitsuLibFramework.RegisterModSettings(Entry.ModId, page => page
            // 关掉 mod json 的 manifest 回退，标题/描述/显示名全部走本地化键
            .WithoutSourceAssemblyManifestLookup()
            .WithTitle(Text("TOMANDJERRY-PAGE.title", "卡面替换"))
            .WithDescription(Text("TOMANDJERRY-PAGE.description",
                "按角色或卡池单独开关卡面替换。关闭某个卡池后，该卡池的卡牌会恢复原版卡面。"))
            .WithModDisplayName(Text("TOMANDJERRY-MOD_NAME.title", "猫鼠尖塔"))
            .AddSection("pools", section =>
            {
                var body = section.WithTitle(Text("TOMANDJERRY-SECTION_POOLS.title", "按角色 / 卡池开关"));

                Add(body, "ironclad", "characters", "IRONCLAD.title", "铁甲战士",
                    m => m.Ironclad, (m, v) => m.Ironclad = v);
                Add(body, "silent", "characters", "SILENT.title", "静默猎手",
                    m => m.Silent, (m, v) => m.Silent = v);
                Add(body, "defect", "characters", "DEFECT.title", "故障机器人",
                    m => m.Defect, (m, v) => m.Defect = v);
                Add(body, "necrobinder", "characters", "NECROBINDER.title", "亡灵契约师",
                    m => m.Necrobinder, (m, v) => m.Necrobinder = v);
                Add(body, "regent", "characters", "REGENT.title", "储君",
                    m => m.Regent, (m, v) => m.Regent = v);
                Add(body, "fool", "characters", "FOOL.title", "愚者",
                    m => m.Fool, (m, v) => m.Fool = v);
                Add(body, "colorless", SettingsTable, "TOMANDJERRY-POOL_COLORLESS.title", "无色",
                    m => m.Colorless, (m, v) => m.Colorless = v);
                Add(body, "curse", "gameplay_ui", "CARD_RARITY.CURSE", "诅咒",
                    m => m.Curse, (m, v) => m.Curse = v);
                Add(body, "status", "gameplay_ui", "CARD_RARITY.STATUS", "状态",
                    m => m.Status, (m, v) => m.Status = v);
                Add(body, "token", "gameplay_ui", "CARD_RARITY.TOKEN", "衍生",
                    m => m.Token, (m, v) => m.Token = v);
                Add(body, "event", "gameplay_ui", "CARD_RARITY.EVENT", "事件",
                    m => m.Event, (m, v) => m.Event = v);
                Add(body, "quest", "gameplay_ui", "CARD_RARITY.QUEST", "任务",
                    m => m.Quest, (m, v) => m.Quest = v);
            }));
    }

    /// <summary>未登记的卡池默认开启，避免别的 mod 的卡被误关。</summary>
    public static bool IsEnabled(string pool) => pool switch
    {
        "ironclad" => Settings.Ironclad,
        "silent" => Settings.Silent,
        "defect" => Settings.Defect,
        "necrobinder" => Settings.Necrobinder,
        "regent" => Settings.Regent,
        "fool" => Settings.Fool,
        "colorless" => Settings.Colorless,
        "curse" => Settings.Curse,
        "status" => Settings.Status,
        "token" => Settings.Token,
        "event" => Settings.Event,
        "quest" => Settings.Quest,
        _ => true,
    };

    private static ModSettingsText Text(string key, string fallback) =>
        ModSettingsText.LocString(SettingsTable, key, fallback);

    private static void Add(
        ModSettingsSectionBuilder section,
        string pool,
        string locTable,
        string locKey,
        string fallback,
        Func<CardImageSettings, bool> get,
        Action<CardImageSettings, bool> set)
    {
        section.AddToggle(
            pool,
            ModSettingsText.LocString(locTable, locKey, fallback),
            new ModSettingsValueBinding<CardImageSettings, bool>(
                Entry.ModId, DataKey, SaveScope.Global, get, set));
    }
}
