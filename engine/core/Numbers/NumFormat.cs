namespace NekoClicker.Core.Numbers;

/// <summary>数字的显示风格。</summary>
public enum NumberStyle
{
    /// <summary>长刻度名：<c>1.234 million</c>。Cookie Clicker 默认风格；<b>仓库里没有宿主再用它</b>。</summary>
    Long,

    /// <summary>短刻度名：<c>1.234M</c>。1000 起缩写（<c>1K</c>）；<b>两个宿主用的都是它</b>。</summary>
    Short,

    /// <summary>科学计数法：<c>1.234e6</c>。</summary>
    Scientific,

    /// <summary>纯数字 + 千分位：<c>1,234,567</c>。超过 1e21 自动退回科学计数法。</summary>
    Plain,
}

/// <summary>
/// 数值格式化。<para>
/// 增量游戏的数字会跨越十几个数量级，直接打印既有 30 位整数没人读得懂，也会撑爆 UI。
/// 这里实现 Cookie Clicker 的 <c>Beautify</c> 策略：按 3 位一档套用刻度名，
/// 名称用尽后退回科学计数法。<b>两套表的档位基准不一样，这是故意的</b>——
/// 短刻度（<see cref="ShortScaleNames"/>）从 <b>1e3</b> 起（<c>K</c> 那一档），
/// 长名表（<see cref="LongScaleNames"/>）从 <b>1e6</b> 起（英文里 "1.234 thousand"
/// 不是惯用写法，1000 ~ 999999 写成完整数字更好读）。于是：小于 1000 时四种风格一致，
/// 1000 ~ 999999 只有 <see cref="NumberStyle.Short"/> 会缩写（<c>7.579K</c>）。
/// </para>
/// <para>
/// <b>K/M/B/T 只有 <see cref="ShortScaleNames"/> 这一套，两个宿主都走
/// <see cref="NumberStyle.Short"/>。</b>同一个数在终端与浏览器上必须是同一个字符串；
/// 长名风格（<c>1.234 million</c>）留在公开 API 里供宿主自选，但仓库里没有任何一处宿主再用它。
/// </para>
/// </summary>
public static class NumFormat
{
    /// <summary>短刻度名称表，索引 0 对应 1e3（所以 1000 是 <c>1K</c>）。</summary>
    public static readonly string[] ShortScaleNames =
    [
        "K", "M", "B", "T", "Qa", "Qi", "Sx", "Sp", "Oc", "No", "Dc",
        "Ud", "Dd", "Td", "Qad", "Qid", "Sxd", "Spd", "Od", "Nd", "Vg",
    ];

    /// <summary>
    /// 长刻度名称表，索引 0 对应 1e6（<b>没有千位那一档</b>，理由见类型注释）。
    /// 与短刻度表逐档对齐、只差一个基准：<c>LongScaleNames[i]</c> 与
    /// <c>ShortScaleNames[i + 1]</c> 指的是同一个档位。
    /// </summary>
    public static readonly string[] LongScaleNames =
    [
        "million", "billion", "trillion", "quadrillion", "quintillion",
        "sextillion", "septillion", "octillion", "nonillion", "decillion",
        "undecillion", "duodecillion", "tredecillion", "quattuordecillion", "quindecillion",
        "sexdecillion", "septendecillion", "octodecillion", "novemdecillion", "vigintillion",
    ];

    /// <summary>按指定风格格式化数字。</summary>
    public static string Format(double value, NumberStyle style = NumberStyle.Long)
    {
        if (double.IsNaN(value)) return "NaN";
        if (double.IsPositiveInfinity(value)) return "∞";
        if (double.IsNegativeInfinity(value)) return "-∞";

        string sign = value < 0 ? "-" : string.Empty;
        double abs = Math.Abs(value);

        if (abs == 0) return "0";

        // 极小值：完整小数会退化成 "0"，直接走科学计数法。
        if (abs < 1e-5) return sign + Scientific(abs);

        if (style == NumberStyle.Scientific) return sign + Scientific(abs);

        // 还没到这一档的缩写门槛时四种风格一致（整数部分加千分位、小数最多 3 位）。
        // Short 的门槛是 1000（K 档），其余三种是 100 万。
        if (abs < ScaleFloor(style)) return sign + FormatBelowMillion(abs);

        // Plain 想要"完整数字"，但超过 1e21 之后连屏幕都放不下，退化为科学计数法。
        if (style == NumberStyle.Plain)
        {
            return abs < 1e21
                ? sign + Math.Round(abs).ToString("#,0", System.Globalization.CultureInfo.InvariantCulture)
                : sign + Scientific(abs);
        }

        bool shortStyle = style == NumberStyle.Short;
        string[] names = shortStyle ? ShortScaleNames : LongScaleNames;
        return sign + FormatScaled(abs, names, space: !shortStyle, baseTier: BaseTier(shortStyle));
    }

