using System.Diagnostics;
using System.Reflection;
using System.Text;

namespace NekoClicker.Core.Tests;

/// <summary>标记一个测试方法。方法可以是静态的，也可以是实例方法。</summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class TestAttribute : Attribute
{
    /// <summary>可选：覆盖显示名。</summary>
    public string? Name { get; init; }
}

/// <summary>断言失败时抛出。</summary>
public sealed class AssertionException : Exception
{
    /// <summary>创建异常。</summary>
    public AssertionException(string message) : base(message)
    {
    }
}

/// <summary>
/// 极简断言库。<para>
/// 本仓库刻意不依赖 xunit/NUnit：框架本身零第三方依赖，测试工具链也不该成为例外
/// （在无网络的构建环境里尤其如此）。断言失败一律抛 <see cref="AssertionException"/>，
/// 由 <see cref="TestRunner"/> 捕获并汇报。
/// </para>
/// </summary>
public static class Check
{
    /// <summary>断言为真。</summary>
    public static void True(bool condition, string message = "")
        => Arg(condition, message.Length == 0 ? "期望为 true，实际为 false。" : message);

    /// <summary>断言为假。</summary>
    public static void False(bool condition, string message = "")
        => Arg(!condition, message.Length == 0 ? "期望为 false，实际为 true。" : message);

    /// <summary>断言相等。</summary>
    public static void Equal<T>(T expected, T actual, string message = "")
    {
        if (EqualityComparer<T>.Default.Equals(expected, actual)) return;
        throw new AssertionException(
            $"{Prefix(message)}期望 <{expected}>，实际 <{actual}>。");
    }

    /// <summary>断言不相等。</summary>
    public static void NotEqual<T>(T unexpected, T actual, string message = "")
    {
        if (!EqualityComparer<T>.Default.Equals(unexpected, actual)) return;
        throw new AssertionException($"{Prefix(message)}期望不等于 <{unexpected}>，但相等。");
    }

    /// <summary>断言绝对误差在容差内。</summary>
    public static void Close(double expected, double actual, double tolerance = 1e-9, string message = "")
    {
        if (double.IsNaN(actual)) throw new AssertionException($"{Prefix(message)}实际值为 NaN。");
        if (Math.Abs(expected - actual) <= tolerance) return;
        throw new AssertionException(
            $"{Prefix(message)}期望 {expected} ± {tolerance}，实际 {actual}（差值 {Math.Abs(expected - actual)}）。");
    }

    /// <summary>断言相对误差在容差内（适合比较量级很大的值）。</summary>
    public static void CloseRelative(double expected, double actual, double tolerance = 1e-6, string message = "")
    {
        if (double.IsNaN(actual)) throw new AssertionException($"{Prefix(message)}实际值为 NaN。");
        double scale = Math.Max(Math.Abs(expected), Math.Abs(actual));
        if (scale == 0) return;
        double error = Math.Abs(expected - actual) / scale;
        if (error <= tolerance) return;
        throw new AssertionException(
            $"{Prefix(message)}期望 ≈{expected}，实际 {actual}（相对误差 {error:E3} > {tolerance:E3}）。");
    }

    /// <summary>断言大于。</summary>
    public static void Greater(double actual, double threshold, string message = "")
    {
        if (actual > threshold) return;
        throw new AssertionException($"{Prefix(message)}期望 &gt; {threshold}，实际 {actual}。");
    }

    /// <summary>断言大于等于。</summary>
    public static void AtLeast(double actual, double threshold, string message = "")
    {
        if (actual >= threshold) return;
        throw new AssertionException($"{Prefix(message)}期望 ≥ {threshold}，实际 {actual}。");
    }

    /// <summary>断言小于等于。</summary>
    public static void AtMost(double actual, double threshold, string message = "")
    {
        if (actual <= threshold) return;
        throw new AssertionException($"{Prefix(message)}期望 ≤ {threshold}，实际 {actual}。");
    }

    /// <summary>断言字符串包含子串。</summary>
    public static void Contains(string haystack, string needle, string message = "")
    {
        if (haystack.Contains(needle, StringComparison.Ordinal)) return;
        throw new AssertionException($"{Prefix(message)}期望包含 <{needle}>，实际为 <{haystack}>。");
    }

