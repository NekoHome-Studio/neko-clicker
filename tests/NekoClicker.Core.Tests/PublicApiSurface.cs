using System.Globalization;
using System.Reflection;
using System.Text;

namespace NekoClicker.Core.Tests;

/// <summary>
/// 公开 API 表面的<b>快照</b>工具：把一个程序集的公开类型与成员摊平成一份可 diff 的文本。<para>
/// 这是本项目"版本承诺"的执法手段。框架对使用者的承诺是：<b>公开 API 只增不改</b>——
/// 只要这份快照变了，就一定有人需要改代码或需要一次 major 版本，而这两件事都必须是有意为之的。
/// </para>
/// <para>
/// 为什么用 <see cref="System.Reflection"/> 手写、而不是引入官方工具：
/// <c>Microsoft.DotNet.ApiCompat</c> 的比较基准是"上一个已发布的包"，而本仓库从来没有发布过包；
/// <c>GenAPI</c> 在 SDK 里也不随附。更要紧的是本仓库的既定前提——<b>零第三方依赖、可离线构建</b>：
/// 测试项目的 targeting pack 里连 <c>System.Reflection.Metadata</c> 都没有，
/// 引一个 NuGet 包就等于把"无网络也能构建"这条前提弄丢。
/// </para>
/// <para>
/// 输出刻意<b>不含时间戳、不含路径、不含版本号</b>：同一份源码必须产出逐字节相同的文本，
/// 否则"快照变了"就不再等于"API 变了"，守卫会退化成噪音。
/// </para>
/// </summary>
public static class PublicApiSurface
{
    /// <summary>快照格式版本。格式本身变化时递增，避免把"格式改了"误读成"API 改了"。</summary>
    public const string FormatVersion = "1";

    /// <summary>快照文件里第一行的前缀。</summary>
    public const string HeaderPrefix = "# NekoClicker 公开 API 快照";

    /// <summary>
    /// 摊平一个程序集的公开 API，返回按序排列的文本行。<para>
    /// 首行记下"这份快照描述的是哪个版本"，这是把 API 变化与版本号绑在一起的唯一凭据：
    /// <c>VersionTests</c> 要求快照里的版本与当前程序集版本一致，
    /// 于是"只改 API 不升版本"这条路走不通——重新生成快照会写上当前版本，
    /// 而当前版本没升，两个数字就对不上。
    /// </para>
    /// </summary>
    /// <param name="assembly">目标程序集。</param>
    public static string[] Dump(Assembly assembly)
    {
        ArgumentNullException.ThrowIfNull(assembly);

        NullabilityInfoContext nullability = new();
        List<string> lines =
        [
            $"{HeaderPrefix} format={FormatVersion} assembly={assembly.GetName().Name} version={VersionOf(assembly)}",
        ];

        foreach (Type type in assembly.GetExportedTypes().OrderBy(t => t.FullName, StringComparer.Ordinal))
        {
            lines.Add(string.Empty);
            lines.Add(TypeLine(type, nullability));

            // 构造顺序无关：成员按行文本排序，保证同一份源码产出同一份快照。
            lines.AddRange(MemberLines(type, nullability).OrderBy(line => line, StringComparer.Ordinal));
        }

        // 只去掉末尾空行，中间的空行是"分隔每个类型"的一部分。
        while (lines.Count > 0 && lines[^1].Length == 0) lines.RemoveAt(lines.Count - 1);

        return [.. lines];
    }

    /// <summary>
    /// 读程序集的语义版本；<c>null</c> 表示这个程序集没有版本元数据。<para>
    /// 这里刻意不依赖 <c>NekoClicker.Core.ApiVersion</c>——快照工具要能对<b>任意</b>程序集工作，
    /// 否则"用被测对象校验它自己"会让守卫失去独立性。
    /// </para>
    /// </summary>
    public static string? VersionOf(Assembly assembly)
    {
        ArgumentNullException.ThrowIfNull(assembly);

        string? informational = assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion;

        if (string.IsNullOrWhiteSpace(informational))
        {
            return assembly.GetName().Version?.ToString(3);
        }

        // .NET 8 起 InformationalVersion 会被追加 "+<commit sha>"，版本号本身不该带构建元数据。
        int plus = informational.IndexOf('+');
        return plus >= 0 ? informational[..plus] : informational;
    }

