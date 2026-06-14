namespace MeloongCore;

/// <summary>
/// 表示一个由可选下界和可选上界组成的范围。
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

    /// <summary>
    /// 取当前范围与另一个范围的交集。
    /// </summary>
    /// <returns>
    /// 若两个范围存在交集，则返回交集范围；否则返回 <see langword="null" />。
    /// </returns>
    public ValueRange<T>? Intersect(ValueRange<T> other) {
        T? lower = default;
        T? upper = default;
        var isLowerInclusive = true;
        var isUpperInclusive = true;

        if (HasLower && other.HasLower) {
            switch (Lower!.CompareTo(other.Lower!)) {
                case > 0:
                    lower = Lower;
                    isLowerInclusive = IsLowerInclusive;
                    break;
                case < 0:
                    lower = other.Lower;
                    isLowerInclusive = other.IsLowerInclusive;
                    break;
                default:
                    lower = Lower;
                    isLowerInclusive = IsLowerInclusive && other.IsLowerInclusive;
                    break;
            }
        } else if (HasLower) {
            lower = Lower;
            isLowerInclusive = IsLowerInclusive;
        } else if (other.HasLower) {
            lower = other.Lower;
            isLowerInclusive = other.IsLowerInclusive;
        }

        if (HasUpper && other.HasUpper) {
            switch (Upper!.CompareTo(other.Upper!)) {
                case < 0:
                    upper = Upper;
                    isUpperInclusive = IsUpperInclusive;
                    break;
                case > 0:
                    upper = other.Upper;
                    isUpperInclusive = other.IsUpperInclusive;
                    break;
                default:
                    upper = Upper;
                    isUpperInclusive = IsUpperInclusive && other.IsUpperInclusive;
                    break;
            }
        } else if (HasUpper) {
            upper = Upper;
            isUpperInclusive = IsUpperInclusive;
        } else if (other.HasUpper) {
            upper = other.Upper;
            isUpperInclusive = other.IsUpperInclusive;
        }

        var anyHasLower = HasLower || other.HasLower;
        var anyHasUpper = HasUpper || other.HasUpper;
        if (anyHasLower && anyHasUpper) {
            var compare = lower!.CompareTo(upper!);
            if (compare > 0) return null;
            if (compare == 0 && (!isLowerInclusive || !isUpperInclusive)) return null;
        }

        return new(lower, upper, isLowerInclusive, isUpperInclusive, anyHasLower, anyHasUpper);
    }

    public override string ToString()
        => $"{(HasLower && IsLowerInclusive ? "[" : "(")}{(HasLower ? Lower!.ToString() : "-∞")}, {(HasUpper ? Upper!.ToString() : "+∞")}{(HasUpper && IsUpperInclusive ? "]" : ")")}";
}