    /// <summary>断言值有限（非 NaN / 非无穷）。</summary>
    public static void Finite(double value, string message = "")
    {
        if (double.IsFinite(value)) return;
        throw new AssertionException($"{Prefix(message)}期望有限值，实际为 {value}。");
    }

    /// <summary>断言抛出指定异常。</summary>
    public static TException Throws<TException>(Action action, string message = "")
        where TException : Exception
    {
        try
        {
            action();
        }
        catch (TException ex)
        {
            return ex;
        }
        catch (Exception ex)
        {
            throw new AssertionException(
                $"{Prefix(message)}期望抛出 {typeof(TException).Name}，实际抛出 {ex.GetType().Name}：{ex.Message}");
        }
        throw new AssertionException($"{Prefix(message)}期望抛出 {typeof(TException).Name}，但没有抛出任何异常。");
    }

    /// <summary>断言不为 null。</summary>
    public static void NotNull(object? value, string message = "")
    {
        if (value is not null) return;
        throw new AssertionException($"{Prefix(message)}期望非 null，实际为 null。");
    }

    /// <summary>断言为 null。</summary>
    public static void Null(object? value, string message = "")
    {
        if (value is null) return;
        throw new AssertionException($"{Prefix(message)}期望 null，实际为 <{value}>。");
    }

    /// <summary>直接失败。</summary>
    public static void Fail(string message) => throw new AssertionException(message);

    private static void Arg(bool condition, string message)
    {
        if (!condition) throw new AssertionException(message);
    }

    private static string Prefix(string message) => message.Length == 0 ? string.Empty : message + "｜";
}

/// <summary>测试结果统计。</summary>
/// <param name="Passed">通过数。</param>
/// <param name="Failed">失败数。</param>
/// <param name="Failures">失败详情。</param>
public sealed record TestSummary(int Passed, int Failed, IReadOnlyList<string> Failures)
{
    /// <summary>是否全部通过。</summary>
    public bool AllPassed => Failed == 0;
}

/// <summary>反射驱动的测试运行器：扫描程序集里所有 <see cref="TestAttribute"/> 方法并逐个执行。</summary>
public static class TestRunner
{
    /// <summary>运行全部测试。</summary>
    /// <param name="assembly">目标程序集；默认当前程序集。</param>
    /// <param name="filter">可选的名称过滤（包含匹配）。</param>
    /// <param name="verbose">是否打印每个通过用例。</param>
    /// <param name="timing">是否逐条计时并打印最慢的一批（见 <c>--timing</c>）。</param>
    /// <param name="jobs">并发度；<c>1</c> 为串行（见 <c>--serial</c> / <c>--jobs</c>）。</param>
    public static TestSummary RunAll(
        Assembly? assembly = null,
        string? filter = null,
        bool verbose = true,
        bool timing = false,
        int jobs = 0)
    {
        assembly ??= Assembly.GetExecutingAssembly();

        // 计时模式下不逐条打勾：411 行 ✓ 会把真正要看的表顶出屏幕。
        if (timing) verbose = false;

        List<(Type Type, MethodInfo Method)> tests = [];
        foreach (Type type in assembly.GetTypes().OrderBy(t => t.FullName, StringComparer.Ordinal))
        {
            foreach (MethodInfo method in type.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance))
            {
                if (method.GetCustomAttribute<TestAttribute>() is null) continue;
                if (method.GetParameters().Length != 0) continue;

                string display = $"{type.Name}.{method.Name}";
                if (filter is not null && !display.Contains(filter, StringComparison.OrdinalIgnoreCase)) continue;
                tests.Add((type, method));
            }
        }

        if (jobs <= 0) jobs = Environment.ProcessorCount;
        jobs = Math.Clamp(jobs, 1, Math.Max(1, tests.Count));

        var slots = new TestOutcome[tests.Count];
        var total = Stopwatch.StartNew();

