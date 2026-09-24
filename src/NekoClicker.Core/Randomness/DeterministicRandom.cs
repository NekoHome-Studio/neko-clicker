namespace NekoClicker.Core.Randomness;

/// <summary>
/// 确定性伪随机数发生器（xorshift128+）。<para>
/// 增量游戏里随机只出现在少数地方（金猫出现时机、掉落结果、卡牌池），但它必须
/// <b>可存档、可复现</b>：同一个种子在测试与玩家之间必须得到同一串序列，否则
/// 平衡性验证就没法做。因此这里不用 <see cref="System.Random"/>（其内部状态不可
/// 存取，且不同 .NET 版本的算法可能变化），而是自带一个状态可序列化的小型 PRNG。
/// </para>
/// </summary>
public sealed class DeterministicRandom
{
    private ulong _s0;
    private ulong _s1;

    /// <summary>用单个种子初始化。</summary>
    public DeterministicRandom(ulong seed)
    {
        // 先用 splitmix64 把种子展开成两个状态字：相邻种子（0,1,2...）也能产生
        // 互不相关的序列，直接用种子当状态会得到高度相关的开头。
        ulong z = seed;
        _s0 = SplitMix64(ref z);
        _s1 = SplitMix64(ref z);
        if (_s0 == 0 && _s1 == 0) _s1 = 0x9E3779B97F4A7C15UL;
    }

    /// <summary>从存档恢复状态。</summary>
    public DeterministicRandom(ulong state0, ulong state1)
    {
        _s0 = state0;
        _s1 = state1;
        if (_s0 == 0 && _s1 == 0) _s1 = 0x9E3779B97F4A7C15UL;
    }

    /// <summary>当前状态字 0，用于存档。</summary>
    public ulong State0 => _s0;

    /// <summary>当前状态字 1，用于存档。</summary>
    public ulong State1 => _s1;

    /// <summary>生成下一个 64 位无符号整数。</summary>
    public ulong NextUInt64()
    {
        ulong x = _s0;
        ulong y = _s1;
        _s0 = y;
        x ^= x << 23;
        _s1 = x ^ y ^ (x >> 17) ^ (y >> 26);
        return _s1 + y;
    }

    /// <summary>[0, 1) 区间的双精度浮点数。</summary>
    public double NextDouble() => (NextUInt64() >> 11) * (1.0 / 9007199254740992.0);

    /// <summary>[min, max) 区间的双精度浮点数。</summary>
    public double NextDouble(double min, double max) => min + (NextDouble() * (max - min));

    /// <summary>[minInclusive, maxExclusive) 区间的整数。范围非法时返回 minInclusive。</summary>
    public int NextInt(int minInclusive, int maxExclusive)
    {
        if (maxExclusive <= minInclusive) return minInclusive;
        ulong range = (ulong)(maxExclusive - (long)minInclusive);
        return (int)(minInclusive + (long)(NextUInt64() % range));
    }

    /// <summary>按权重挑选一项。权重非正的项会被跳过；全部非正时返回第一项。</summary>
    public T PickWeighted<T>(IReadOnlyList<T> items, Func<T, double> weightSelector)
    {
        if (items.Count == 0) throw new ArgumentException("items 不能为空。", nameof(items));

        double total = 0;
        for (int i = 0; i < items.Count; i++)
        {
            double w = weightSelector(items[i]);
            if (w > 0) total += w;
        }
        if (total <= 0) return items[0];

        double roll = NextDouble() * total;
        for (int i = 0; i < items.Count; i++)
        {
            double w = weightSelector(items[i]);
            if (w <= 0) continue;
            roll -= w;
            if (roll < 0) return items[i];
        }
        return items[^1];
    }

    /// <summary>原地洗牌（Fisher-Yates）。</summary>
    public void Shuffle<T>(IList<T> items)
    {
        for (int i = items.Count - 1; i > 0; i--)
        {
            int j = NextInt(0, i + 1);
            (items[i], items[j]) = (items[j], items[i]);
        }
    }

    private static ulong SplitMix64(ref ulong x)
    {
        x += 0x9E3779B97F4A7C15UL;
        ulong z = x;
        z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
        z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
        return z ^ (z >> 31);
    }
}