    /// <summary>从快照首行里取出它记录的版本号。</summary>
    public static string? VersionInHeader(string headerLine)
    {
        ArgumentNullException.ThrowIfNull(headerLine);

        const string Marker = "version=";
        int at = headerLine.IndexOf(Marker, StringComparison.Ordinal);
        return at < 0 ? null : headerLine[(at + Marker.Length)..].Trim();
    }

    /// <summary>把快照行拼成一份以 LF 换行的文本（提交进仓库的形态）。</summary>
    public static string Render(IEnumerable<string> lines)
        => string.Join("\n", lines) + "\n";

    /// <summary>把提交进仓库的快照文本拆回行序列。</summary>
    public static string[] Parse(string text)
        => text.Replace("\r\n", "\n", StringComparison.Ordinal).TrimEnd('\n').Split('\n');

    /// <summary>
    /// 比较两份快照，返回人类可读的差异说明。<para>
    /// <b>三类改动分开报</b>，因为它们对应两种完全不同的处理：
    /// 少了什么 / 改了什么 = 不兼容改动（要么改回去，要么升 major 并写迁移说明）；
    /// 多了什么 = 新能力（应该升 minor 并更新快照）。
    /// </para>
    /// </summary>
    /// <returns>没有差异时返回 <c>null</c>，否则返回一份多行说明。</returns>
    public static string? Compare(IReadOnlyList<string> saved, IReadOnlyList<string> current)
    {
        ArgumentNullException.ThrowIfNull(saved);
        ArgumentNullException.ThrowIfNull(current);

        HashSet<string> savedSet = [.. saved.Where(line => line.Length > 0 && !line.StartsWith('#'))];
        HashSet<string> currentSet = [.. current.Where(line => line.Length > 0 && !line.StartsWith('#'))];

        List<string> removed = [.. savedSet.Except(currentSet).OrderBy(line => line, StringComparer.Ordinal)];
        List<string> added = [.. currentSet.Except(savedSet).OrderBy(line => line, StringComparer.Ordinal)];

        if (removed.Count == 0 && added.Count == 0) return null;

        StringBuilder report = new();
        report.AppendLine($"公开 API 快照对不上：少了 {removed.Count} 项、多了 {added.Count} 项。");

        if (removed.Count > 0)
        {
            report.AppendLine();
            report.AppendLine("  [不兼容] 快照里有、现在没有了（要么改回去，要么升 MAJOR 并写迁移说明）：");
            foreach (string line in removed) report.AppendLine("    - " + line);
        }

        if (added.Count > 0)
        {
            report.AppendLine();
            report.AppendLine("  [新增] 现在有、快照里没有（只增不改的话升 MINOR 并更新快照）：");
            foreach (string line in added) report.AppendLine("    + " + line);
        }

        report.AppendLine();
        report.Append("  快照文件：src/NekoClicker.Core/PublicApi.txt");
        return report.ToString();
    }

    // ---------------------------------------------------------------- 类型行

    private static string TypeLine(Type type, NullabilityInfoContext nullability)
    {
        string kind = KindOf(type);
        StringBuilder line = new();

        if (type.IsNested) line.Append(NestedVisibility(type) + ' ');
        else line.Append(type.IsPublic ? "public " : "internal ");

        // 修饰符只对 class 有意义：
        //   - 静态类的元数据形态就是 abstract + sealed，写 "static" 比 "abstract sealed" 贴近源码；
        //   - struct / enum / delegate 在 C# 里不可能显式写 sealed/abstract，照搬元数据会产出
        //     "sealed struct" 这种不存在的写法（快照是给人读的，得是合法的 C#）。
        if (kind == "class")
        {
            if (type.IsAbstract && type.IsSealed) line.Append("static ");
            else if (type.IsSealed) line.Append("sealed ");
            else if (type.IsAbstract) line.Append("abstract ");
        }

        line.Append(kind).Append(' ').Append(TypeName(type));

        if (type.IsEnum)
        {
            // 底层类型是公开契约的一部分（改成 uint 会让所有强转处失效）。
            line.Append(" : ").Append(TypeName(Enum.GetUnderlyingType(type)));
            AppendGenericParameters(line, type, nullability);
            return line.ToString();
        }

        // delegate 的 BaseType 是 MulticastDelegate，struct 是 ValueType，都不该出现在快照里：
        // 它们改不了，写出来只是噪音。
        List<string> bases = [];
        if (kind == "class" && type.BaseType is { } baseType && baseType != typeof(object))
        {
            bases.Add(TypeName(baseType));
        }

        bases.AddRange(
            type.GetInterfaces()
                .Where(candidate => !type.GetInterfaces().Any(other =>
                    other != candidate && other.GetInterfaces().Contains(candidate)))
                .Select(TypeName));

        if (bases.Count > 0) line.Append(" : ").Append(string.Join(", ", bases.OrderBy(name => name, StringComparer.Ordinal)));

        AppendGenericParameters(line, type, nullability);
        return line.ToString();
    }