        if (jobs == 1)
        {
            for (int i = 0; i < tests.Count; i++) slots[i] = RunOne(tests[i]);
        }
        else
        {
            // 输出必须缓冲后按原顺序回放：422 条用例里有一批会打印诊断
            // （最慢的那条一次打九行），并行直写会把它们搅成一团乱码，
            // 而"读得懂的输出"正是这个自研运行器存在的理由之一。
            var capture = new CapturingWriter(Console.Out, tests.Count);
            TextWriter original = Console.Out;
            Console.SetOut(capture);

            try
            {
                Parallel.For(
                    0,
                    tests.Count,
                    new ParallelOptions { MaxDegreeOfParallelism = jobs },
                    i =>
                    {
                        CapturingWriter.BeginSlot(i);
                        slots[i] = RunOne(tests[i]);
                    });
            }
            finally
            {
                Console.SetOut(original);
            }

            for (int i = 0; i < tests.Count; i++) capture.Replay(i);
        }

        total.Stop();

        int passed = 0;
        List<string> failures = [];
        List<(string Display, double Seconds, bool Passed)> timings = [];

        for (int i = 0; i < tests.Count; i++)
        {
            TestOutcome outcome = slots[i];
            string display = $"{tests[i].Type.Name}.{tests[i].Method.Name}";

            if (outcome.Passed) passed++;
            else failures.Add($"{display}\n      {outcome.Error}");

            if (verbose || !outcome.Passed)
            {
                Console.WriteLine(outcome.Passed
                    ? $"  \u001b[32m✓\u001b[0m {display}"
                    : $"  \u001b[31m✗\u001b[0m {display}\n      \u001b[31m{outcome.Error}\u001b[0m");
            }

            if (timing) timings.Add((display, outcome.Seconds, outcome.Passed));
        }

        Console.WriteLine();
        if (failures.Count == 0)
            Console.WriteLine($"\u001b[32m全部通过：{passed} 个用例。\u001b[0m");
        else
            Console.WriteLine($"\u001b[31m{passed} 通过 / {failures.Count} 失败（共 {tests.Count}）。\u001b[0m");

        if (timing) PrintTimings(timings, total.Elapsed.TotalSeconds);

