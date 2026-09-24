using System.Reflection;

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
    public static TestSummary RunAll(Assembly? assembly = null, string? filter = null, bool verbose = true)
    {
        assembly ??= Assembly.GetExecutingAssembly();

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

        int passed = 0;
        List<string> failures = [];

        foreach ((Type type, MethodInfo method) in tests)
        {
            string display = $"{type.Name}.{method.Name}";
            try
            {
                object? target = method.IsStatic ? null : Activator.CreateInstance(type);
                method.Invoke(target, null);
                passed++;
                if (verbose) Console.WriteLine($"  \u001b[32m✓\u001b[0m {display}");
            }
            catch (Exception ex)
            {
                Exception actual = ex;
                if (ex is TargetInvocationException { InnerException: { } inner }) actual = inner;
                failures.Add($"{display}\n      {actual.GetType().Name}: {actual.Message}");
                Console.WriteLine($"  \u001b[31m✗\u001b[0m {display}");
                Console.WriteLine($"      \u001b[31m{actual.GetType().Name}\u001b[0m: {actual.Message}");
            }
        }

        Console.WriteLine();
        if (failures.Count == 0)
            Console.WriteLine($"\u001b[32m全部通过：{passed} 个用例。\u001b[0m");
        else
            Console.WriteLine($"\u001b[31m{passed} 通过 / {failures.Count} 失败（共 {tests.Count}）。\u001b[0m");

        return new TestSummary(passed, failures.Count, failures);
    }
}
