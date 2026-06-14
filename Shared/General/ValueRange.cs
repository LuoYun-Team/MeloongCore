namespace MeloongCore;

/// <summary>
/// 表示一个由可选下界和可选上界组成的值范围。
/// </summary>
public sealed class ValueRange<T>(
    T? lower, T? upper,
    bool isLowerInclusive, bool isUpperInclusive,
    bool hasLower, bool hasUpper
) where T : notnull, IComparable<T> {

    public T? Lower { get; private set; } = hasLower && lower is null ? throw new ArgumentNullException(nameof(lower)) : lower;
    public T? Upper { get; private set; } = hasUpper && upper is null ? throw new ArgumentNullException(nameof(upper)) : upper;

    public bool HasLower { get; private set; } = hasLower;
    public bool HasUpper { get; private set; } = hasUpper;

    public void ClearLower() { Lower = default; HasLower = false; }
    public void ClearUpper() { Upper = default; HasUpper = false; }

    public void SetLower(T lower, bool isInclusive) {
        Lower = lower;
        HasLower = true;
        IsLowerInclusive = isInclusive;
    }
    public void SetUpper(T upper, bool isInclusive) {
        Upper = upper;
        HasUpper = true;
        IsUpperInclusive = isInclusive;
    }

    /// <summary> 下界值是否为闭区间。 </summary>
    public bool IsLowerInclusive = isLowerInclusive;
    /// <summary> 上界值是否为闭区间。 </summary>
    public bool IsUpperInclusive = isUpperInclusive;

    #region 工厂方法

    /// <summary> 创建等同于 <c>[lower, upper]</c> 的闭区间。 </summary>
    public static ValueRange<T> Closed(T lower, T upper) => new(lower, upper, true, true, true, true);
    /// <summary> 创建形如 <c>[lower, upper)</c> 的左闭右开区间。 </summary>
    public static ValueRange<T> ClosedOpen(T lower, T upper) => new(lower, upper, true, false, true, true);
    /// <summary> 创建形如 <c>(lower, upper]</c> 的左开右闭区间。 </summary>
    public static ValueRange<T> OpenClosed(T lower, T upper) => new(lower, upper, false, true, true, true);
    /// <summary> 创建形如 <c>(lower, upper)</c> 的开区间。 </summary>
    public static ValueRange<T> Open(T lower, T upper) => new(lower, upper, false, false, true, true);
    /// <summary> 创建形如 <c>[value, value]</c> 的单值闭区间。 </summary>
    public static ValueRange<T> Exactly(T value) => new(value, value, true, true, true, true);

    /// <summary> 创建形如 <c>[lower, +∞)</c> 的范围。 </summary>
    public static ValueRange<T> AtLeast(T lower) => new(lower, default, true, true, true, false);
    /// <summary> 创建形如 <c>(lower, +∞)</c> 的范围。 </summary>
    public static ValueRange<T> GreaterThan(T lower) => new(lower, default, false, true, true, false);
    /// <summary> 创建形如 <c>(-∞, upper]</c> 的范围。 </summary>
    public static ValueRange<T> AtMost(T upper) => new(default, upper, true, true, false, true);
    /// <summary> 创建形如 <c>(-∞, upper)</c> 的范围。 </summary>
    public static ValueRange<T> LessThan(T upper) => new(default, upper, true, false, false, true);
    /// <summary> 创建形如 <c>(-∞, +∞)</c> 的无限范围。 </summary>
    public static ValueRange<T> All() => new(default, default, true, true, false, false);

    #endregion

    /// <summary>
    /// 判断指定值是否位于当前范围内。
    /// </summary>
    public bool Contains(T value) {
        if (HasLower) {
            var compareLower = value.CompareTo(Lower!);
            if (compareLower < 0) return false;
            if (compareLower == 0 && !IsLowerInclusive) return false;
        }
        if (HasUpper) {
            var compareUpper = value.CompareTo(Upper!);
            if (compareUpper > 0) return false;
            if (compareUpper == 0 && !IsUpperInclusive) return false;
        }
        return true;
    }

    public override string ToString()
        => $"{(HasLower && IsLowerInclusive ? "[" : "(")}{(HasLower ? Lower!.ToString() : "-∞")}, {(HasUpper ? Upper!.ToString() : "+∞")}{(HasUpper && IsUpperInclusive ? "]" : ")")}";
}