        return new TestSummary(passed, failures.Count, failures);
    }

    /// <summary>跑一条用例，把它打印的东西留给调用方决定什么时候放出来。</summary>
    private static TestOutcome RunOne((Type Type, MethodInfo Method) test)
    {
        var watch = Stopwatch.StartNew();

        try
        {
            object? target = test.Method.IsStatic ? null : Activator.CreateInstance(test.Type);
            test.Method.Invoke(target, null);
            return new TestOutcome(true, null, watch.Elapsed.TotalSeconds);
        }
        catch (Exception ex)
        {
            Exception actual = ex;
            if (ex is TargetInvocationException { InnerException: { } inner }) actual = inner;
            return new TestOutcome(false, $"{actual.GetType().Name}: {actual.Message}", watch.Elapsed.TotalSeconds);
        }
        finally
        {
            watch.Stop();
        }
    }

    /// <summary>一条用例的结果。</summary>
    private readonly record struct TestOutcome(bool Passed, string? Error, double Seconds);

    /// <summary>
    /// 按"用例"分桶的 <see cref="TextWriter"/>：每个线程写自己那一桶，跑完再按原顺序倒出来。<para>
    /// 为什么必须这么做：并行直写 stdout 会把各用例的诊断行交错在一起。这个运行器是自研的，
    /// 它唯一比现成框架强的地方就是输出可读——为了并行把这点丢掉不划算。
    /// </para>
    /// <para>
    /// 分桶靠 <see cref="ThreadStaticAttribute"/>：<see cref="Parallel.For(int, int, ParallelOptions, Action{int})"/>
    /// 的一次迭代在同一个线程上跑到底，所以槽位在一次用例内是稳定的。用例自己起的线程
    /// 没有槽位，直接落到原始输出上（不缓冲也不阻塞）。
    /// </para>
    /// </summary>
    private sealed class CapturingWriter : TextWriter
    {
        private readonly TextWriter _sink;
        private readonly StringBuilder[] _buckets;

        [ThreadStatic]
        private static int _slot;

        public CapturingWriter(TextWriter sink, int count)
        {
            _sink = sink;
            _buckets = new StringBuilder[count];
            for (int i = 0; i < count; i++) _buckets[i] = new StringBuilder();
        }

        /// <inheritdoc />
        public override Encoding Encoding => _sink.Encoding;

        /// <summary>把当前线程的写入指向第 <paramref name="slot"/> 条用例。</summary>
        public static void BeginSlot(int slot) => _slot = slot;

        /// <summary>把某条用例缓冲的输出倒到原始输出上（按顺序调用即为原顺序）。</summary>
        public void Replay(int slot)
        {
            StringBuilder bucket = _buckets[slot];
            if (bucket.Length == 0) return;

            _sink.Write(bucket.ToString());
            bucket.Clear();
        }

        /// <inheritdoc />
        public override void Write(char value)
        {
            StringBuilder? bucket = Bucket();
            if (bucket is null) _sink.Write(value);
            else bucket.Append(value);
        }

        /// <inheritdoc />
        public override void Write(string? value)
        {
            if (value is null) return;

            StringBuilder? bucket = Bucket();
            if (bucket is null) _sink.Write(value);
            else bucket.Append(value);
        }

        private StringBuilder? Bucket()
        {
            int slot = _slot;
            return slot >= 0 && slot < _buckets.Length ? _buckets[slot] : null;
        }
    }

    /// <summary>最慢的一批</summary>
    private const int TimingHead = 40;

    /// <summary>
    /// 打印逐条计时。存在的理由：按类名做子串过滤去估耗时**已经错过两次**——
    /// 过滤器 `ContentTests` 会把 8 个 <c>*ContentTests</c> 类一起吞掉，
    /// 于是那张"按类耗时表"里的数字根本不是那个类的时间。
    /// 要砍耗时就只能看逐条的真实数字，而运行器是唯一拿得到它的地方。
    /// <para>
    /// <b>占比的分母是"逐条之和"，不是总墙钟</b>：并行时各用例互相抢 CPU，逐条墙钟之和
    /// 会大于总墙钟，拿它去除总时间会算出"占 452%"这种胡话（真出现过）。用逐条之和，
    /// 回答的就是"这条占全部计算量的多少"，与并发度无关。
    /// </para>
    /// </summary>
    private static void PrintTimings(List<(string Display, double Seconds, bool Passed)> timings, double wallSeconds)
    {
        double work = timings.Sum(t => t.Seconds);

        Console.WriteLine();
        Console.WriteLine($"\u001b[36m=== 逐条计时：{timings.Count} 条，墙钟 {wallSeconds:0.0}s，"
                          + $"逐条合计 {work:0.0}s（{work / Math.Max(0.001, wallSeconds):0.0}x 并发）===\u001b[0m");
        Console.WriteLine("  占比按计算量算；并行下逐条墙钟含 CPU 竞争放大，判断真实代价请跑 --serial。");

        List<(string Display, double Seconds, bool Passed)> sorted =
            [.. timings.OrderByDescending(t => t.Seconds)];

        int shown = 0;
        foreach ((string display, double seconds, bool ok) in sorted)
        {
            shown++;
            if (shown > TimingHead) continue;

            string share = work <= 0 ? "  -  " : $"{seconds / work * 100,5:0.0}%";
            string mark = ok ? " " : "\u001b[31m✗\u001b[0m";
            Console.WriteLine($"  {seconds,8:0.00}s {share}{mark} {display}");
        }

        double headSum = sorted.Take(TimingHead).Sum(t => t.Seconds);

        Console.WriteLine();
        Console.WriteLine($"  最慢 {Math.Min(TimingHead, sorted.Count)} 条合计 {headSum:0.0}s"
                          + $"（占计算量 {(work <= 0 ? 0 : headSum / work * 100):0.0}%）");

        // 分布比"最慢一条"更有用：砍一个 100s 的用例和砍一百个 1s 的用例，
        // 对作者心智的代价完全不同。
        foreach (double threshold in new[] { 60.0, 30.0, 10.0, 5.0, 1.0 })
        {
            List<(string Display, double Seconds, bool Passed)> bucket =
                [.. timings.Where(t => t.Seconds >= threshold)];
            double bucketSum = bucket.Sum(t => t.Seconds);
            Console.WriteLine($"  ≥{threshold,5:0}s：{bucket.Count,4} 条，合计 {bucketSum,7:0.0}s"
                              + $"（占计算量 {(work <= 0 ? 0 : bucketSum / work * 100):0.0}%）");
        }
    }
}
