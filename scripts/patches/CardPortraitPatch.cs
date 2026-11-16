using System.Collections.Generic;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Models;

namespace TomAndJerryCardImage.scripts.patches;

// 直接接管最终纹理而不是 *Path：原版 DingyDagger / MadScience / Wither 只 override 了路径，
// 拦纹理就一并吃掉，不用按类型特判。
// TryGet 命中时要返回 false 才跳过原 getter；未命中返回 true 走原链，
// 其它 mod 对 PortraitPath / Portrait 的覆写照常生效。
[HarmonyPatch(typeof(CardModel), nameof(CardModel.Portrait), MethodType.Getter)]
internal static class PortraitTexturePatch
{
    private static bool Prefix(CardModel __instance, ref Texture2D __result)
        => !CardImageTextures.TryGet(__instance, out __result);
}

// 查看卡牌大图走 BigPortrait
[HarmonyPatch(typeof(CardModel), nameof(CardModel.BigPortrait), MethodType.Getter)]
internal static class BigPortraitTexturePatch
{
    private static bool Prefix(CardModel __instance, ref Texture2D __result)
        => !CardImageTextures.TryGet(__instance, out __result);
}

internal static class CardImageTextures
{
    // 缓存「该卡有没有替图」，开关状态每次现查
    private static readonly Dictionary<string, string?> PathCache = new();
    private static readonly Dictionary<string, Texture2D> TextureCache = new();

    public static bool TryGet(CardModel card, out Texture2D texture)
    {
        texture = null!;

        string pool;
        string entry;
        try
        {
            pool = card.Pool.Title.ToLowerInvariant();
            entry = card.Id.Entry.ToLowerInvariant();
        }
        catch
        {
            // Pool 在卡不属于任何卡池时会抛
            return false;
        }

        if (!CardImageConfig.IsEnabled(pool)) return false;

        var key = pool + "/" + entry;
        if (!PathCache.TryGetValue(key, out var path))
        {
            path = ResolveExistingPath(pool, entry);
            PathCache[key] = path;
        }

        if (path == null) return false;

        if (!TextureCache.TryGetValue(path, out var cached) || !GodotObject.IsInstanceValid(cached))
        {
            cached = ResourceLoader.Load<Texture2D>(path, null, ResourceLoader.CacheMode.Reuse);
            if (cached == null) return false;
            TextureCache[path] = cached;
        }

        texture = cached;
        return true;
    }

    // 历史素材有无下划线两种命名（ashen_strike / ashenstrike），两种都认
    private static string? ResolveExistingPath(string pool, string entry)
    {
        var withUnderscore = $"res://images/{pool}/{entry}.png";
        if (ResourceLoader.Exists(withUnderscore)) return withUnderscore;

        var compact = $"res://images/{pool}/{entry.Replace("_", "")}.png";
        if (ResourceLoader.Exists(compact)) return compact;

        return null;
    }
}
