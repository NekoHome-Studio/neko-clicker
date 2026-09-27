namespace NekoClicker.Core.Numbers;

/// <summary>
/// 数值运算辅助。<para>
/// 本框架统一使用 <see cref="double"/> 作为货币/产量类型（与 Cookie Clicker 的
/// <c>Game.cookies</c> 一致）：它天然支持 1e308 级别的数值、无需引入大数库，
/// 且浮点误差在增量游戏的量级下不可见。<c>*</c>/<c>+</c> 会静默溢出成
/// <see cref="double.PositiveInfinity"/>，本类提供饱和运算把结果钉在
/// <see cref="double.MaxValue"/>，避免"价格变成 Infinity 后永远买不起"的经典 bug。
/// </para>
/// </summary>
public static class Num
{
    /// <summary>饱和上界。所有饱和运算都以此为界。</summary>
    public const double Max = double.MaxValue;

    /// <summary>判断是否是可用于显示的有限数值。</summary>
    public static bool IsFinite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);

    /// <summary>把负数值压到 0。</summary>
    public static double NonNegative(double value) => value > 0 ? value : 0;

    /// <summary>把 value 限制在 [min, max] 内。</summary>
    public static double Clamp(double value, double min, double max)
    {
        if (double.IsNaN(value)) return min;
        if (value < min) return min;
        if (value > max) return max;
        return value;
    }

    /// <summary>饱和加法：溢出时返回 ±<see cref="Max"/>，而不是 Infinity。</summary>
    public static double SafeAdd(double a, double b)
    {
        double r = a + b;
        if (double.IsInfinity(r)) return r > 0 ? Max : -Max;
        if (double.IsNaN(r)) return 0;
        return r;
    }

    /// <summary>饱和乘法：溢出时返回 ±<see cref="Max"/>。</summary>
    public static double SafeMul(double a, double b)
    {
        double r = a * b;
        if (double.IsInfinity(r)) return r > 0 ? Max : -Max;
        if (double.IsNaN(r)) return 0;
        return r;
    }

    /// <summary>饱和乘方。</summary>
    public static double SafePow(double a, double b)
    {
        if (a <= 0) return a == 0 ? 0 : double.NaN;
        double r = Math.Pow(a, b);
        if (double.IsInfinity(r)) return Max;
        if (double.IsNaN(r)) return 0;
        return r;
    }

    /// <summary>饱和求和。</summary>
    public static double SafeSum(IEnumerable<double> values)
    {
        double acc = 0;
        foreach (double v in values) acc = SafeAdd(acc, v);
        return acc;
    }

    /// <summary>浮点近似比较（用于测试与 UI 抖动抑制）。</summary>
    public static bool Approximately(double a, double b, double epsilon = 1e-9)
    {
        if (a == b) return true;
        double diff = Math.Abs(a - b);
        double scale = Math.Max(Math.Abs(a), Math.Abs(b));
        return diff <= epsilon * Math.Max(1.0, scale);
    }

    /// <summary>相对误差比较，适合比较量级很大的产量值。</summary>
    public static bool RelativeEquals(double a, double b, double tolerance = 1e-6)
    {
        if (a == b) return true;
        if (!IsFinite(a) || !IsFinite(b)) return false;
        double scale = Math.Max(Math.Abs(a), Math.Abs(b));
        if (scale == 0) return true;
        return Math.Abs(a - b) / scale <= tolerance;
    }
}
