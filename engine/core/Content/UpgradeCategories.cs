namespace NekoClicker.Core.Content;

/// <summary>
/// <see cref="UpgradeDefinition.Category"/> 里那条「这座升级属于哪座建筑」的约定。<para>
/// 约定本身从 1.4.0 起就散在内容包里（<c>$"building:{building.Id}"</c>），文档写在
/// <see cref="UpgradeDefinition.Category"/> 上；这个类型把它收成<b>一个出处</b>，
/// 于是构建期能校验它、引擎能按它建索引、界面能按索引取到"这座建筑自己的升级"。
/// </para>
/// <para>
/// <b>为什么是一条字符串而不是一个强类型字段</b>：<see cref="UpgradeDefinition.Category"/>
/// 一直是内容作者用的自由分组（<c>"click"</c> / <c>"memory"</c> / <c>"era"</c>…），
/// 十一个包都在用同一个字段表达"分组"。把建筑这条单独提成字段会让"分组"有两个出处，
/// 而世界上的升级只有一部分属于建筑。所以这里只把<b>既有的那条约定</b>变成可校验的。
/// </para>
/// </summary>
public static class UpgradeCategories
{
    /// <summary>建筑专属升级的分类前缀（值是 <c>"building:"</c>）。</summary>
    public const string BuildingPrefix = "building:";

    /// <summary>构造"属于这座建筑"的分类值。</summary>
    /// <param name="buildingId">建筑 id。</param>
    public static string ForBuilding(string buildingId)
    {
        ArgumentException.ThrowIfNullOrEmpty(buildingId);
        return BuildingPrefix + buildingId;
    }

    /// <summary>
    /// 试着从分类里取出建筑 id。<para>
    /// <c>"building:catnap"</c> → <c>true</c> + <c>"catnap"</c>；
    /// 前缀不对、或者是 <c>"building:"</c>（空 id）→ <c>false</c>。<b>不抛异常</b>：
    /// 调用方要能对"这压根不是建筑分类"与"是建筑分类但 id 是坏的"给出不同的处置，
    /// 而后者在构建期是一条必须点名的错误（见 <c>GameContentBuilder</c>）。
    /// </para>
    /// </summary>
    /// <param name="category">分类值；可为 <c>null</c>。</param>
    /// <param name="buildingId">取出的建筑 id；失败时为空串。</param>
    public static bool TryGetBuildingId(string? category, out string buildingId)
    {
        buildingId = string.Empty;
        if (category is null || !category.StartsWith(BuildingPrefix, StringComparison.Ordinal)) return false;

        string rest = category[BuildingPrefix.Length..];
        if (rest.Length == 0) return false;

        buildingId = rest;
        return true;
    }
}
