namespace MeloongCore;

/// <summary>
/// 日志等级。
/// </summary>
public enum LogLevels {
    /// <summary>
    /// 追踪。
    /// 只在调试状态下输出日志信息。
    /// </summary>
    Trace,
    /// <summary>
    /// 信息。
    /// </summary>
    Info,
    /// <summary>
    /// 警告。
    /// </summary>
    Warning,
    /// <summary>
    /// 错误。
    /// </summary>
    Error
}

/// <summary>
/// 输出日志时执行的错误汇报行为。
/// </summary>
public enum LogBehaviors {
    /// <summary>
    /// 无提示。
    /// </summary>
    None,
    /// <summary>
    /// 只在调试状态下给出错误提示信息。
    /// </summary>
    ToastIfDebug,
    /// <summary>
    /// 给出错误提示信息。
    /// </summary>
    Toast,
    /// <summary>
    /// 弹出错误弹窗，不要求反馈。
    /// </summary>
    Alert,
    /// <summary>
    /// 弹出错误弹窗，要求提交反馈。
    /// </summary>
    AlertThenFeedback,
    /// <summary>
    /// 弹出系统原生样式的错误弹窗，要求提交反馈，然后使程序崩溃。
    /// </summary>
    AlertThenCrash
}

public interface ILogger {
    void Log(string message, LogLevels level, LogBehaviors behavior, string filePath);
    void Log(Exception ex, string? message, LogLevels level, LogBehaviors behavior, string filePath);
}

public static class Logger {
    public static ILogger Instance { get; set; } = new ConsoleLogger();

    // 转发给实例的方法调用
    // TODO: Trace
    public static void Log(string message, LogLevels level = LogLevels.Info, LogBehaviors behavior = LogBehaviors.None, [CallerFilePath] string filePath = "")
        => Instance.Log(message, level, behavior, filePath);
    public static void Log(Exception ex, string? message = null, LogLevels level = LogLevels.Warning, LogBehaviors behavior = LogBehaviors.ToastIfDebug, [CallerFilePath] string filePath = "")
        => Instance.Log(ex, message, level, behavior, filePath);
    public static void Trace(string message, LogBehaviors behavior = LogBehaviors.None, [CallerFilePath] string filePath = "") => Log(message, LogLevels.Trace, behavior, filePath);
    public static void Info(string message, LogBehaviors behavior = LogBehaviors.None, [CallerFilePath] string filePath = "") => Log(message, LogLevels.Info, behavior, filePath);
    public static void Warning(string message, LogBehaviors behavior = LogBehaviors.None, [CallerFilePath] string filePath = "") => Log(message, LogLevels.Warning, behavior, filePath);
    public static void Error(string message, LogBehaviors behavior = LogBehaviors.None, [CallerFilePath] string filePath = "") => Log(message, LogLevels.Error, behavior, filePath);
    public static void Trace(Exception ex, string? message = null, LogBehaviors behavior = LogBehaviors.None, [CallerFilePath] string filePath = "") => Log(ex, message, LogLevels.Trace, behavior, filePath);
    public static void Info(Exception ex, string? message = null, LogBehaviors behavior = LogBehaviors.None, [CallerFilePath] string filePath = "") => Log(ex, message, LogLevels.Info, behavior, filePath);
    public static void Warning(Exception ex, string? message = null, LogBehaviors behavior = LogBehaviors.ToastIfDebug, [CallerFilePath] string filePath = "") => Log(ex, message, LogLevels.Warning, behavior, filePath);
    public static void Error(Exception ex, string? message = null, LogBehaviors behavior = LogBehaviors.AlertThenFeedback, [CallerFilePath] string filePath = "") => Log(ex, message, LogLevels.Error, behavior, filePath);
}

public class ConsoleLogger : ILogger {
    public void Log(string message, LogLevels level, LogBehaviors behavior, string filePath) {
    }
    public void Log(Exception ex, string? message, LogLevels level, LogBehaviors behavior, string filePath) {
    }
}