    private static string KindOf(Type type)
    {
        if (type.IsEnum) return "enum";
        if (type.IsValueType) return "struct";
        if (type.IsInterface) return "interface";
        if (typeof(Delegate).IsAssignableFrom(type)) return "delegate";
        return "class";
    }

    private static string NestedVisibility(Type type)
        => type.IsNestedPublic ? "public"
            : type.IsNestedFamily ? "protected"
            : type.IsNestedFamORAssem ? "protected internal"
            : "internal";

    private static void AppendGenericParameters(StringBuilder line, Type type, NullabilityInfoContext nullability)
    {
        if (!type.IsGenericTypeDefinition) return;

        Type[] parameters = type.GetGenericArguments();
        if (parameters.Length == 0) return;

        line.Append('<');
        for (int i = 0; i < parameters.Length; i++)
        {
            if (i > 0) line.Append(", ");
            Type parameter = parameters[i];
            line.Append(parameter.Name);

            GenericParameterAttributes attributes = parameter.GenericParameterAttributes;
            List<string> constraints = [];

            GenericParameterAttributes variance = attributes & GenericParameterAttributes.VarianceMask;
            if (variance == GenericParameterAttributes.Covariant) constraints.Add("out");
            else if (variance == GenericParameterAttributes.Contravariant) constraints.Add("in");

            if ((attributes & GenericParameterAttributes.ReferenceTypeConstraint) != 0) constraints.Add("class");
            if ((attributes & GenericParameterAttributes.NotNullableValueTypeConstraint) != 0) constraints.Add("struct");
            if ((attributes & GenericParameterAttributes.DefaultConstructorConstraint) != 0
                && (attributes & GenericParameterAttributes.NotNullableValueTypeConstraint) == 0)
            {
                constraints.Add("new()");
            }

            constraints.AddRange(
                parameter.GetGenericParameterConstraints()
                    .Where(constraint => constraint != typeof(ValueType))
                    .Select(TypeName));

            if (constraints.Count > 0) line.Append(" : ").Append(string.Join(", ", constraints));
        }

        line.Append('>');
    }

    // ---------------------------------------------------------------- 成员行

