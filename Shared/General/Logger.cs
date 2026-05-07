using System.Collections.Concurrent;
using System.Diagnostics;

namespace MeloongCore;

/// <summary>
/// 日志等级。
/// </summary>
public enum LogLevel {
    /// <summary>
    /// 追踪。
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
public enum LogBehavior {
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

public static class Logger {
    internal static BaseLogger Instance { get; set; } = new();

    // 转发给实例的方法包装
    public static void Log(string message, LogLevel level = LogLevel.Info, LogBehavior behavior = LogBehavior.None, [CallerFilePath] string filePath = "") 
        => Instance.Log(message, level, behavior, filePath);
    public static void Trace(string message, LogBehavior behavior = LogBehavior.None, [CallerFilePath] string filePath = "") 
        => Log(message, LogLevel.Trace, behavior, filePath);
    public static void Info(string message, LogBehavior behavior = LogBehavior.None, [CallerFilePath] string filePath = "") 
        => Log(message, LogLevel.Info, behavior, filePath);
    public static void Warning(string message, LogBehavior behavior = LogBehavior.ToastIfDebug, [CallerFilePath] string filePath = "") 
        => Log(message, LogLevel.Warning, behavior, filePath);
    public static void Error(string message, LogBehavior behavior = LogBehavior.AlertThenFeedback, [CallerFilePath] string filePath = "") 
        => Log(message, LogLevel.Error, behavior, filePath);

    public static void Log(Func<string> messageGetter, LogLevel level = LogLevel.Info, LogBehavior behavior = LogBehavior.None, [CallerFilePath] string filePath = "") {
        if (Instance.MinLevel <= level) Instance.Log(messageGetter(), level, behavior, filePath); }
    public static void Trace(Func<string> messageGetter, LogBehavior behavior = LogBehavior.None, [CallerFilePath] string filePath = "") 
        => Log(messageGetter, LogLevel.Trace, behavior, filePath);
    public static void Info(Func<string> messageGetter, LogBehavior behavior = LogBehavior.None, [CallerFilePath] string filePath = "") 
        => Log(messageGetter, LogLevel.Info, behavior, filePath);
    public static void Warning(Func<string> messageGetter, LogBehavior behavior = LogBehavior.ToastIfDebug, [CallerFilePath] string filePath = "") 
        => Log(messageGetter, LogLevel.Warning, behavior, filePath);
    public static void Error(Func<string> messageGetter, LogBehavior behavior = LogBehavior.AlertThenFeedback, [CallerFilePath] string filePath = "") 
        => Log(messageGetter, LogLevel.Error, behavior, filePath);

    public static void Log(Exception ex, string? message = null, LogLevel level = LogLevel.Warning, LogBehavior behavior = LogBehavior.ToastIfDebug, [CallerFilePath] string filePath = "") 
        => Instance.Log(ex, message, level, behavior, filePath);
    public static void Trace(Exception ex, string? message = null, LogBehavior behavior = LogBehavior.None, [CallerFilePath] string filePath = "") 
        => Log(ex, message, LogLevel.Trace, behavior, filePath);
    public static void Info(Exception ex, string? message = null, LogBehavior behavior = LogBehavior.None, [CallerFilePath] string filePath = "") 
        => Log(ex, message, LogLevel.Info, behavior, filePath);
    public static void Warning(Exception ex, string? message = null, LogBehavior behavior = LogBehavior.ToastIfDebug, [CallerFilePath] string filePath = "") 
        => Log(ex, message, LogLevel.Warning, behavior, filePath);
    public static void Error(Exception ex, string? message = null, LogBehavior behavior = LogBehavior.AlertThenFeedback, [CallerFilePath] string filePath = "") 
        => Log(ex, message, LogLevel.Error, behavior, filePath);

    /// <summary>
    /// 在不调用 Logger 的情况下，将信息直接发送到输出窗口。
    /// </summary>
    [Conditional("DEBUG")]
    public static void SendToDebug(string message, LogLevel level = LogLevel.Error, [CallerFilePath] string filePath = "")
        => Debug.WriteLine($"{BaseLogger.GetLogPrefix(level, filePath)}{message}");
    /// <summary>
    /// 在不调用 Logger 的情况下，将信息直接发送到输出窗口。
    /// </summary>
    [Conditional("DEBUG")]
    public static void SendToDebug(Exception ex, string? message = null, LogLevel level = LogLevel.Error, [CallerFilePath] string filePath = "")
        => Debug.WriteLine($"{BaseLogger.GetLogPrefix(level, filePath)}{(message is null ? "" : $"{message}：")}{ex.GetDisplay(true)}");

}

/// <summary>
/// 最基础的日志实现。
/// 仅将日志输出到 <see cref="Debug"/>，不实现 <see cref="LogBehavior"/>。
/// </summary>
public class BaseLogger {

    /// <summary>
    /// 最低日志输出等级，低于此等级的日志将被忽略。
    /// </summary>
    public LogLevel MinLevel { get; set; } = LogLevel.Trace;

