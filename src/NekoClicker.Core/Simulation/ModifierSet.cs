using NekoClicker.Core.Content;
using NekoClicker.Core.Numbers;

namespace NekoClicker.Core;

/// <summary>
/// 修饰符累加器：把同目标的修饰符按语义折叠成一个四元组。<para>
/// 语义（与 Cookie Clicker 的乘数链一致）：
/// <code>v = ((base + Flat) × (1 + Σ AdditivePercent) × Π Multiplicative) ^ Π Power</code>
/// 加法百分比与乘法分开累计，是为了让"两个 +50%"得到 +100%（而不是 ×2.25），
/// 而"两个翻倍"得到 ×4——这是增量游戏里玩家能直觉预期的行为。
/// </para>
/// </summary>
public struct ModifierAccumulator
{
    /// <summary>加法项累计。</summary>
    public double Flat;

    /// <summary>加法百分比累计（0.5 表示 +50%）。</summary>
    public double AdditivePercent;

    /// <summary>乘法项累计（初值 1）。</summary>
    public double Multiplicative;

    /// <summary>乘方指数累计（初值 1）。</summary>
    public double Power;

    /// <summary>是否存在任何修饰符。</summary>
    public bool Any;

    /// <summary>未受任何修饰的中性值。</summary>
    public static ModifierAccumulator Identity => new() { Multiplicative = 1.0, Power = 1.0 };

    /// <summary>把该累加器作用于基础值。</summary>
    public readonly double Apply(double baseValue)
    {
        if (!Any) return baseValue;
        double v = (baseValue + Flat) * (1.0 + AdditivePercent) * Multiplicative;
        if (Power != 1.0) v = Num.SafePow(v, Power);
        return v;
    }
}

/// <summary>
/// 一次求解得到的修饰符集合。<para>
/// 生命周期与"状态变更"绑定：购买、成就解锁、增益增减、读档都会让引擎丢弃旧集合重建。
/// 每个固定步长的生产结算只读它，因此 30fps 下没有重复遍历全部升级定义的代价。
/// </para>
/// </summary>
public sealed class ModifierSet
{
    private readonly Dictionary<ModifierTarget, ModifierAccumulator> _targets = [];

    /// <summary>集合是否为空。</summary>
    public bool IsEmpty => _targets.Count == 0;

    /// <summary>已登记的目标数量（诊断用）。</summary>
    public int TargetCount => _targets.Count;

    /// <summary>登记一条修饰符（数值会先按成长曲线解析）。</summary>
    public void Add(Modifier modifier, IGameMetrics metrics)
    {
        Add(modifier.Target, modifier.Operation, modifier.Resolve(metrics));
    }

    /// <summary>按目标 + 运算方式登记一个数值。</summary>
    public void Add(ModifierTarget target, ModifierOperation operation, double value)
    {
        if (!_targets.TryGetValue(target, out ModifierAccumulator acc))
            acc = ModifierAccumulator.Identity;

        switch (operation)
        {
            case ModifierOperation.Flat:
                acc.Flat += value;
                break;
            case ModifierOperation.AdditivePercent:
                acc.AdditivePercent += value;
                break;
            case ModifierOperation.Multiplicative:
                acc.Multiplicative *= value;
                break;
            case ModifierOperation.Power:
                acc.Power *= value;
                break;
        }
        acc.Any = true;
        _targets[target] = acc;
    }

    /// <summary>读取某目标的累加器。</summary>
    public ModifierAccumulator Get(ModifierTarget target)
        => _targets.TryGetValue(target, out ModifierAccumulator acc) ? acc : ModifierAccumulator.Identity;

    /// <summary>把全部修饰符作用于基础值。</summary>
    public double Apply(double baseValue, ModifierTarget target) => Get(target).Apply(baseValue);

    /// <summary>乘法部分：<c>(1 + Σ 百分比) × Π 乘法</c>（不含加法项与乘方）。</summary>
    public double Multiplier(ModifierTarget target)
    {
        if (!_targets.TryGetValue(target, out ModifierAccumulator acc)) return 1.0;
        return (1.0 + acc.AdditivePercent) * acc.Multiplicative;
    }

