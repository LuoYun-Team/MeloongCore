namespace MeloongCore.Extensions;
public static class ExceptionExtensions {
    /// <summary>
    /// 提取 <paramref name="ex"/> 与其 InnerException 的详细描述与堆栈信息。返回内容总是多于一行。
    /// </summary>
    /// <param name="showAllStacks">是否必须显示所有堆栈。通常用于判定堆栈信息。</param>
    public static string GetDetail(this Exception? ex, bool showAllStacks = false) {
        if (ex is null) return "无可用错误信息！";

        // 获取最底层的异常
        var outerEx = ex;
        var innerEx = ex;
        while (innerEx.InnerException is not null) innerEx = innerEx.InnerException;

        // 获取各级错误的描述与堆栈信息
        var descList = new List<string>();
        bool isInner = false;
        while (ex is not null) {
            descList.Add((isInner ? "→ " : "") + ex.Message.ReplaceLineEndings("\r\n", true));
            if (ex.StackTrace is not null) {
                foreach (string stack in ex.StackTrace.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)) {
                    if (showAllStacks || stack.ContainsIgnoreCase("pcl"))
                        descList.Add(stack.ReplaceLineEndings(""));
                }
            }
            if (ex.GetType().FullName != "System.Exception")
                descList.Add("   错误类型：" + ex.GetType().FullName);
            ex = ex.InnerException;
            isInner = true;
        }

        // 构造输出信息
        string? usualReason = AnalyzeUsualReason(innerEx, outerEx, descList);
        if (usualReason is not null) {
            return usualReason + "\r\n\r\n————————————\r\n详细错误信息：\r\n" + descList.Join("\r\n");
        } 
        return descList.Join("\r\n");
    }

    /// <summary>
    /// 提取 <paramref name="ex"/> 与其 InnerException 的描述，汇总到一行。
    /// </summary>
    public static string GetBrief(this Exception? ex) {
        if (ex is null) return "无可用错误信息！";

        // 获取最底层的异常
        var outerEx = ex;
        var innerEx = ex;
        while (innerEx.InnerException is not null) innerEx = innerEx.InnerException;

        // 获取各级错误的描述
        var descList = new List<string>();
        while (ex is not null) {
            descList.Add(ex.Message.ReplaceLineEndings(" ", true));
            ex = ex.InnerException;
        }
        descList = descList.Distinct().ToList();

        // 构造输出信息
        string? usualReason = AnalyzeUsualReason(innerEx, outerEx, descList);
        if (usualReason is not null)
            return usualReason + "详细错误：" + descList.First();
        descList.Reverse();
        return descList.Join(" ← ");
    }

    private static string? AnalyzeUsualReason(Exception innerEx, Exception outerEx, List<string> descList) {
        if (innerEx is TypeLoadException or BadImageFormatException or MissingMethodException or NotImplementedException or TypeInitializationException)
            return "PCL 的运行环境存在问题。请尝试重新安装 .NET Framework 4.8 然后再试。若无法安装，请先卸载较新版本的 .NET Framework，然后再尝试安装。";
        if (innerEx is UnauthorizedAccessException)
            return "PCL 的权限不足。请尝试右键 PCL 选择以管理员身份运行。";
        if (innerEx is OutOfMemoryException)
            return "你的电脑运行内存不足，导致 PCL 无法继续运行。请在关闭一部分不需要的程序后再试。";
        if (innerEx is COMException)
            return "由于操作系统或显卡存在问题，导致出现错误。请尝试重启 PCL。";
        if (innerEx is System.Net.Sockets.SocketException && descList.Any(l => l.Contains("WSAStartup")))
            return "请尝试卸载中国移动云盘，然后再试。";
        if (outerEx.IsNetworkRelated())
            return "你的网络环境不佳，请稍后再试，或使用 VPN 改善网络环境。";
        return null;
    }

    /// <summary>
    /// 判断某个 <paramref name="ex"/> 是否为网络问题所导致。
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