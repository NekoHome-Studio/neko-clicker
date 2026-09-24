using NekoClicker.Core.Numbers;

namespace NekoClicker.Core.Tests;

/// <summary>数值格式化与饱和运算。</summary>
public static class NumbersTests
{
    [Test]
    public static void Format_BelowMillion_UsesSeparators()
    {
        Check.Equal("0", NumFormat.Format(0));
        Check.Equal("1", NumFormat.Format(1));
        Check.Equal("999", NumFormat.Format(999));
        Check.Equal("1,000", NumFormat.Format(1_000));
        Check.Equal("999,999", NumFormat.Format(999_999));
        Check.Equal("-1,234", NumFormat.Format(-1_234));
    }

    [Test]
    public static void Format_SmallFractions_KeepDecimals()
    {
        Check.Equal("0.5", NumFormat.Format(0.5));
        Check.Equal("1.25", NumFormat.Format(1.25));
        Check.Equal("0.0001", NumFormat.Format(0.0001));
    }

    [Test]
    public static void Format_LongScale_Names()
    {
        Check.Equal("1 million", NumFormat.Format(1e6));
        Check.Equal("1.5 million", NumFormat.Format(1.5e6));
        Check.Equal("999,999", NumFormat.Format(999_999));
        Check.Equal("1 billion", NumFormat.Format(1e9));
        Check.Equal("2.5 trillion", NumFormat.Format(2.5e12));
        Check.Equal("7 quadrillion", NumFormat.Format(7e15));
        Check.Equal("1 sextillion", NumFormat.Format(1e21));
    }

    [Test]
    public static void Format_PowerOfTenBoundaries_DoNotFallThrough()
    {
        // 这些值最容易踩到 Math.Log10 的舍入误差：档位必须正好落在 10 的整数次幂上。
        Check.Equal("1 million", NumFormat.Format(Math.Pow(10, 6)));
        Check.Equal("1 billion", NumFormat.Format(Math.Pow(10, 9)));
        Check.Equal("1 trillion", NumFormat.Format(Math.Pow(10, 12)));
        Check.Equal("1 quintillion", NumFormat.Format(Math.Pow(10, 18)));
        Check.Equal("1 vigintillion", NumFormat.Format(Math.Pow(10, 63)));
    }

    [Test]
    public static void Format_Scientific_Fallback_WhenNamesExhausted()
    {
        Check.Equal("1e66", NumFormat.Format(1e66));
        Check.Equal("1e-6", NumFormat.Format(1e-6));
    }

    [Test]
    public static void Format_ShortStyle()
    {
        Check.Equal("1M", NumFormat.Format(1e6, NumberStyle.Short));
        Check.Equal("1.5M", NumFormat.Format(1.5e6, NumberStyle.Short));
        Check.Equal("1B", NumFormat.Format(1e9, NumberStyle.Short));
        Check.Equal("3.25Qa", NumFormat.Format(3.25e15, NumberStyle.Short));
    }

    [Test]
    public static void Format_PlainStyle_UsesSeparators()
    {
        Check.Equal("1,234,567", NumFormat.Format(1_234_567, NumberStyle.Plain));
        Check.Equal("15,000", NumFormat.Format(15_000, NumberStyle.Plain));
        Check.Equal("100,000,000,000,000,000,000", NumFormat.Format(1e20, NumberStyle.Plain));
        // 超过 1e21 后完整数字无法阅读，退回科学计数法
        Check.Equal("1e21", NumFormat.Format(1e21, NumberStyle.Plain));
    }

    [Test]
    public static void Format_CustomScaleNames_AreHonoured()
    {
        // 本地化场景：用"万/亿"表替换英文刻度名（索引 0 对应 1e6）。
        string[] chinese = ["百万", "十亿", "万亿"];
        Check.Equal("2.5百万", NumFormat.Format(2.5e6, chinese, space: false));
        Check.Equal("3 十亿", NumFormat.Format(3e9, chinese));
    }

    [Test]
    public static void Format_SpecialValues()
    {
        Check.Equal("NaN", NumFormat.Format(double.NaN));
        Check.Equal("∞", NumFormat.Format(double.PositiveInfinity));
        Check.Equal("-∞", NumFormat.Format(double.NegativeInfinity));
    }

