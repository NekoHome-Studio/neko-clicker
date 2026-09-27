namespace NekoClicker.Core.Events;

/// <summary>所有游戏事件的标记接口。</summary>
public interface IGameEvent
{
}

/// <summary>
/// 类型安全的事件总线。<para>
/// 引擎不直接暴露 <c>event</c> 字段：UI 需要按需订阅十几种事件，模块也需要在
/// 运行时挂/卸监听，用回调字段会迅速退化成一张巨大的 <c>Action</c> 列表。
/// 按类型索引的字典让订阅/退订都是 O(1)，且派发时只触碰真正关心该事件的订阅者。
/// </para>
/// <para>约定：事件按<b>静态类型</b>精确派发（事件都是 sealed record），不做基类冒泡。</para>
/// </summary>
public sealed class GameEventBus
{
    private readonly Dictionary<Type, List<Delegate>> _handlers = [];
    private readonly object _gate = new();

    /// <summary>订阅某类事件；释放返回的令牌即可退订（推荐配合 <c>using</c>）。</summary>
    public IDisposable Subscribe<T>(Action<T> handler) where T : IGameEvent
    {
        ArgumentNullException.ThrowIfNull(handler);
        lock (_gate)
        {
            if (!_handlers.TryGetValue(typeof(T), out List<Delegate>? list))
            {
                list = [];
                _handlers[typeof(T)] = list;
            }
            list.Add(handler);
        }
        return new Subscription(this, typeof(T), handler);
    }

    /// <summary>退订某类事件。</summary>
    public void Unsubscribe<T>(Action<T> handler) where T : IGameEvent
    {
        lock (_gate) Remove(typeof(T), handler);
    }

    /// <summary>派发事件。订阅者抛出的异常会被记录而不是向上抛，避免一个 UI bug 中断整个模拟。</summary>
    public void Publish<T>(T evt) where T : IGameEvent
    {
        Delegate[] snapshot;
        lock (_gate)
        {
            if (!_handlers.TryGetValue(typeof(T), out List<Delegate>? list) || list.Count == 0) return;
            // 复制一份：允许订阅者在回调里退订或再订阅。
            snapshot = [.. list];
        }

        foreach (Delegate d in snapshot)
        {
            try
            {
                ((Action<T>)d)(evt);
            }
            catch (Exception ex)
            {
                LastHandlerError = ex;
            }
        }
    }

    /// <summary>是否有人订阅某类事件（用于跳过昂贵的事件构造）。</summary>
    public bool HasSubscribers<T>() where T : IGameEvent
    {
        lock (_gate) return _handlers.TryGetValue(typeof(T), out List<Delegate>? list) && list.Count > 0;
    }

    /// <summary>最近一次订阅者抛出的异常，便于诊断而不至于中断游戏。</summary>
    public Exception? LastHandlerError { get; private set; }

    /// <summary>清空所有订阅。</summary>
    public void Clear()
    {
        lock (_gate) _handlers.Clear();
    }

    private void Remove(Type eventType, Delegate handler)
    {
        if (!_handlers.TryGetValue(eventType, out List<Delegate>? list)) return;
        list.Remove(handler);
        if (list.Count == 0) _handlers.Remove(eventType);
    }

    private sealed class Subscription : IDisposable
    {
        private readonly GameEventBus _bus;
        private readonly Type _eventType;
        private readonly Delegate _handler;
        private bool _disposed;

        public Subscription(GameEventBus bus, Type eventType, Delegate handler)
        {
            _bus = bus;
            _eventType = eventType;
            _handler = handler;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            lock (_bus._gate) _bus.Remove(_eventType, _handler);
        }
    }
}
