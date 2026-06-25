namespace MeloongCore;

/// <summary>
/// 可合并和重启的工作器。
/// <para/> 在工作负载运行期间多次调用 <see cref="Run()"/> 会取消当前负载并使用最新令牌重新执行；多次调用也只重新执行一次。
/// <para/> 若即将重启，则忽略当前负载的异常。
/// </summary>
public interface IRedoableWorker {
    /// <summary>当前是否正在运行。</summary>
    bool Running { get; }
    /// <summary>是否曾经有过未被取消且未失败的运行。</summary>
    bool HasSucceeded { get; }
    /// <summary>标识上次进入空闲时，并非因为取消或失败，而是正常运行到结束。</summary>
    bool LastSucceeded { get; }
    /// <summary>在工作线程运行工作负载。若当前已在运行，则取消当前负载并使用最新令牌重启。</summary>
    void Run(CancellationToken cancellationToken = default);
    /// <summary>取消当前运行。</summary>
    void Cancel();
    /// <summary>仅在当前处于运行状态时，等待其完成。</summary>
    /// <returns>若未超时则返回 true。</returns>
    bool WaitIfRunning(int millisecondsTimeout = -1);
}

/// <inheritdoc cref="IRedoableWorker"/>
public abstract class RedoableWorkerBase<TOut>(Func<CancellationToken, TOut> workload) : IRedoableWorker {

    // ============================================ 状态 ============================================

    /// <inheritdoc/>
    public bool Running { get { lock (this) return running; } }
    private bool running;

    /// <inheritdoc/>
    public bool HasSucceeded { get { lock (this) return hasSucceeded; } }
    private bool hasSucceeded;

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

    private bool pendingRedo;

    // =========================================== 运行与取消 ===========================================

    private CancellationTokenSource? realCts;
    private CancellationToken lastToken;
    private readonly ManualResetEventSlim idleEvent = new(initialState: true);

    /// <inheritdoc/>
    public void Run(CancellationToken cancellationToken = default) {
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
        var th = new Thread(_Run);
        th.Start();
    }
    private void _Run() {
        try {
            TOut result = workload(realCts!.Token); // 实际的执行
            realCts.Token.ThrowIfCancellationRequested();
            lock (this) {
                realCts?.Dispose();
                if (pendingRedo) { // 接取重启请求
                    pendingRedo = false;
                    realCts = CancellationTokenSource.CreateLinkedTokenSource(lastToken);
                    _Run(); return;
                } else {
                    realCts = null;
                }
                running = false; idleEvent.Set();
                lastSucceeded = true;
                lastResult = result;
                hasSucceeded = true;
            }
        } catch (Exception ex) {
            lock (this) {
                realCts?.Dispose();
                if (pendingRedo) { // 接取重启请求
                    pendingRedo = false;
                    realCts = CancellationTokenSource.CreateLinkedTokenSource(lastToken);
                    if (!ex.IsCanceled()) Logger.Info(ex, "在重启前出现了异常");
                    _Run(); return;
                } else {
                    realCts = null;
                }
                running = false; idleEvent.Set();
                lastSucceeded = false;
            }
            if (!ex.IsCanceled()) Logger.Error(ex, "工作线程执行失败", LogBehavior.None);
        }
    }

    /// <inheritdoc/>
    public void Cancel() {
        lock (this) {
            pendingRedo = false;
            realCts?.Cancel();
        }
    }

    /// <inheritdoc/>
    public bool WaitIfRunning(int millisecondsTimeout = -1)
        => idleEvent.Wait(millisecondsTimeout);

}

/// <inheritdoc />
public sealed class RedoableWorker<TOut> : RedoableWorkerBase<TOut> {
    public RedoableWorker(Func<CancellationToken, TOut> workload) : base(workload) { }
    public RedoableWorker(Func<TOut> workload) : base(_ => workload()) { }
}

/// <inheritdoc />
public sealed class RedoableWorker : RedoableWorkerBase<object?> {
    public RedoableWorker(Action<CancellationToken> workload) : base(ct => { workload(ct); return null; }) { }
    public RedoableWorker(Action workload) : base(_ => { workload(); return null; }) { }
}