    /// <summary>加法项。</summary>
    public double FlatOf(ModifierTarget target) => Get(target).Flat;

    /// <summary>乘方指数。</summary>
    public double PowerOf(ModifierTarget target) => Get(target).Power;

    /// <summary>多个目标的乘法部分连乘（例如全局价格 × 单建筑价格）。</summary>
    public double CombinedMultiplier(params ModifierTarget[] targets)
    {
        double result = 1.0;
        foreach (ModifierTarget t in targets) result *= Multiplier(t);
        return result;
    }

    /// <summary>枚举所有已受影响的（目标，累加器）对。</summary>
    public IEnumerable<KeyValuePair<ModifierTarget, ModifierAccumulator>> Entries => _targets;
}

/// <summary>
/// 修饰符求解器：决定"哪些修饰符当前生效"。<para>
/// 生效来源共六类——已购升级（按购买次数重复计入）、已解锁成就、生效中的增益（按层数重复计入）、
/// 当前纪元、已作答选择的选项修饰符、以及当前主导立场的修饰符。
/// （永久升级本身也在升级表里，因此不会重复计算。）新增来源时只改这里。
/// </para>
/// </summary>
public static class ModifierResolver
{
    /// <summary>求解当前生效的全部修饰符。</summary>
    /// <param name="content">内容定义。</param>
    /// <param name="state">游戏状态。</param>
    /// <param name="metrics">只读指标。</param>
    /// <param name="includeBuffs">是否计入生效中的增益（离线收益结算时会传 <c>false</c>）。</param>
    public static ModifierSet Build(GameContent content, GameState state, IGameMetrics metrics, bool includeBuffs = true)
    {
        ModifierSet set = new();

        // 1) 升级：按已购次数重复计入（可重复购买的升级会线性/指数叠加）。
        foreach ((string id, int count) in state.UpgradeCounts)
        {
            if (count <= 0) continue;
            if (!content.UpgradeById.TryGetValue(id, out UpgradeDefinition? def)) continue;
            if (def.Modifiers.Count == 0) continue;
            for (int i = 0; i < count; i++)
                foreach (Modifier m in def.Modifiers)
                    set.Add(m, metrics);
        }

        // 2) 成就：解锁即永久生效。
        foreach (string id in state.Achievements)
        {
            if (!content.AchievementById.TryGetValue(id, out AchievementDefinition? def)) continue;
            foreach (Modifier m in def.Modifiers) set.Add(m, metrics);
        }

        // 3) 增益：按层数重复计入。
        if (includeBuffs)
        {
            foreach (ActiveBuff buff in state.Buffs)
            {
                if (!content.BuffById.TryGetValue(buff.Id, out BuffDefinition? def)) continue;
                int stacks = Math.Max(1, buff.Stacks);
                for (int i = 0; i < stacks; i++)
                    foreach (Modifier m in def.Modifiers)
                        set.Add(m, metrics);
            }
        }

        // 4) 当前纪元：本层常驻的规则倍率（"这一层世界是怎么运转的"）。
        if (content.EraByIndex.TryGetValue(state.Era, out EraDefinition? era))
        {
            foreach (Modifier m in era.Modifiers) set.Add(m, metrics);
        }

        // 5) 已作答的选择：选项自带的修饰符，永久生效。
        //    按"选了哪个选项"取值，而不是"答没答过"——所以状态里存的是选项 id。
        foreach ((string choiceId, string optionId) in state.ChoiceAnswers)
        {
            if (!content.ChoiceById.TryGetValue(choiceId, out ChoiceDefinition? choice)) continue;
            foreach (ChoiceOption option in choice.Options)
            {
                if (!string.Equals(option.Id, optionId, StringComparison.Ordinal)) continue;
                foreach (Modifier m in option.Modifiers) set.Add(m, metrics);
                break;
            }
        }

        // 6) 当前主导立场：立场轴漂移会直接改产量，这是"选择有数值代价"的落点。
        if (content.HasStances
            && ChoiceSystem.DominantStance(content, state) is { } dominant
            && content.StanceById.TryGetValue(dominant, out StanceDefinition? stance))
        {
            foreach (Modifier m in stance.Modifiers) set.Add(m, metrics);
        }

        return set;
    }
}
