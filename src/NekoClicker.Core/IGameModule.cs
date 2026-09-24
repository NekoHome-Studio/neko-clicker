using NekoClicker.Core.Content;

namespace NekoClicker.Core;

/// <summary>
/// 可插拔的游戏模块：把"引擎不认识但某个游戏需要"的系统（例如花园、股票、小游戏）
/// 从核心中分离出去。<para>
/// 模块与内容包的区别：内容包只提供数据；模块可以挂载事件、在每个 tick 里做事、
/// 并向内容构建器补充定义。核心引擎永远不会依赖任何具体模块。
/// </para>
/// </summary>
public interface IGameModule
{
    /// <summary>模块名（用于诊断）。</summary>
    string Name { get; }

    /// <summary>向内容构建器补充本模块的定义。构建期调用一次。</summary>
    /// <param name="builder">内容构建器。</param>
    void Configure(GameContentBuilder builder)
    {
    }

    /// <summary>引擎创建完成后调用，用于订阅事件。</summary>
    /// <param name="engine">宿主引擎。</param>
    void OnAttach(GameEngine engine)
    {
    }

    /// <summary>每个固定步长调用一次。</summary>
    /// <param name="engine">宿主引擎。</param>
    /// <param name="deltaSeconds">固定步长。</param>
    void OnTick(GameEngine engine, double deltaSeconds)
    {
    }

    /// <summary>
    /// 离线收益结算完成后调用。<para>
    /// 用途：模块自己维护的派生状态（例如计数器类的"幸福感""士气""被阅读度"）在离线期间
    /// 不会经过 <see cref="OnTick"/>，不处理就会凭空落后一大截。实现应当按
    /// <paramref name="progress"/> 的时长补算。<b>只有引擎实际发放了离线收益时才会调用</b>
    /// （即 <see cref="GameEngine.ApplyOfflineProgress"/> 返回非 null）。
    /// </para>
    /// </summary>
    /// <param name="engine">宿主引擎。</param>
    /// <param name="progress">离线结算明细。</param>
    void OnOffline(GameEngine engine, OfflineProgress progress)
    {
    }

    /// <summary>转生时调用，用于重置模块自身的运行时状态。</summary>
    /// <param name="engine">宿主引擎。</param>
    void OnAscend(GameEngine engine)
    {
    }
}
