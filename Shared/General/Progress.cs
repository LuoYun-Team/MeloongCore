namespace MeloongCore;
public class ProgressProvider {

    // 主项进度
    private (double actual, double skiped, double splited) progressParts = (0, 0, 0);
    private double progressSum => progressParts.actual + progressParts.skiped + progressParts.splited;
    /// <summary>
    /// 将当前进度设置为指定值。
    /// 若 <paramref name="skiped"/> 为 true，这段进度的增量值将被计为跳过：当前观测进度不会增加，后续观测进度将增加地更快。
    /// </summary>
    public void Set(double value, bool skiped = false) {
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
    }
    /// <summary>
    /// 将当前进度增加指定值。
    /// 若 <paramref name="skiped"/> 为 true，这段进度的增量值将被计为跳过：当前观测进度不会增加，后续观测进度将增加地更快。
    /// </summary>
    public void Add(double value, bool skiped = false) 
        => Set(value + progressSum, skiped);

    // 子项进度
    private readonly List<(double weight, ProgressProvider sub)> childrens = [];
    /// <summary>
    /// 拆分一个子项：当该子项完成时，当前进度将增至 <paramref name="value"/>。
    /// </summary>
    public ProgressProvider SplitTo(double value) 
        => SplitBy(value - progressSum).First();
    /// <summary>
    /// 拆分多个子项，每个子项都占据对应指定的进度量。
    /// <para/>例如，调用 <c>SplitBy(0.2, 0.3)</c> 将返回两个子项，分别占据总进度的 20%、30%。
    /// </summary>
    public List<ProgressProvider> SplitBy(params double[] percentages) {
        var totalWeight = percentages.Sum().Clamp(0, 1);
        var totalPercentage = (totalWeight + progressSum).Clamp(0, 1) - progressSum;
        progressParts.splited += totalPercentage;
        return percentages.Select(percentage => {
            if (percentage <= 0) throw new ArgumentException("子项进度必须为正数");
            var weight = percentage / totalWeight * totalPercentage;
            var sub = new ProgressProvider();
            childrens.Add((weight, sub));
            return sub;
        }).ToList();
    }
    private (double actual, double skiped) GetTotalProgress() {
        (double actual, double skiped) current = (progressParts.actual, progressParts.skiped);
        if (childrens.Any()) { // 将子项的进度加入主项
            var perWeight = progressParts.splited / childrens.Sum(c => c.weight);
            childrens.ForEach(c => {
                var (subActual, subSkiped) = c.sub.GetTotalProgress();
                current.actual += subActual * perWeight * c.weight;
                current.skiped += subSkiped * perWeight * c.weight;
            });
        }
        return current;
    }

    // 观测
    private (double actual, double skiped) observedProgress = (0, 0);
    private double incrementProgress = 0;
    /// <summary>
    /// 获取一个在多次观测之间必定单调递增的进度值，范围为 [0, 1]。
    /// </summary>
    public double Observe() {
        (double actual, double skiped) current = GetTotalProgress();
        if (current == observedProgress) return incrementProgress; // 未改变
        if (current.actual >= observedProgress.actual) { // 进度增加
            incrementProgress += (1 - incrementProgress) *
                (current.actual - observedProgress.actual) / (1 - observedProgress.actual - current.skiped);
        }
        observedProgress = current;
        return incrementProgress;
    }

}
