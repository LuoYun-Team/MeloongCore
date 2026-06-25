namespace MeloongCore;
public class ProgressProvider {

    // 设置 workerProgress
    private (double actual, double skiped) workerProgress = (0, 0);
    public void Set(double value, bool skiped = false) {
        value = value.Clamp(0, 1);
        double delta = value - workerProgress.actual - workerProgress.skiped;
        if (delta < 0) { // 等比减少
            workerProgress = (workerProgress.actual * value / (workerProgress.actual + workerProgress.skiped), 
                              workerProgress.skiped * value / (workerProgress.actual + workerProgress.skiped));
        } else if (skiped) {
            workerProgress.skiped += delta;
        } else {
            workerProgress.actual += delta;
        }
    }
    public void Add(double value, bool skiped = false) 
        => Set(value + workerProgress.actual + workerProgress.skiped, skiped);

    // 观察
    private (double actual, double skiped) observedWorkerProgress = (0, 0);
    private double incrementProgress = 0;
    public double Observe() {
        if (workerProgress == observedWorkerProgress) return incrementProgress; // 未改变
        if (workerProgress.actual >= observedWorkerProgress.actual) { // 进度增加
            incrementProgress += (1 - incrementProgress) *
                (workerProgress.actual - observedWorkerProgress.actual) / (1 - observedWorkerProgress.actual - workerProgress.skiped);
        }
        observedWorkerProgress = workerProgress;
        return incrementProgress;
    }

}
