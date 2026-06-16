using System.Diagnostics;

namespace MeloongCore;
public static class TaskUtils {

    public static void Run(this Task task) => task.GetAwaiter().GetResult();
    public static T Run<T>(this Task<T> task) => task.GetAwaiter().GetResult();

    /// <summary>
    /// 静默运行程序并等待其结束，返回其输出和退出码。
    /// 支持 notepad、git 等命令。
    /// 超时或启动失败会抛出异常。
    /// </summary>
    public static async Task<(string Output, int ExitCode)> RunProgramAsync(string file, string arguments = "", int? timeoutMs = null, Encoding? encoding = null) {
        var info = new ProcessStartInfo {
            Arguments = arguments,
            FileName = PathUtils.ToShortPath(file),
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true, StandardOutputEncoding = encoding,
            RedirectStandardError = true, StandardErrorEncoding = encoding
        };
        using var program = new Process { StartInfo = info, EnableRaisingEvents = true };
        if (!program.Start()) throw new InvalidOperationException($"运行程序时出现意外错误：{file} {arguments}");
        bool hasTimeout = timeoutMs.HasValue && timeoutMs > 0;
        Logger.Info($"运行程序，并返回其输出：{file} {arguments}{(hasTimeout ? $"，最长可等待 {timeoutMs}ms" : "")}");

        // 读取输出和错误流
        var outputTask = program.StandardOutput.ReadToEndAsync();
        var errorTask = program.StandardError.ReadToEndAsync();
        var completionSource = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
        program.Exited += (_, _) => completionSource.TrySetResult(null);
        if (program.HasExited) completionSource.TrySetResult(null);
        var task = completionSource.Task;

        // 等待超时
        if (hasTimeout) {
            Logger.Trace($"等待程序 {program.Id} 完成");
            if (await Task.WhenAny(task, Task.Delay(timeoutMs!.Value)) != task) {
                try {
                    if (!program.HasExited) program.Kill();
                } catch (InvalidOperationException) { // 进程已退出，无需处理
                }
                throw new TimeoutException($"运行程序超时：{file} {arguments}");
            }
        }
        await task;
        return (await outputTask + await errorTask, program.ExitCode);
    }

}