    /// <summary>
    /// 某一档风格开始缩写的门槛：短刻度从 1000 起（<c>K</c>），其余从 100 万起。
    /// </summary>
    private static double ScaleFloor(NumberStyle style) => style == NumberStyle.Short ? 1e3 : 1e6;

    /// <summary>某一档刻度表的档位基准：索引 0 对应 1e3 时返回 1，对应 1e6 时返回 2。</summary>
    private static int BaseTier(bool shortStyle) => shortStyle ? 1 : 2;

    /// <summary>
    /// 用自定义刻度名称表格式化。<b>基准与 <see cref="ShortScaleNames"/> 相同：索引 0 对应 1e3。</b>
    /// （本地化时换一张表就能换一套刻度名，但"从千位起缩写"这条口径与内置短刻度一致。）
    /// </summary>
    /// <param name="value">待格式化数值。</param>
    /// <param name="scaleNames">刻度名称，索引 0 必须对应 1e3。</param>
    /// <param name="space">数字与名称之间是否加空格。</param>
    public static string Format(double value, IReadOnlyList<string> scaleNames, bool space = true)
    {
        if (scaleNames is null || scaleNames.Count == 0) return Format(value, NumberStyle.Scientific);

        string sign = value < 0 ? "-" : string.Empty;
        double abs = Math.Abs(value);
        if (abs < 1e3) return Format(value, NumberStyle.Plain);
        return sign + FormatScaled(abs, scaleNames, space, baseTier: 1);
    }

    /// <summary>长名称风格（<c>1.234 million</c>）。<b>没有宿主再用它</b>，见类型注释。</summary>
    public static string FormatLong(double value) => Format(value, NumberStyle.Long);

    /// <summary>短名称风格（<c>1.234M</c>）。两个宿主显示大数时用的都是它。</summary>
    public static string FormatShort(double value) => Format(value, NumberStyle.Short);

    /// <summary>千分位整数风格（<c>1,234,567</c>）。</summary>
    public static string FormatPlain(double value) => Format(value, NumberStyle.Plain);

    /// <summary>科学计数法风格（<c>1.234e6</c>）。</summary>
    public static string FormatScientific(double value) => Format(value, NumberStyle.Scientific);

    /// <summary>把小数比率格式化为百分比，例如 0.125 → <c>12.5%</c>。</summary>
    public static string Percent(double fraction, int decimals = 1)
    {
        if (double.IsNaN(fraction)) return "NaN";
        if (double.IsInfinity(fraction)) return fraction > 0 ? "∞%" : "-∞%";
        string fmt = decimals <= 0 ? "0" : "0." + new string('#', decimals);
        return (fraction * 100.0).ToString(fmt, System.Globalization.CultureInfo.InvariantCulture) + "%";
    }

    /// <summary>倍数显示，例如 <c>×7</c>。</summary>
    public static string Multiplier(double value) => "×" + Format(value, NumberStyle.Plain);

    /// <summary>带符号显示，例如 <c>+5</c> / <c>-3</c>。</summary>
    public static string Signed(double value)
    {
        string body = Format(Math.Abs(value), NumberStyle.Plain);
        return value < 0 ? "-" + body : "+" + body;
    }