    private static IEnumerable<string> MemberLines(Type type, NullabilityInfoContext nullability)
    {
        const BindingFlags Declared =
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

        if (type.IsEnum)
        {
            foreach (string name in Enum.GetNames(type))
            {
                // 枚举成员的值是契约：有人存过它、也有人序列化过它。
                object value = Enum.Parse(type, name);
                yield return $"  field {name} = {Literal(value)}";
            }

            yield break;
        }

        foreach (FieldInfo field in type.GetFields(Declared))
        {
            if (!IsVisible(field)) continue;

            StringBuilder line = new("  field ");
            if (field.IsLiteral) line.Append("const ");
            else if (field.IsStatic && field.IsInitOnly) line.Append("static readonly ");
            else if (field.IsStatic) line.Append("static ");
            else if (field.IsInitOnly) line.Append("readonly ");

            line.Append(TypeName(field.FieldType, field, nullability)).Append(' ').Append(field.Name);
            if (field.IsLiteral) line.Append(" = ").Append(Literal(field.GetRawConstantValue()));

            yield return line.ToString();
        }

        foreach (ConstructorInfo constructor in type.GetConstructors(Declared))
        {
            if (!IsVisible(constructor)) continue;
            yield return "  ctor " + TypeName(type) + Parameters(constructor.GetParameters(), nullability);
        }

        foreach (PropertyInfo property in type.GetProperties(Declared))
        {
            MethodInfo? getter = property.GetMethod;
            MethodInfo? setter = property.SetMethod;

            if (!IsVisible(getter) && !IsVisible(setter)) continue;

            MethodInfo? primary = IsVisible(getter) ? getter : setter;
            StringBuilder line = new("  prop ");

            if (primary!.IsStatic) line.Append("static ");
            if (primary.IsAbstract) line.Append("abstract ");
            else if (primary.IsVirtual && !primary.IsFinal) line.Append("virtual ");

            line.Append(TypeName(property.PropertyType, property, nullability)).Append(' ').Append(property.Name);

            // 索引器：参数直接写进属性签名。
            ParameterInfo[] indexParameters = property.GetIndexParameters();
            if (indexParameters.Length > 0) line.Append(Parameters(indexParameters, nullability));

            List<string> accessors = [];
            if (IsVisible(getter))
            {
                string accessor = "get;";
                accessor = PrependAccessorVisibility(accessor, getter!, property);
                accessors.Add(accessor);
            }

            if (IsVisible(setter))
            {
                // init-only 与"普通 set"对调用方几乎等价，但序列化/反射场景下不同，照样记下来。
                bool isInit = setter!.ReturnParameter.GetRequiredCustomModifiers()
                    .Any(modifier => modifier.FullName == "System.Runtime.CompilerServices.IsExternalInit");

                string accessor = (isInit ? "init;" : "set;");
                accessor = PrependAccessorVisibility(accessor, setter, property);
                accessors.Add(accessor);
            }

            if (accessors.Count > 0) line.Append(" { ").Append(string.Join(' ', accessors)).Append(" }");

            yield return line.ToString();
        }

        foreach (EventInfo @event in type.GetEvents(Declared))
        {
            MethodInfo? adder = @event.AddMethod;
            if (!IsVisible(adder)) continue;

            StringBuilder line = new("  event ");
            if (adder!.IsStatic) line.Append("static ");
            if (adder.IsAbstract) line.Append("abstract ");
            else if (adder.IsVirtual && !adder.IsFinal) line.Append("virtual ");

            line.Append(TypeName(@event.EventHandlerType!, @event, nullability)).Append(' ').Append(@event.Name);
            yield return line.ToString();
        }

        foreach (MethodInfo method in type.GetMethods(Declared))
        {
            if (!IsVisible(method)) continue;
            if (IsAccessor(method)) continue;
            if (IsCompilerSynthesizedName(method.Name)) continue;

            StringBuilder line = new("  method ");
            if (method.IsStatic) line.Append("static ");
            if (method.IsAbstract) line.Append("abstract ");
            else if (method.IsVirtual && !method.IsFinal) line.Append("virtual ");

            line.Append(TypeName(method.ReturnType, method.ReturnParameter, nullability))
                .Append(' ')
                .Append(method.Name);

            if (method.IsGenericMethodDefinition)
            {
                line.Append('<')
                    .Append(string.Join(", ", method.GetGenericArguments().Select(argument => argument.Name)))
                    .Append('>');
            }

            line.Append(Parameters(method.GetParameters(), nullability));
            yield return line.ToString();
        }
    }

    private static string PrependAccessorVisibility(string accessor, MethodInfo method, PropertyInfo property)
    {
        // 只有当访问器比属性本身更窄时才写出来——"private set" 和 "public set" 是两回事。
        string propertyVisibility = Visibility(property);
        string accessorVisibility = Visibility(method);
        return accessorVisibility == propertyVisibility ? accessor : accessorVisibility + " " + accessor;
    }

    private static string Parameters(ParameterInfo[] parameters, NullabilityInfoContext nullability)
    {
        if (parameters.Length == 0) return "()";

        List<string> rendered = [];
        foreach (ParameterInfo parameter in parameters)
        {
            StringBuilder text = new();
            if (parameter.IsDefined(typeof(ParamArrayAttribute), inherit: false)) text.Append("params ");
            if (parameter.ParameterType.IsByRef)
            {
                text.Append(parameter.IsOut ? "out " : parameter.IsIn ? "in " : "ref ");
            }

            text.Append(TypeName(parameter.ParameterType, parameter, nullability)).Append(' ').Append(parameter.Name);

            if (parameter.HasDefaultValue)
            {
                text.Append(" = ").Append(Literal(parameter.DefaultValue));
            }

            rendered.Add(text.ToString());
        }

        return "(" + string.Join(", ", rendered) + ")";
    }

