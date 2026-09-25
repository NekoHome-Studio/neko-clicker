using NekoClicker.Core;

namespace NekoClicker.Content.NineLives;

/// <summary>
/// 「信仰」：神明纪元引入的第二资源。<para>
/// 与咖啡馆的"幸福感"同构——用 <see cref="IGameModule"/> 实现，核心零改动：
/// 状态放 <c>GameState.Counters</c>（跨层保留，"神记得你"），
/// 供 <c>Scaling(ScalingSource.CustomCounter)</c> 驱动修饰符，
/// 供 <c>UnlockCondition.Counter</c> 作为解锁条件（自带进度条）。
/// </para>
/// <para>
/// 离线期间 <see cref="OnTick"/> 不会运行，所以必须实现 <see cref="OnOffline"/> 补算。
/// </para>
/// </summary>
internal sealed class FaithModule : IGameModule
{
    /// <summary>信仰的计数器键。</summary>
    public const string CounterKey = "faith";

    /// <summary>每多少座建筑每秒产出 1 点信仰。比幸福感更慢，神明纪元要熬。</summary>
    public const double BuildingsPerPointPerSecond = 120.0;

    /// <summary>模块名。</summary>
    public string Name => "faith";

    /// <inheritdoc />
    public void OnTick(GameEngine engine, double deltaSeconds) => Accrue(engine, deltaSeconds);

    /// <inheritdoc />
    public void OnOffline(GameEngine engine, OfflineProgress progress)
        => Accrue(engine, progress.CreditedSeconds);

    private static void Accrue(GameEngine engine, double seconds)
    {
        if (seconds <= 0) return;
        double rate = engine.State.TotalBuildings() / BuildingsPerPointPerSecond;
        engine.State.AddCounter(CounterKey, rate * seconds);
    }
}
