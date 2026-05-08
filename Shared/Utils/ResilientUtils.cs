namespace MeloongCore;
public static class ResilientUtils {

    /// <summary>
    /// 在抛出异常时，延迟并自动重试。
    /// </summary>
    public static void Retry(Action action, int maxAttempts = 2, int delayMs = 200, [CallerMemberName] string caller = "")
        => RetryOn<Exception>(action, maxAttempts, delayMs, caller);
    /// <summary>
    /// 在抛出特定异常时，延迟并自动重试。
    /// </summary>
    public static void RetryOn<TException>(Action action, int maxAttempts = 2, int delayMs = 200, [CallerMemberName] string caller = "") where TException : Exception {
        int attempt = 0;
        while (true) {
            try {
                action();
                return; // 成功则退出
            } catch (TException ex) {
                attempt++;
                if (attempt >= maxAttempts) throw; // 超过最大尝试次数
                Logger.Warn(ex, $"{caller} 第 {attempt} 次尝试失败，将在 {delayMs}ms 后重试");
                Thread.Sleep(delayMs);
            }
        }
    }

    /// <summary>
    /// 在抛出异常时，延迟并自动重试。
    /// </summary>
    public static void Retry<TOut>(Func<TOut> func, int maxAttempts = 2, int delayMs = 200, [CallerMemberName] string caller = "")
        => RetryOn<Exception, TOut>(func, maxAttempts, delayMs, caller);
    /// <summary>
    /// 在抛出特定异常时，延迟并自动重试。
    /// </summary>
    public static TOut RetryOn<TException, TOut>(Func<TOut> func, int maxAttempts = 2, int delayMs = 200, [CallerMemberName] string caller = "") where TException : Exception {
        int attempt = 0;
        while (true) {
            try {
                return func(); // 成功则退出
            } catch (TException ex) {
                attempt++;
                if (attempt >= maxAttempts) throw; // 超过最大尝试次数
                Logger.Warn(ex, $"{caller} 第 {attempt} 次尝试失败，将在 {delayMs}ms 后重试");
                Thread.Sleep(delayMs);
            }
        }
    }

}
