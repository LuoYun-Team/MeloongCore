namespace MeloongCore.Extensions;
public static class ExceptionExtensions {

    /// <summary>
    /// 返回该异常最底层的 <see cref="Exception.InnerException"/>。
    /// </summary>
    public static Exception RootException(this Exception ex) {
        while (ex.InnerException is { } inner) ex = inner;
        return ex;
    }

    /// <summary>
    /// 获取 <paramref name="ex"/> 的用户友好描述。
    /// 若 <paramref name="showMultilineStacks"/> 为 true，则返回多行的详细描述与堆栈信息；否则不整理堆栈，仅将 <see cref="Exception.Message"/> 汇总到一行。
    /// </summary>
    public static string GetDisplay(this Exception? ex, bool showMultilineStacks) {
        if (ex is null) return "无可用错误信息！";

        // 提取堆栈信息
        var lines = new List<string>();
        bool isInnerException = false;
        for (Exception? currentEx = ex; currentEx is not null; currentEx = currentEx.InnerException) {
            if (showMultilineStacks) {
                lines.Add((isInnerException ? "→ " : "") + currentEx.Message.ReplaceLineEndings("\r\n", true));
                if (currentEx.GetType() != typeof(Exception)) lines.Add("   错误类型：" + currentEx.GetType().FullName);
                var stackLines = (currentEx.StackTrace?.ReplaceLineEndings("\r", true).Split('\r') ?? [])
                    .Select(l => l.BeforeFirst("(") + l.AfterFirst(")"))
                    .Distinct();
                lines.AddRange(stackLines);
            } else {
                lines.Add(currentEx.Message.ReplaceLineEndings(" ", true));
            }
            isInnerException = true;
        }

        // 分析常见错误原因
        string? commonReason = null;
        var rootException = ex.RootException();
        if (rootException is TypeLoadException or BadImageFormatException or MissingMethodException or NotImplementedException or TypeInitializationException)
            commonReason = "运行环境存在问题。请尝试重新安装 .NET Framework 4.8 然后再试。若无法安装，请先卸载较新版本的 .NET Framework，然后再尝试安装。";
        else if (rootException is UnauthorizedAccessException)
            commonReason = "程序权限不足。请尝试右键程序，选择以管理员身份运行，或将文件移动到其他文件夹。";
        else if (rootException is OutOfMemoryException)
            commonReason = "系统的运行内存不足。请在关闭一部分不需要的程序后再试。";
        else if (rootException is System.Net.Sockets.SocketException && lines.Any(l => l.Contains("WSAStartup")))
            commonReason = "请尝试卸载中国移动云盘，然后再试。";
        else if (ex.IsNetworkRelated())
            commonReason = "你的网络环境不佳，请稍后再试，或使用 VPN 改善网络环境。";

        // 输出
        if (showMultilineStacks) {
            if (commonReason is null) {
                return lines.Join("\r\n");
            } else {
                return commonReason + "\r\n\r\n————————————\r\n详细错误信息：\r\n" + lines.Join("\r\n");
            }
        } else {
            lines = lines.Distinct().ToList();
            if (commonReason is null) {
                lines.Reverse();
                return lines.Join(" ← ");
            } else {
                return commonReason + "详细错误：" + lines.First();
            }
        }
    }

    /// <summary>
    /// 判断某个 <paramref name="ex"/> 是否为网络连接不良所导致。
    /// </summary>
    public static bool IsNetworkRelated(this Exception ex) {
        // 提取 Message（不能用 GetDetail，因为堆栈的方法参数中就有 timeout 字样）
        string detail = "";
        var cur = ex;
        detail += cur.Message;
        while (cur.InnerException is not null) {
            cur = cur.InnerException;
            detail += cur.Message;
        }
        // 判断
        if (detail.Contains("(403)")) return false;
        return new[] {
            "(408)", "超时", "timeout", "网络请求失败", "连接尝试失败", "远程主机强迫关闭了",
            "远程方已关闭传输流", "未能解析此远程名称", "由于目标计算机积极拒绝", "基础连接已经关闭"
        }.Any(k => detail.ContainsIgnoreCase(k));
    }

}
