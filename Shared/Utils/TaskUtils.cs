using System.Diagnostics;

namespace MeloongCore;
public static class TaskUtils {

    /// <summary>
    /// 静默运行程序并等待其结束，返回其输出和退出码。
    /// 支持 notepad、git 等命令。
    /// 超时或启动失败会抛出异常。
    /// </summary>
    public static async Task<(string Output, int ExitCode)> RunProgramAsync(string file, string arguments = "", int? timeoutMs = null, Encoding? encoding = null, CancellationToken cancellationToken = default) {
        cancellationToken.ThrowIfCancellationRequested();
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
        bool hasTimeout = timeoutMs is > 0;
        Logger.Info($"运行程序，并返回其输出：{file} {arguments}{(hasTimeout ? $"，最长可等待 {timeoutMs}ms" : "")}");

        // 输出和错误流
        var outputTask = program.StandardOutput.ReadToEndAsync();
        var errorTask = program.StandardError.ReadToEndAsync();

        var completionSource = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
        program.Exited += (_, _) => completionSource.TrySetResult(null);
        if (program.HasExited) completionSource.TrySetResult(null);
        var task = completionSource.Task;
        var timeoutTask = Task.Delay(hasTimeout ? timeoutMs!.Value : Timeout.Infinite);
        var cancelTask = Task.Delay(Timeout.Infinite, cancellationToken);
        Logger.Trace($"等待程序 {program.Id} 完成");
        var completedTask = await Task.WhenAny(task, timeoutTask, cancelTask);
        if (completedTask != task) {
            try {
                if (!program.HasExited) program.Kill();
            } catch (InvalidOperationException) { // 进程已退出，无需处理
            }
            if (completedTask == cancelTask) throw new OperationCanceledException(cancellationToken);
            throw new TimeoutException($"运行程序超时：{file} {arguments}");
        }
        return (await outputTask + await errorTask, program.ExitCode);
    }

    public static void ForEach<T>(IEnumerable<T> source, Action<T> body)
        => Parallel.ForEach(source, body);
    public static async Task ForEachAsync<T>(IEnumerable<T> source, int maxDegreeOfParallelism, Func<T, CancellationToken, Task> body, CancellationToken cancellationToken = default) {
        using var linkedCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var token = linkedCancellationTokenSource.Token;
        var enumeratorLock = new object();
        using var enumerator = source.GetEnumerator();
        async Task WorkerAsync() {
            try {
                while (true) {
                    T item;
                    lock (enumeratorLock) {
                        token.ThrowIfCancellationRequested();
                        if (!enumerator.MoveNext()) return;
                        item = enumerator.Current;
                    }
                    await body(item, token).ConfigureAwait(false);
                }
            } catch {
                try {
                    linkedCancellationTokenSource.Cancel(); // 让尚未开始的 worker 尽快停止
                } catch {
                }
                throw;
            }
        }
        await Task.WhenAll([.. Enumerable.Range(0, maxDegreeOfParallelism).Select(_ => Task.Run(WorkerAsync))]).ConfigureAwait(false);
    }

}