    private static bool IsVisible(MethodInfo? method)
        => method is not null && (method.IsPublic || method.IsFamily || method.IsFamilyOrAssembly);

    /// <summary>
    /// 属性 / 事件的访问器——它们已经由属性行、事件行代表，不再单独出一行方法行。<para>
    /// 这两个判据是<b>公开</b>的，因为"Dumper 会渲染哪些方法"必须只有一个定义：
    /// 覆盖度用例（<c>PublicApi_CoversEveryExportedTypeAndEveryPublicMember</c>）要按同一套规则
    /// 枚举成员，否则两边会各自漂移——实测踩过：覆盖度用例一度用 <c>IsSpecialName</c> 一刀切，
    /// 而运算符（<c>op_Equality</c>）也是 special-name，于是运算符被覆盖度用例整个跳过。
    /// </para>
    /// </summary>
    public static bool IsAccessor(MethodInfo method)
    {
        ArgumentNullException.ThrowIfNull(method);

        return method.IsSpecialName
            && (method.Name.StartsWith("get_", StringComparison.Ordinal)
                || method.Name.StartsWith("set_", StringComparison.Ordinal)
                || method.Name.StartsWith("add_", StringComparison.Ordinal)
                || method.Name.StartsWith("remove_", StringComparison.Ordinal));
    }

    /// <summary>
    /// 编译器合成的成员不进快照：record 的 <c>&lt;Clone&gt;$</c> 之类的名字里带尖括号
    /// （C# 不允许标识符出现这两个字符），它们随编译器实现变化，写进快照只会制造假警报。<para>
    /// 注意 record 另外合成的 <c>Equals</c> / <c>GetHashCode</c> / <c>op_Equality</c> / <c>Deconstruct</c>
    /// 是<b>保留</b>的：名字合法、调用方写得出，而且它们跟着属性集合变化而变化——
    /// 跳过它们反而会漏掉"给 record 加了一个属性"这种真实的 API 变化。
    /// </para>
    /// </summary>
    public static bool IsCompilerSynthesizedName(string name)
    {
        ArgumentNullException.ThrowIfNull(name);

        return name.Contains('<', StringComparison.Ordinal) || name.Contains('>', StringComparison.Ordinal);
    }

    /// <summary>一个方法是否会在快照里单独占一行。</summary>
    public static bool IsRenderedAsMethodLine(MethodInfo method)
    {
        ArgumentNullException.ThrowIfNull(method);

        return IsVisible(method) && !IsAccessor(method) && !IsCompilerSynthesizedName(method.Name);
    }

    private static bool IsVisible(FieldInfo field)
        => field.IsPublic || field.IsFamily || field.IsFamilyOrAssembly;

    private static bool IsVisible(ConstructorInfo constructor)
        => constructor.IsPublic || constructor.IsFamily || constructor.IsFamilyOrAssembly;

    private static string Visibility(MethodInfo method)
        => method.IsPublic ? "public"
            : method.IsFamily ? "protected"
            : method.IsFamilyOrAssembly ? "protected internal"
            : "private";

    private static string Visibility(PropertyInfo property)
        => property.GetMethod is { } getter && getter.IsPublic
            || property.SetMethod is { } setter && setter.IsPublic ? "public"
            : property.GetMethod is { } protectedGetter && protectedGetter.IsFamily
              || property.SetMethod is { } protectedSetter && protectedSetter.IsFamily ? "protected"
            : property.GetMethod is { } internalGetter && internalGetter.IsFamilyOrAssembly
              || property.SetMethod is { } internalSetter && internalSetter.IsFamilyOrAssembly ? "protected internal"
            : "private";

    // ---------------------------------------------------------------- 名字与字面量

    /// <summary>类型名 + 该位置的可空标注。</summary>
    private static string TypeName(Type type, MemberInfo member, NullabilityInfoContext nullability)
        => TypeName(type) + NullableSuffix(type, CreateNullability(nullability, member));