    // 核心方法
    public virtual void Log(string message, LogLevel level, LogBehavior behavior, string filePath) {
        if (MinLevel > level) return;
        try {
            var formattedMessage = Format(message, level, filePath, null);
            Output(formattedMessage, level);
            HandleBehavior(message, formattedMessage, behavior, null);
        } catch (Exception logEx) {
            Logger.SendToDebug(logEx, "打印日志失败");
        }
    }
    public virtual void Log(Exception ex, string? message, LogLevel level, LogBehavior behavior, string filePath) {
        if (MinLevel > level) return;
        if (ex is ThreadInterruptedException) {
            Thread.CurrentThread.Interrupt();
            return;
        }
        try {
            var formattedMessage = (message is null ? "" : $"{message}：") + ex.GetDisplay(true);
            formattedMessage = Format(formattedMessage, level, filePath, ex);
            Output(formattedMessage, level);
            HandleBehavior(message, formattedMessage, behavior, ex);
        } catch (Exception logEx) {
            Logger.SendToDebug(logEx, "打印日志失败");
        }
    }

    /// <summary>
    /// 格式化日志文本。
    /// </summary>
    protected virtual string Format(string text, LogLevel level, string filePath, Exception? ex) {
        string prefix = GetLogPrefix(level, filePath);
        return text
            .ReplaceLineEndings("\n", mergeMultiple: true).Split(['\n'], StringSplitOptions.RemoveEmptyEntries)
            .Select(t => prefix + t)
            .Join("\r\n");
    }
    public static string GetLogPrefix(LogLevel level, string filePath) 
        => $"{DateTime.Now:HH':'mm':'ss'.'fff} {level.ToString().First()} {(Thread.CurrentThread.Name is null ? "" : $"<{Thread.CurrentThread.Name}> ")}[{Path.GetFileName(filePath).BeforeFirst(".")}] ";

    /// <summary>
    /// 输出格式化后的日志文本。
    /// </summary>
    protected virtual void Output(string formattedMessage, LogLevel level) {
        Debug.WriteLine(formattedMessage);
    }

    /// <summary>
    /// 在调用 Log 方法的线程执行 <see cref="LogBehavior"/>。
    /// </summary>
    protected virtual void HandleBehavior(string? rawMessage, string formattedMessage, LogBehavior behavior, Exception? ex) {
    }

}

/// <summary>
/// 将日志输出到 <see cref="Debug"/> 并写入指定的文件夹，最多保留 5 个文件。
/// 不实现 <see cref="LogBehavior"/>。
/// </summary>
public class FileLogger : BaseLogger, IDisposable {

    /// <inheritdoc/>
    protected override void Output(string formattedText, LogLevel level) {
        base.Output(formattedText, level);
        if (!writerAvailable && queue.Count >= 100) return; // 在 writer 就绪前，最多缓存 100 条日志
        queue.Enqueue(formattedText);
        queuedEvent.Set();
    }

    private readonly ConcurrentQueue<string> queue = new();
    private readonly AutoResetEvent queuedEvent = new(false);
    private StreamWriter? writer;
    private volatile bool writerAvailable = false;

    /// <summary>
    /// 将日志立即写入文件。
    /// 不会抛出异常。
    /// </summary>
    public void Flush() {
        try {
            if (writer is null || !writerAvailable) return;
            lock (flushLock)
                while (queue.TryDequeue(out string line)) writer.WriteLine(line); // writer 指定了 AutoFlush
        } catch (Exception logEx) {
            Logger.SendToDebug(logEx, "写入日志失败");
        }
    }
    private readonly object flushLock = new();

    /// <summary>
    /// 初始化此 Logger。
    /// 会在新线程中，将之前的日志文件依次后移，最多保留 5 个文件。
    /// </summary>
    public FileLogger(string logFolder, string fileNamePrefix = "Log", string fileNameSuffix = ".txt") {
        var thread = new Thread(() => {
            // 轮转日志文件，将 Log1.txt 留空
            Logger.Info("日志初始化开始");
            try {
                for (int i = 4; i >= 1; i--) {
                    string newerFile = Path.Combine(logFolder, $"{fileNamePrefix}{i + 1}{fileNameSuffix}");
                    string olderFile = Path.Combine(logFolder, $"{fileNamePrefix}{i}{fileNameSuffix}");
                    if (!FileUtils.Exists(olderFile)) continue;
                    FileUtils.Copy(olderFile, newerFile);
                    FileUtils.Delete(olderFile);
                }
            } catch (IOException ex) {
                Logger.Warning(ex, "可能同时开启了多个程序，这或许会导致未知问题", LogBehavior.Toast);
            } catch (Exception ex) {
                Logger.Warning(ex, "整理日志文件失败");
            }
            // 写入新日志文件
            try {
                writer = new StreamWriter(PathUtils.WithLongPath(Path.Combine(logFolder, $"{fileNamePrefix}1{fileNameSuffix}")), append: true) { AutoFlush = true };
                writerAvailable = true;
                Logger.Info("日志初始化成功");
                while (writerAvailable) {
                    queuedEvent.WaitOne();
                    Flush();
                }
            } catch (Exception ex) {
                Logger.Warning(ex, "写入日志文件失败", LogBehavior.Toast);
                writerAvailable = false;
            }
        }) { Name = nameof(FileLogger), Priority = ThreadPriority.Lowest, IsBackground = true };
        thread.Start();
    }

    public void Dispose() {
        writerAvailable = false;
        if (writer is not null) ((IDisposable) writer).Dispose();
        queuedEvent.Dispose();
        GC.SuppressFinalize(this);
    }
}
