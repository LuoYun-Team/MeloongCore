namespace MeloongCore;

/// <summary>
/// 可合并和重启的工作器。
/// <para/> 在工作负载运行期间多次调用 <see cref="Start(CancellationToken)"/> 会取消当前负载并使用最新令牌重新执行；多次调用也只重新执行一次。
/// </summary>
public interface IRedoableWorker {
    /// <summary>当前是否正在运行。</summary>
    bool Running { get; }
    /// <summary>是否曾经有过未被取消且未失败的运行。</summary>
    bool HasSucceeded { get; }
    /// <summary>标识上次进入空闲时，并非因为取消或失败，而是正常运行到结束。</summary>
    bool LastSucceeded { get; }
    /// <summary>在工作线程运行工作负载。若当前已在运行，则取消当前负载并使用最新令牌重启。</summary>
    void Start(CancellationToken cancellationToken = default);
    /// <summary>取消当前运行。</summary>
    void Cancel();
    /// <summary>仅在当前处于运行状态时，等待其完成。
    /// <returns>若未超时则返回 true。</returns>
    bool WaitIfRunning(int millisecondsTimeout = -1, CancellationToken cancellationToken = default);
    /// <summary>仅在当前处于运行状态时，异步等待其完成。
    /// <returns>若未超时则返回 true。</returns>
    Task<bool> WaitIfRunningAsync(int millisecondsTimeout = -1, CancellationToken cancellationToken = default);
}

// TODO: 现在的可观测性不佳，考虑增加调用方信息以及自身名称等，输出到日志，例如 CallerArgumentExpression
/// <inheritdoc cref="IRedoableWorker"/>
public abstract class RedoableWorkerBase<TOut>(Func<CancellationToken?, ProgressProvider?, TOut> workload, ProgressProvider? progress = null) : IRedoableWorker {

    // ============================================ 状态与事件 ============================================

    /// <inheritdoc/>
    public bool Running { get { lock (this) return running; } }
    private bool running;
    /// <summary>从空闲状态进入运行状态时触发。</summary>
    public event Action? Started;
    /// <summary>运行结束并进入空闲状态时触发。</summary>
    public event Action? Stopped;

    /// <inheritdoc/>
    public bool HasSucceeded { get { lock (this) return hasSucceeded; } }
    private bool hasSucceeded;
    /// <summary>工作负载成功完成时触发，参数为返回值。<para/>这可能会在重启前触发。</summary>
    public event Action<TOut>? Succeeded;
    /// <summary>工作负载执行失败时触发，参数为发生的异常。<para/>这可能会在重启前触发。</summary>
    public event Action<Exception>? Failed;
    /// <summary>运行被取消时触发。<para/>这可能会在重启前触发。</summary>
    public event Action? Canceled;

    /// <inheritdoc/>
    public bool LastSucceeded { get { lock (this) return lastSucceeded; } }
    private bool lastSucceeded;

    /// <summary>上次未被取消且未失败的运行中，工作负载的返回值。</summary>
    public TOut LastResult {
        get {
            lock (this) {
                if (!hasSucceeded) throw new InvalidOperationException("从未成功完成过。");
                return lastResult == null ? default! : (TOut) lastResult;
            }
        }
    }
    private object? lastResult;

    public readonly ProgressProvider Progress = progress ?? new ProgressProvider();

    private bool pendingRedo;

    // =========================================== 运行与取消 ===========================================

    private CancellationTokenSource? realCts;
    private CancellationToken lastToken;
    private readonly ManualResetEventSlim idleEvent = new(initialState: true);

    /// <inheritdoc/>
    public void Start(CancellationToken cancellationToken = default) {
        // 接取运行状态
        lock (this) {
            lastToken = cancellationToken;
            if (running) {
                pendingRedo = true;
                realCts?.Cancel();
                return;
            }
            running = true; idleEvent.Reset();
            realCts = CancellationTokenSource.CreateLinkedTokenSource(lastToken);
        }
        var th = new Thread(_Invoke) { IsBackground = true };
        th.Start();
    }
    private void _Invoke() {
        Started?.Invoke();
        while (true) {
            try {
                realCts!.Token.ThrowIfCancellationRequested();
                Progress?.Reset();
                TOut result = workload(realCts.Token, Progress); // 实际的执行
                realCts.Token.ThrowIfCancellationRequested();
                Succeeded?.Invoke(result);
                lock (this) {
                    realCts?.Dispose();
                    if (pendingRedo) { // 接取重启请求
                        pendingRedo = false;
                        realCts = CancellationTokenSource.CreateLinkedTokenSource(lastToken);
                        continue;
                    }
                    realCts = null;
                    lastSucceeded = true;
                    lastResult = result;
                    hasSucceeded = true;
                    running = false; idleEvent.Set();
                    Progress?.Finish();
                }
            } catch (Exception ex) {
                if (ex.IsCanceled()) {
                    Canceled?.Invoke();
                } else {
                    Failed?.Invoke(ex);
                    Logger.Log(ex, $"工作线程执行失败{(pendingRedo ? "，但即将重启，或可忽略" : "")}", pendingRedo ? LogLevel.Info : LogLevel.Warn);
                }
                lock (this) {
                    realCts?.Dispose();
                    if (pendingRedo) { // 接取重启请求
                        pendingRedo = false;
                        realCts = CancellationTokenSource.CreateLinkedTokenSource(lastToken);
                        continue;
                    }
                    realCts = null;
                    lastSucceeded = false;
                    running = false; idleEvent.Set();
                    Progress?.Skip();
                }
            }
            break;
        }
        Stopped?.Invoke();
    }

    /// <inheritdoc/>
    public void Cancel() {
        lock (this) {
            pendingRedo = false;
            realCts?.Cancel();
        }
    }

    /// <inheritdoc/>
    public bool WaitIfRunning(int millisecondsTimeout = -1, CancellationToken cancellationToken = default)
        => idleEvent.Wait(millisecondsTimeout, cancellationToken);
    /// <inheritdoc/>
    public async Task<bool> WaitIfRunningAsync(int millisecondsTimeout = -1, CancellationToken cancellationToken = default) {
        cancellationToken.ThrowIfCancellationRequested();
        var completionSource = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var waitHandle = ThreadPool.RegisterWaitForSingleObject(idleEvent.WaitHandle,
            (_, timedOut) => completionSource.TrySetResult(!timedOut), null, millisecondsTimeout, executeOnlyOnce: true);
        using var cancellationRegistration = cancellationToken.Register(() => completionSource.TrySetCanceled(cancellationToken));
        try {
            return await completionSource.Task;
        } finally {
            waitHandle.Unregister(null);
        }
    }

}

/// <inheritdoc />
public class RedoableWorker<TOut> : RedoableWorkerBase<TOut> {
    public RedoableWorker(Func<CancellationToken?, ProgressProvider?, TOut> workload) : base(workload) { }
    public RedoableWorker(Func<CancellationToken?, TOut> workload) : base((c, _) => workload(c)) { }
    public RedoableWorker(Func<TOut> workload) : base((_, _) => workload()) { }
}

/// <inheritdoc />
public class RedoableWorker : RedoableWorkerBase<object?> {
    public RedoableWorker(Action<CancellationToken?, ProgressProvider?> workload) : base((c, p) => { workload(c, p); return null; }) { }
    public RedoableWorker(Action<CancellationToken?> workload) : base((c, _) => { workload(c); return null; }) { }
    public RedoableWorker(Action workload) : base((_, _) => { workload(); return null; }) { }
}