    /// <summary>
    /// 反射的可空信息在 net8.0 上只有<b>四个</b>重载：参数 / 属性 / 字段 / 事件
    /// （<c>Create(Type)</c> 与 <c>GetNullabilityInfo</c> 都是更高版本才有的）。
    /// 这里按具体类型分派一次；没覆盖到的成员类型返回 <c>null</c>，
    /// 表示"这个位置没有可空标注"——快照工具不该因为一个边缘成员就让整个守卫炸掉。
    /// </summary>
    private static NullabilityInfo? CreateNullability(NullabilityInfoContext nullability, MemberInfo member)
        => member switch
        {
            FieldInfo field => nullability.Create(field),
            PropertyInfo property => nullability.Create(property),
            EventInfo @event => nullability.Create(@event),
            _ => null,
        };

    /// <summary>类型名 + 该参数位置的可空标注（<c>out</c> / <c>ref</c> 参数要看参数本身）。</summary>
    private static string TypeName(Type type, ParameterInfo parameter, NullabilityInfoContext nullability)
        => TypeName(type) + NullableSuffix(type, nullability.Create(parameter));

    /// <summary>
    /// 把一个类型写成不依赖程序集身份的规范名。<para>
    /// 用 <c>FullName</c> 而不是 <c>CSharpFriendlyName</c>：我们比较的是<b>同一个程序集的两个版本</b>，
    /// 只要同一份源码产出同一个名字就够用，不需要还原成 C# 语法。
    /// 泛型参数位置用 <c>&lt;&gt;</c> 占位，具体实参写进尖括号里（<c>List&lt;string&gt;</c>），
    /// 这样"把 <c>object</c> 改成 <c>string</c>"这种改动会被看见。
    /// </para>
    /// </summary>
    private static string TypeName(Type type)
    {
        if (type.IsByRef) return TypeName(type.GetElementType()!);
        if (type.IsArray) return TypeName(type.GetElementType()!) + "[" + new string(',', type.GetArrayRank() - 1) + "]";
        if (type.IsPointer) return TypeName(type.GetElementType()!) + "*";

        if (type.IsGenericParameter) return type.Name;

        if (type.IsGenericType)
        {
            string definition = type.GetGenericTypeDefinition().FullName
                ?? type.GetGenericTypeDefinition().Name;
            int tick = definition.IndexOf('`');
            if (tick >= 0) definition = definition[..tick];

            return definition + "<"
                + string.Join(", ", type.GetGenericArguments().Select(argument => TypeName(argument)))
                + ">";
        }

        return type.FullName ?? type.Name;
    }

    /// <summary>
    /// 可空标注。<c>!</c> = 不可空、<c>?</c> = 可空、什么都不写 = 该位置没有可空信息。<para>
    /// 这是公开契约的一部分：把返回类型从 <c>string?</c> 收紧成 <c>string</c>，
    /// 调用方的 <c>?? </c> 与 <c>?.</c> 就会开始报"不必要的"警告；反过来放松则是安全的。
    /// </para>
    /// <para>
    /// 只记录<b>最外层</b>的可空性（<c>List&lt;string&gt;?</c> 里的那个 <c>?</c>）。
    /// 元素级标注（<c>List&lt;string?&gt;</c>）不在此列——它不影响调用方能否编译，
    /// 把它也塞进快照只会让快照变脆。
    /// </para>
    /// </summary>
    private static string NullableSuffix(Type type, NullabilityInfo? info)
    {
        if (info is null) return string.Empty;
        if (type.IsValueType || type.IsGenericParameter || type.IsByRef || type.IsPointer) return string.Empty;

        return info.ReadState switch
        {
            NullabilityState.NotNull => "!",
            NullabilityState.Nullable => "?",
            _ => string.Empty,
        };
    }

    /// <summary>把常量/默认值写成可比较的字面量。</summary>
    private static string Literal(object? value)
    {
        return value switch
        {
            null => "null",
            string text => "\"" + text.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal) + "\"",
            char character => "'" + character + "'",
            bool flag => flag ? "true" : "false",
            double number => number.ToString("R", CultureInfo.InvariantCulture),
            float number => number.ToString("R", CultureInfo.InvariantCulture),
            IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
            _ => value.ToString() ?? "null",
        };
    }
}
