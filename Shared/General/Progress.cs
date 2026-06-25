namespace MeloongCore;

/// <summary>
/// 提供一个追踪任务进度的对象，它所提供的供用户查看的进度必定单调递增。
/// 支持重试、拆分子项进度、记录部分进度被跳过。
/// </summary>
public class ProgressProvider {

    /// <summary>
    /// 当进度被改变时触发。
    /// </summary>
    public event Action? ProgressChanged;

    // ===================================== 主项进度 =====================================

    private (double actual, double skiped, double splited) progressParts = (0, 0, 0);
    private double progressSum => progressParts.actual + progressParts.skiped + progressParts.splited;
    private bool _Set(double value, bool skiped) {
        value = value.Clamp(0, 1);
        double delta = value - progressSum;
        if (delta < 0) { // 等比减少
            progressParts = (progressParts.actual * value / progressSum,
                              progressParts.skiped * value / progressSum,
                              progressParts.splited * value / progressSum);
        } else if (skiped) {
            progressParts.skiped += delta;
        } else {
            progressParts.actual += delta;
        }
        return delta != 0;
    }

    /// <summary>
    /// 将当前进度设置为指定值。
    /// 若 <paramref name="skiped"/> 为 true，这段进度的增量值将被计为跳过：当前观测进度不会增加，后续观测进度将增加地更快。
    /// </summary>
    public void Set(double value, bool skiped = false) {
        bool changed;
        lock (this) changed = _Set(value, skiped);
        if (changed) ProgressChanged?.Invoke();
    }
    /// <summary>
    /// 将当前进度增加指定值。
    /// 若 <paramref name="skiped"/> 为 true，这段进度的增量值将被计为跳过：当前观测进度不会增加，后续观测进度将增加地更快。
    /// </summary>
    public void Add(double value, bool skiped = false) {
        bool changed;
        lock (this) changed = _Set(value + progressSum, skiped);
        if (changed) ProgressChanged?.Invoke();
    }

    // ===================================== 子项进度 =====================================

    private readonly List<(double percentage, ProgressProvider sub)> childrens = [];

    /// <summary>
    /// 拆分一个子项：当该子项完成时，当前进度将增至 <paramref name="value"/>。
    /// </summary>
    public ProgressProvider SplitTo(double value) {
        lock (this) return SplitBy(value - progressSum).First();
    }
    /// <summary>
    /// 拆分多个子项，每个子项都占据对应指定的进度量。
    /// <para/>例如，调用 <c>SplitBy(0.2, 0.3)</c> 将返回两个子项，分别占据总进度的 20%、30%。
    /// <para/>若总进度量 > 1，子项的进度量将相对缩小。
    /// </summary>
    public List<ProgressProvider> SplitBy(params double[] percentages) {
        lock (this) {
            if (percentages.Any(p => p <= 0)) throw new ArgumentException("子项进度必须为正数");
            progressParts.splited += (percentages.Sum() + progressSum).Clamp(0, 1) - progressSum;
            return percentages.Select(percentage => {
                var sub = new ProgressProvider();
                sub.ProgressChanged += () => ProgressChanged?.Invoke();
                childrens.Add((percentage, sub));
                return sub;
            }).ToList();
        }
    }
    /// <summary>
    /// 获取包含子项进度的实际总进度值。
    /// </summary>
    private (double actual, double skiped) GetTotalProgress() {
        lock (this) {
            (double actual, double skiped) current = (progressParts.actual, progressParts.skiped);
            if (childrens.Any(c => c.percentage > 0)) { // 将子项的进度加入主项
                var mult = progressParts.splited / childrens.Sum(c => c.percentage);
                childrens.ForEach(c => {
                    var (subActual, subSkiped) = c.sub.GetTotalProgress();
                    current.actual += subActual * mult * c.percentage;
                    current.skiped += subSkiped * mult * c.percentage;
                });
            }
            return current;
        }
    }

    // ===================================== 观测 =====================================

    private (double actual, double skiped) observedProgress = (0, 0);
    private double incrementProgress = 0;
    /// <summary>
    /// 获取一个在多次观测之间必定单调递增的进度值，范围为 [0, 1]。
    /// </summary>
    public double Observe() {
        lock (this) {
            (double actual, double skiped) current = GetTotalProgress();
            if (current == observedProgress) return incrementProgress; // 未改变
            if (current.actual + current.skiped > 0.9999999) { // 已完成
                incrementProgress = 1;
            } else if (current.actual >= observedProgress.actual) { // 进度增加
                incrementProgress += (1 - incrementProgress) *
                    (current.actual - observedProgress.actual) / (1 - observedProgress.actual - current.skiped);
            }
            observedProgress = current;
            return incrementProgress;
        }
    }

}