    [Test]
    public static void Percent_And_Multiplier()
    {
        Check.Equal("12.5%", NumFormat.Percent(0.125));
        Check.Equal("100%", NumFormat.Percent(1.0));
        Check.Equal("0%", NumFormat.Percent(0));
        Check.Equal("×7", NumFormat.Multiplier(7));
        Check.Equal("+5", NumFormat.Signed(5));
        Check.Equal("-3", NumFormat.Signed(-3));
    }

    [Test]
    public static void Duration_FormatsAcrossRanges()
    {
        Check.Equal("0.5s", NumFormat.Duration(0.5));
        Check.Equal("45s", NumFormat.Duration(45));
        Check.Equal("1m 30s", NumFormat.Duration(90));
        Check.Equal("1h 00m", NumFormat.Duration(3_600));
        Check.Equal("1d 1h", NumFormat.Duration(90_000));
    }

    [Test]
    public static void Duration_CarriesRoundedSeconds()
    {
        Check.Equal("1m 00s", NumFormat.Duration(60));
        Check.Equal("2m 30s", NumFormat.Duration(150));
        // 浮点累加常得到 1499.9999999999998 秒；四舍五入后必须进位，不能显示 "24m 60s"。
        Check.Equal("25m 00s", NumFormat.Duration(1499.9999999999998));
        Check.Equal("2h 00m", NumFormat.Duration(7199.999999999999));
    }

    [Test]
    public static void Num_SaturatesInsteadOfOverflowing()
    {
        Check.Equal(double.MaxValue, Num.SafeAdd(double.MaxValue, double.MaxValue));
        Check.Equal(double.MaxValue, Num.SafeMul(1e300, 1e300));
        Check.Equal(double.MaxValue, Num.SafePow(10, 400));
        Check.Equal(-double.MaxValue, Num.SafeAdd(-double.MaxValue, -double.MaxValue));
        Check.True(Num.IsFinite(Num.SafeMul(1e300, 1e300)));
    }

    [Test]
    public static void Num_ClampAndHelpers()
    {
        Check.Equal(3.0, Num.Clamp(5, 0, 3));
        Check.Equal(0.0, Num.NonNegative(-5));
        Check.True(Num.Approximately(0.1 + 0.2, 0.3));
        Check.True(Num.RelativeEquals(1e300, 1e300 * (1 + 1e-12)));
        Check.False(Num.RelativeEquals(1e300, 2e300));
    }
}

/// <summary>确定性随机数发生器。</summary>
public static class RandomTests
{
    [Test]
    public static void SameSeed_ProducesSameSequence()
    {
        var a = new Randomness.DeterministicRandom(42);
        var b = new Randomness.DeterministicRandom(42);
        for (int i = 0; i < 100; i++) Check.Equal(a.NextUInt64(), b.NextUInt64());
    }

    [Test]
    public static void DifferentSeeds_Diverge()
    {
        var a = new Randomness.DeterministicRandom(1);
        var b = new Randomness.DeterministicRandom(2);
        bool differs = false;
        for (int i = 0; i < 10 && !differs; i++) differs = a.NextUInt64() != b.NextUInt64();
        Check.True(differs, "不同种子不应产生相同序列。");
    }

    [Test]
    public static void StateRoundTrip_ContinuesSequence()
    {
        var a = new Randomness.DeterministicRandom(123);
        for (int i = 0; i < 5; i++) a.NextUInt64();

        var restored = new Randomness.DeterministicRandom(a.State0, a.State1);
        for (int i = 0; i < 20; i++) Check.Equal(a.NextUInt64(), restored.NextUInt64());
    }

    [Test]
    public static void NextDouble_StaysInRange()
    {
        var rng = new Randomness.DeterministicRandom(9);
        for (int i = 0; i < 10_000; i++)
        {
            double v = rng.NextDouble();
            Check.True(v is >= 0 and < 1, $"NextDouble 越界：{v}");
        }

        for (int i = 0; i < 1_000; i++)
        {
            int v = rng.NextInt(3, 7);
            Check.True(v is >= 3 and < 7, $"NextInt 越界：{v}");
        }
    }

    [Test]
    public static void PickWeighted_RespectsWeights()
    {
        var rng = new Randomness.DeterministicRandom(5);
        string[] items = ["a", "b"];
        double[] weights = [9, 1];

        int aCount = 0;
        for (int i = 0; i < 10_000; i++)
            if (rng.PickWeighted(items, x => weights[Array.IndexOf(items, x)]) == "a") aCount++;

        Check.True(aCount is > 8500 and < 9500, $"权重 9:1 的分布异常（a 出现 {aCount} 次）。");
    }
}
