using System.Collections.Concurrent;
using System.Diagnostics;

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

public static class Logger {
    public static BaseLogger Instance { get; set; } = new();

    // 转发给实例的方法调用

    public static void Log(string message, LogLevels level = LogLevels.Info, LogBehaviors behavior = LogBehaviors.None, [CallerFilePath] string filePath = "")
        => Instance.Log(message, level, behavior, filePath);
    public static void Log(Exception ex, string? message = null, LogLevels level = LogLevels.Warning, LogBehaviors behavior = LogBehaviors.ToastIfDebug, [CallerFilePath] string filePath = "")
        => Instance.Log(ex, message, level, behavior, filePath);

    public static void Trace(Func<string> message, LogBehaviors behavior = LogBehaviors.None, [CallerFilePath] string filePath = "") { if (Instance.MinLevel <= LogLevels.Trace) Log(message(), LogLevels.Trace, behavior, filePath); }
    public static void Trace(string message, LogBehaviors behavior = LogBehaviors.None, [CallerFilePath] string filePath = "") => Log(message, LogLevels.Trace, behavior, filePath);
    public static void Info(string message, LogBehaviors behavior = LogBehaviors.None, [CallerFilePath] string filePath = "") => Log(message, LogLevels.Info, behavior, filePath);
    public static void Warning(string message, LogBehaviors behavior = LogBehaviors.ToastIfDebug, [CallerFilePath] string filePath = "") => Log(message, LogLevels.Warning, behavior, filePath);
    public static void Error(string message, LogBehaviors behavior = LogBehaviors.AlertThenFeedback, [CallerFilePath] string filePath = "") => Log(message, LogLevels.Error, behavior, filePath);
    
    public static void Trace(Exception ex, string? message = null, LogBehaviors behavior = LogBehaviors.None, [CallerFilePath] string filePath = "") => Log(ex, message, LogLevels.Trace, behavior, filePath);
    public static void Info(Exception ex, string? message = null, LogBehaviors behavior = LogBehaviors.None, [CallerFilePath] string filePath = "") => Log(ex, message, LogLevels.Info, behavior, filePath);
    public static void Warning(Exception ex, string? message = null, LogBehaviors behavior = LogBehaviors.ToastIfDebug, [CallerFilePath] string filePath = "") => Log(ex, message, LogLevels.Warning, behavior, filePath);
    public static void Error(Exception ex, string? message = null, LogBehaviors behavior = LogBehaviors.AlertThenFeedback, [CallerFilePath] string filePath = "") => Log(ex, message, LogLevels.Error, behavior, filePath);
}

/// <summary>
/// 最基础的日志实现。
/// 仅将日志输出到 <see cref="Debug"/>，不实现 <see cref="LogBehaviors"/>。
/// </summary>
public class BaseLogger {

    /// <summary>
    /// 最低日志输出等级，低于此等级的日志将被忽略。
    /// </summary>
    public LogLevels MinLevel { get; set; } = LogLevels.Trace;

    // 核心方法
    public virtual void Log(string message, LogLevels level, LogBehaviors behavior, string filePath) {
        if (MinLevel > level) return;
        try {
            var formattedMessage = Format(message, level, filePath, null);
            Output(formattedMessage, level);
            HandleBehavior(message, formattedMessage, behavior, null);
        } catch {}
    }
    public virtual void Log(Exception ex, string? message, LogLevels level, LogBehaviors behavior, string filePath) {
        if (MinLevel > level) return;
        if (ex is ThreadInterruptedException) return;
        try {
            var formattedMessage = (message is null ? "" : $"{message}：") + ex.GetDisplay(true);
            formattedMessage = Format(formattedMessage, level, filePath, ex);
            Output(formattedMessage, level);
            HandleBehavior(message, formattedMessage, behavior, ex);
        } catch { }
    }

    /// <summary>
    /// 格式化日志文本。
    /// </summary>
    protected virtual string Format(string text, LogLevels level, string filePath, Exception? ex) {
        string prefix = $"{DateTime.Now:HH':'mm':'ss'.'fff} {level.ToString().First()} {(Thread.CurrentThread.Name is null ? "" : $"<{Thread.CurrentThread.Name}> ")}[{filePath.AfterLast(@"\").BeforeFirst(".")}] ";
        return text
            .ReplaceLineEndings("\n", mergeMultiple: true).Split(['\n'], StringSplitOptions.RemoveEmptyEntries)
            .Select(t => prefix + t)
            .Join("\r\n");
    }

    /// <summary>
    /// 输出格式化后的日志文本。
    /// </summary>
    protected virtual void Output(string formattedMessage, LogLevels level) {
        Debug.WriteLine(formattedMessage);
    }

    /// <summary>
    /// 在调用 Log 方法的线程执行 <see cref="LogBehaviors"/>。
    /// </summary>
    protected virtual void HandleBehavior(string? rawMessage, string formattedMessage, LogBehaviors behavior, Exception? ex) {
    }

}

/// <summary>
/// 将日志输出到 <see cref="Debug"/> 并写入指定的文件夹，最多保留 5 个文件。
/// 不实现 <see cref="LogBehaviors"/>。
/// </summary>
public class FileLogger : BaseLogger {

    /// <inheritdoc/>
    protected override void Output(string formattedText, LogLevels level) {
        base.Output(formattedText, level);
        if (!writerAvaliable && queue.Count >= 100) return; // 在 writer 就绪前，最多缓存 100 条日志
        queue.Enqueue(formattedText);
        queuedEvent.Set();
    }

    private readonly ConcurrentQueue<string> queue = new();
    private readonly AutoResetEvent queuedEvent = new(false);
    private StreamWriter? writer;
    private volatile bool writerAvaliable = false;

    /// <summary>
    /// 将日志立即写入文件。
    /// 不会抛出异常。
    /// </summary>
    public void Flush() {
        try {
            if (writer is null || !writerAvaliable) return;
            lock (flushLock) 
                while (queue.TryDequeue(out string line)) writer.WriteLine(line); // writer 指定了 AutoFlush
        } catch { }
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
                Logger.Warning(ex, "可能同时开启了多个程序，这或许会导致未知问题", LogBehaviors.Toast);
            } catch (Exception ex) {
                Logger.Warning(ex, "整理日志文件失败");
            }
            // 写入新日志文件
            try {
                writer = new StreamWriter(PathUtils.WithLongPath(Path.Combine(logFolder, $"{fileNamePrefix}1{fileNameSuffix}")), append: true) { AutoFlush = true };
                writerAvaliable = true;
                Logger.Info("日志初始化成功");
                while (true) {
                    queuedEvent.WaitOne();
                    Flush();
                }
            } catch (Exception ex) {
                Logger.Warning(ex, "写入日志文件失败", LogBehaviors.Toast);
                writerAvaliable = false;
            }
        }) { Name = nameof(FileLogger), Priority = ThreadPriority.Lowest, IsBackground = true };
        thread.Start();
    }

}
