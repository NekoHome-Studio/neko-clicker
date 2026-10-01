using System.Diagnostics;

namespace NekoClicker.Core;

/// <summary>
/// 时间源抽象。<para>
/// 固定步长循环、离线收益、自动存档全都依赖"现在几点/过了多久"。把它们抽到接口后面，
/// 测试里就能用 <see cref="ManualClock"/> 把 8 小时离线压缩成一次方法调用，
/// 而不需要真的 <c>Thread.Sleep</c>。
/// </para>
/// </summary>
public interface IClock
{
    /// <summary>UTC 墙上时间。用于离线收益与存档时间戳。</summary>
    DateTimeOffset UtcNow { get; }

    /// <summary>单调递增的秒数。用于推进模拟，不受系统时间调整影响。</summary>
    double MonotonicSeconds { get; }
}

/// <summary>生产环境时间源。</summary>
public sealed class SystemClock : IClock
{
    private static readonly Stopwatch Watch = Stopwatch.StartNew();

    /// <summary>共享实例。</summary>
    public static readonly SystemClock Instance = new();

    /// <inheritdoc />
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;

    /// <inheritdoc />
    public double MonotonicSeconds => Watch.Elapsed.TotalSeconds;
}

/// <summary>测试用手动时间源：时间只在显式调用时前进。</summary>
public sealed class ManualClock : IClock
{
    private double _monotonic;
    private DateTimeOffset _utcNow;

    /// <summary>创建一个从指定时刻开始的手动时间源。</summary>
    public ManualClock(DateTimeOffset? start = null)
    {
        _utcNow = start ?? new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero);
        _monotonic = 0;
    }

    /// <inheritdoc />
    public DateTimeOffset UtcNow => _utcNow;

    /// <inheritdoc />
    public double MonotonicSeconds => _monotonic;

    /// <summary>让时间前进指定秒数（墙上时间与单调时钟一起前进）。</summary>
    public void Advance(double seconds)
    {
        if (seconds < 0) throw new ArgumentOutOfRangeException(nameof(seconds));
        _monotonic += seconds;
        _utcNow = _utcNow.AddSeconds(seconds);
    }

    /// <summary>只跳跃墙上时间（模拟"关掉游戏一晚上"），单调时钟不动。</summary>
    public void JumpUtc(TimeSpan delta) => _utcNow = _utcNow.Add(delta);

    /// <summary>直接设定墙上时间。</summary>
    public void SetUtcNow(DateTimeOffset value) => _utcNow = value;
}
