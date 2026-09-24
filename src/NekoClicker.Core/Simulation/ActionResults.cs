namespace NekoClicker.Core;

/// <summary>购买/出售的批量模式，与 UI 上的切换按钮一一对应。</summary>
public enum PurchaseMode
{
    /// <summary>买 1 个。</summary>
    Buy1,

    /// <summary>买 10 个。</summary>
    Buy10,

    /// <summary>买 100 个。</summary>
    Buy100,

    /// <summary>买到买不起为止。</summary>
    BuyMax,

    /// <summary>卖 1 个。</summary>
    Sell1,

    /// <summary>卖 10 个。</summary>
    Sell10,

    /// <summary>全部卖出。</summary>
    SellMax,
}

/// <summary><see cref="PurchaseMode"/> 的辅助方法。</summary>
public static class PurchaseModes
{
    private static readonly PurchaseMode[] Cycle =
    [
        PurchaseMode.Buy1, PurchaseMode.Buy10, PurchaseMode.Buy100, PurchaseMode.BuyMax,
    ];

    /// <summary>是否为出售模式。</summary>
    public static bool IsSell(this PurchaseMode mode)
        => mode is PurchaseMode.Sell1 or PurchaseMode.Sell10 or PurchaseMode.SellMax;

    /// <summary>请求的数量；<c>0</c> 表示"尽可能多"。</summary>
    public static int RequestedAmount(this PurchaseMode mode) => mode switch
    {
        PurchaseMode.Buy1 or PurchaseMode.Sell1 => 1,
        PurchaseMode.Buy10 or PurchaseMode.Sell10 => 10,
        PurchaseMode.Buy100 => 100,
        _ => 0,
    };

    /// <summary>按钮文案。</summary>
    public static string Label(this PurchaseMode mode) => mode switch
    {
        PurchaseMode.Buy1 => "买 1",
        PurchaseMode.Buy10 => "买 10",
        PurchaseMode.Buy100 => "买 100",
        PurchaseMode.BuyMax => "买满",
        PurchaseMode.Sell1 => "卖 1",
        PurchaseMode.Sell10 => "卖 10",
        PurchaseMode.SellMax => "全卖",
        _ => mode.ToString(),
    };

    /// <summary>切换到下一个购买档位（不改变买卖方向）。</summary>
    public static PurchaseMode NextBuy(this PurchaseMode mode)
    {
        int index = Array.IndexOf(Cycle, mode);
        if (index < 0) return PurchaseMode.Buy1;
        return Cycle[(index + 1) % Cycle.Length];
    }

    /// <summary>在"买"与"卖"之间切换，保留档位。</summary>
    public static PurchaseMode ToggleSell(this PurchaseMode mode) => mode switch
    {
        PurchaseMode.Buy1 => PurchaseMode.Sell1,
        PurchaseMode.Buy10 => PurchaseMode.Sell10,
        PurchaseMode.Buy100 => PurchaseMode.Sell10,
        PurchaseMode.BuyMax => PurchaseMode.SellMax,
        PurchaseMode.Sell1 => PurchaseMode.Buy1,
        PurchaseMode.Sell10 => PurchaseMode.Buy10,
        PurchaseMode.SellMax => PurchaseMode.BuyMax,
        _ => mode,
    };
}

/// <summary>点击结果。</summary>
/// <param name="Gained">本次点击获得的货币。</param>
/// <param name="CookiesAfter">点击后的存量。</param>
/// <param name="ClickPower">本次生效的点击收益。</param>
public readonly record struct ClickResult(double Gained, double CookiesAfter, double ClickPower);

/// <summary>购买/出售结果。</summary>
public sealed record PurchaseResult
{
    /// <summary>是否成功。</summary>
    public bool Success { get; init; }

    /// <summary>给玩家看的消息。</summary>
    public string Message { get; init; } = string.Empty;

    /// <summary>涉及的内容 id。</summary>
    public string? Id { get; init; }

    /// <summary>实际数量。</summary>
    public int Amount { get; init; }

    /// <summary>实际花费（出售时为返还金额）。</summary>
    public double TotalPrice { get; init; }

    /// <summary>操作后的持有数量。</summary>
    public int OwnedAfter { get; init; }

    /// <summary>构造失败结果。</summary>
    public static PurchaseResult Fail(string message, string? id = null)
        => new() { Success = false, Message = message, Id = id };

    /// <summary>构造成功结果。</summary>
    public static PurchaseResult Ok(string message, string id, int amount, double totalPrice, int ownedAfter)
        => new() { Success = true, Message = message, Id = id, Amount = amount, TotalPrice = totalPrice, OwnedAfter = ownedAfter };
}

/// <summary>点击金猫的结果。</summary>
public sealed record GoldenCookieResult
{
    /// <summary>是否成功。</summary>
    public bool Success { get; init; }

    /// <summary>给玩家看的消息。</summary>
    public string Message { get; init; } = string.Empty;

    /// <summary>抽中的结果 id。</summary>
    public string? OutcomeId { get; init; }

    /// <summary>结果显示名。</summary>
    public string OutcomeName { get; init; } = string.Empty;

    /// <summary>结果图标。</summary>
    public string Icon { get; init; } = string.Empty;

    /// <summary>净获得的货币（可能为负）。</summary>
    public double CookiesGained { get; init; }

    /// <summary>附带的增益 id。</summary>
    public string? BuffId { get; init; }

    /// <summary>附带增益的时长。</summary>
    public double BuffSeconds { get; init; }

    /// <summary>构造失败结果。</summary>
    public static GoldenCookieResult Fail(string message) => new() { Success = false, Message = message };
}

/// <summary>转生结果。</summary>
public sealed record AscensionResult
{
    /// <summary>是否成功。</summary>
    public bool Success { get; init; }

    /// <summary>给玩家看的消息。</summary>
    public string Message { get; init; } = string.Empty;

    /// <summary>转生前的等级。</summary>
    public int PreviousLevel { get; init; }

    /// <summary>转生后的等级。</summary>
    public int NewLevel { get; init; }

    /// <summary>本次获得的转生货币。</summary>
    public double ChipsGained { get; init; }

    /// <summary>累计转生次数。</summary>
    public int Ascensions { get; init; }

    /// <summary>构造失败结果。</summary>
    public static AscensionResult Fail(string message) => new() { Success = false, Message = message };
}

/// <summary>离线收益结算结果。</summary>
/// <param name="ElapsedSeconds">实际离线秒数。</param>
/// <param name="CreditedSeconds">计入收益的秒数（受上限约束）。</param>
/// <param name="CookiesGained">补发的货币。</param>
/// <param name="WasCapped">是否因为触到上限而被截断。</param>
public readonly record struct OfflineProgress(
    double ElapsedSeconds,
    double CreditedSeconds,
    double CookiesGained,
    bool WasCapped);

/// <summary>转生预览（UI 在转生按钮旁展示）。</summary>
/// <param name="CurrentLevel">当前等级。</param>
/// <param name="NextLevel">若现在转生能达到的等级。</param>
/// <param name="ChipsOnAscend">若现在转生能获得的转生货币。</param>
/// <param name="CookiesForNextLevel">达到下一等级所需的历史累计赚取量。</param>
/// <param name="Progress">到下一等级的进度 [0,1]。</param>
/// <param name="CanAscend">是否满足转生条件。</param>
public readonly record struct PrestigePreview(
    int CurrentLevel,
    int NextLevel,
    double ChipsOnAscend,
    double CookiesForNextLevel,
    double Progress,
    bool CanAscend);
