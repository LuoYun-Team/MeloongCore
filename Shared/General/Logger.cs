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

/// <summary>
/// 将日志输出到 <see cref="Debug"/> 的 <see cref="ILogger"/> 基础实现。
/// </summary>
public class ConsoleLogger : ILogger {
    /// <summary>
    /// 在写入日志前对文本进行预处理的处理器。
    /// </summary>
    public event Func<string, string>? TextProcessor;

    /// <summary>
    /// 最低输出等级，低于此等级的日志将被忽略。默认为 <see cref="LogLevels.Trace"/>。
    /// </summary>
    public LogLevels MinLevel { get; set; } = LogLevels.Trace;

    public virtual void Log(string message, LogLevels level, LogBehaviors behavior, string filePath) {
        if (level < MinLevel) return;
        try {
            Emit(message, level, filePath);
        } catch { }
    }
    public virtual void Log(Exception ex, string? message, LogLevels level, LogBehaviors behavior, string filePath) {
        if (level < MinLevel) return;
        if (ex is ThreadInterruptedException) return;
        try {
            Emit((message is null ? "" : $"{message}：") + ex.GetDetail(true), level, filePath);
        } catch { }
    }

    private void Emit(string body, LogLevels level, string filePath) {
        // 构造原始文本
        string threadName = Thread.CurrentThread.Name is { Length: > 0 } n ? n : "主线程";
        string file = filePath.AfterLast("\\").BeforeFirst(".");
        if (file.StartsWithF("Mod")) file = file.AfterFirst("Mod");
        string text = $"<{threadName}> [{file}] {body}\r\n";

        // 应用处理器
        if (TextProcessor is not null) foreach (var h in TextProcessor.GetInvocationList().OfType<Func<string, string>>()) text = h(text);

        string timestamp = $"{DateTime.Now:HH':'mm':'ss'.'fff} | ";
        string debugPrefix = level switch { LogLevels.Trace => "T ", LogLevels.Warning => "W ", LogLevels.Error => "E ", _ => "I " };
        string formatted = text
            .ReplaceLineEndings("\n", mergeMultiple: true).Split(['\n'], StringSplitOptions.RemoveEmptyEntries)
            .Select(t => { t = debugPrefix + timestamp + t; return t; })
            .Join("\r\n");

        WriteOutput(formatted, level);
    }

    /// <summary>
    /// 将格式化后的日志文本写入额外目标。
    /// <see cref="ConsoleLogger"/> 不执行额外操作（已由 <see cref="Emit"/> 写入调试控制台）；
    /// 子类可重写此方法以追加写入更多目标。
    /// </summary>
    protected virtual void WriteOutput(string formattedText, LogLevels level) {
        Debug.WriteLine(formattedText);
    }
}

/// <summary>
/// 在 <see cref="ConsoleLogger"/> 基础上将日志缓冲写入文件的 <see cref="ILogger"/> 实现。
/// </summary>
public class FileLogger : ConsoleLogger {
    private readonly ConcurrentQueue<string> logQueue = new();
    private StreamWriter? logWriter;

    /// <inheritdoc/>
    protected override void WriteOutput(string formattedText, LogLevels level) {
        base.WriteOutput(formattedText, level);
        logQueue.Enqueue(formattedText);
    }

    /// <summary>
    /// 启动日志文件写入。轮换历史日志文件，随后在后台线程持续将缓冲区内容写入 Log1.txt。
    /// </summary>
    /// <param name="logFolder">日志文件夹路径，以 \ 结尾。</param>
    /// <param name="onInitFailed">初始化失败时的回调，参数为捕获到的异常。</param>
    public void LogStart(string logFolder, Action<Exception>? onInitFailed = null) {
        var thread = new Thread(() => {
            bool isInitSuccess = true;
            try {
                for (int i = 4; i >= 1; i--) {
                    if (FileUtils.Exists(logFolder + $"Log{i}.txt")) {
                        if (FileUtils.Exists(logFolder + $"Log{i + 1}.txt"))
                            FileUtils.Delete(logFolder + $"Log{i + 1}.txt");
                        FileUtils.Copy(logFolder + $"Log{i}.txt", logFolder + $"Log{i + 1}.txt");
                    }
                }
                FileUtils.CreateAsStream(logFolder + "Log1.txt").Dispose();
            } catch (Exception ex) {
                isInitSuccess = false;
                onInitFailed?.Invoke(ex);
            }
            try {
                logWriter = new StreamWriter(logFolder + "Log1.txt", append: true) { AutoFlush = true };
            } catch {
                logWriter = null;
            }
            while (true) {
                if (isInitSuccess)
                    LogFlush();
                else
                    while (logQueue.TryDequeue(out _)) { } // 清空队列避免内存爆炸
                Thread.Sleep(50);
            }
        }) {
            Name = "Log Writer",
            Priority = ThreadPriority.Lowest,
            IsBackground = true,
        };
        thread.Start();
    }

    /// <summary>
    /// 将日志队列中的内容立即写入文件。
    /// </summary>
    public void LogFlush() {
        try {
            if (logWriter is null || logQueue.IsEmpty) return;
            var sb = new StringBuilder();
            while (logQueue.TryDequeue(out string? line))
                sb.Append(line).Append("\r\n");
            logWriter.Write(sb);
        } catch { }
    }
}