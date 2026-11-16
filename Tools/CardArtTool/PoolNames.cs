using System;
using System.Collections.Generic;

namespace CardArtTool;

// 卡池显示名（写死，工具独立运行不读游戏本地化）
public static class PoolNames
{
    private static readonly Dictionary<string, string> Names = new(StringComparer.OrdinalIgnoreCase)
    {
        ["ironclad"] = "铁甲战士",
        ["silent"] = "静默猎手",
        ["defect"] = "故障机器人",
        ["necrobinder"] = "亡灵契约师",
        ["regent"] = "储君",
        ["fool"] = "愚者",
        ["colorless"] = "无色",
        ["curse"] = "诅咒",
        ["status"] = "状态",
        ["token"] = "衍生",
        ["event"] = "事件",
        ["quest"] = "任务",
    };

    public static string Get(string pool) => Names.TryGetValue(pool, out var name) ? name : pool;
}

/// <summary>卡池下拉项：Value 为原始卡池名，空字符串表示「全部」。</summary>
public sealed class PoolOption
{
    public string Value { get; init; } = "";
    public string Display { get; init; } = "";

    public override string ToString() => Display;
}