    /// <summary>把秒数格式化为紧凑的时长文本：<c>3d 4h</c> / <c>2h 05m</c> / <c>12m 30s</c> / <c>4.2s</c>。</summary>
    public static string Duration(double seconds)
    {
        if (double.IsNaN(seconds)) return "NaN";
        if (double.IsInfinity(seconds)) return "∞";
        if (seconds < 0) seconds = 0;

        if (seconds < 60) return FormatSeconds(seconds);
        if (seconds < 3600)
        {
            int m = (int)(seconds / 60);
            int s = (int)Math.Round(seconds - (m * 60));
            // 浮点累加会让 1499.9999999 秒算出 59.9999 秒，四舍五入成 60s；
            // 不做进位就会出现 "24m 60s" 这种读起来像 bug 的时长。
            if (s >= 60)
            {
                s -= 60;
                m += 1;
            }
            return $"{m}m {s:00}s";
        }
        if (seconds < 86400)
        {
            int h = (int)(seconds / 3600);
            int m = (int)Math.Round((seconds - (h * 3600)) / 60);
            // 同样的进位问题：7199.99 秒会算出 59.9999 分钟。
            if (m >= 60)
            {
                m -= 60;
                h += 1;
            }
            return $"{h}h {m:00}m";
        }
        if (seconds < 86400 * 365.25 * 100)
        {
            int d = (int)(seconds / 86400);
            int h = (int)Math.Round((seconds - (d * 86400)) / 3600);
            if (h >= 24)
            {
                h -= 24;
                d += 1;
            }
            return $"{d}d {h}h";
        }
        return Format(seconds / (86400 * 365.25), NumberStyle.Plain) + "y";
    }

    /// <summary>把 <see cref="TimeSpan"/> 格式化为紧凑时长文本。</summary>
    public static string Duration(TimeSpan span) => Duration(span.TotalSeconds);

    private static string FormatSeconds(double seconds)
    {
        if (seconds >= 10) return ((int)Math.Round(seconds)).ToString(System.Globalization.CultureInfo.InvariantCulture) + "s";
        return seconds.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture) + "s";
    }

    private static string FormatBelowMillion(double abs)
    {
        if (abs >= 1000)
            return Math.Round(abs).ToString("#,0", System.Globalization.CultureInfo.InvariantCulture);
        if (abs >= 1)
            return Trim(abs, 3);

        // 小于 1 的值用 5 位小数（调用方已保证 abs ≥ 1e-5），否则 0.0001 会被抹成 "0"。
        return abs.ToString("0.#####", System.Globalization.CultureInfo.InvariantCulture);
    }

    private static string FormatScaled(double abs, IReadOnlyList<string> names, bool space, int baseTier)
    {
        // log10/3 得到 3 位一档的档位；1e3 → 1，1e6 → 2，1e9 → 3 ...
        int tier = (int)Math.Floor(Math.Log10(abs) / 3.0);

        // Math.Log10 在 10 的整数次幂附近可能有 1ulp 级舍入（例如 1e21 → 20.999…），
        // 会把档位算低一档并让尾数变成 1000。这里用实际的幂值双向校正，比加 epsilon 可靠。
        while (abs >= Pow10(3 * (tier + 1))) tier++;
        while (tier >= baseTier && abs < Pow10(3 * tier)) tier--;

        int index = tier - baseTier;
        if (index < 0 || index >= names.Count) return Scientific(abs);

        double mantissa = abs / Pow10(3 * tier);

        // 尾数四舍五入后可能进位到 1000，需要再进一档。
        if (mantissa >= 999.9995)
        {
            mantissa /= 1000.0;
            index++;
            if (index >= names.Count) return Scientific(abs);
        }

        string m = Trim(mantissa, 3);
        return space ? m + " " + names[index] : m + names[index];
    }

    private static double Pow10(int exponent) => Math.Pow(10, exponent);

    private static string Trim(double value, int decimals)
    {
        string fmt = decimals <= 0 ? "0" : "0." + new string('#', decimals);
        return value.ToString(fmt, System.Globalization.CultureInfo.InvariantCulture);
    }

    private static string Scientific(double abs)
    {
        // 保留 3 位小数尾数，例如 1.234e21。
        int exponent = (int)Math.Floor(Math.Log10(abs));
        double mantissa = abs / Math.Pow(10, exponent);
        if (mantissa >= 9.9995)
        {
            mantissa /= 10.0;
            exponent++;
        }
        return Trim(mantissa, 3) + "e" + exponent.ToString(System.Globalization.CultureInfo.InvariantCulture);
    }
}
