namespace MeloongCore;
public static class DirectoryUtils {

    /// <summary>
    /// 创建文件夹，或文件所在的文件夹。
    /// 文件夹已存在时不会抛出异常。
    /// </summary>
    public static void Create(string path) {
        if (DirectoryUtils.Exists(path)) return;
        Logger.Trace($"新建文件夹：{path}");
        path = PathUtils.WithLongPath(path);
        Directory.CreateDirectory(path);
    }

    /// <summary>
    /// 判断文件夹是否存在。
    /// </summary>
    public static bool Exists(string path) 
        => Directory.Exists(PathUtils.WithLongPath(path));

    /// <summary>
    /// 获取 <see cref="DirectoryInfo"/> 对象。
    /// </summary>
    public static DirectoryInfo GetInfo(string path) 
        => new(PathUtils.WithLongPath(path));

    /// <summary>
    /// 返回指定路径下的所有文件，不以 \\?\ 开头。
    /// 如果文件夹不存在，返回空列表。
    /// </summary>
    public static IEnumerable<string> GetFiles(string path, bool topDirectoryOnly = false, string searchPattern = "*") {
        if (!DirectoryUtils.Exists(path)) return [];
        return Directory.EnumerateFiles(PathUtils.WithLongPath(path), searchPattern, topDirectoryOnly ? SearchOption.TopDirectoryOnly : SearchOption.AllDirectories).
            Select(PathUtils.WithoutLongPath);
    }

    /// <summary>
    /// 返回指定路径下的所有文件夹，不以分隔符结尾，不以 \\?\ 开头。
    /// 如果文件夹不存在，返回空列表。
    /// </summary>
    public static IEnumerable<string> GetDirectories(string path, bool topDirectoryOnly = false, string searchPattern = "*") {
        if (!DirectoryUtils.Exists(path)) return [];
        return Directory.EnumerateDirectories(PathUtils.WithLongPath(path), searchPattern, topDirectoryOnly ? SearchOption.TopDirectoryOnly : SearchOption.AllDirectories).
            Select(PathUtils.WithoutLongPath);
    }

    /// <summary>
    /// 该文件夹是否为空。
    /// 如果文件夹不存在，返回 true。
    /// </summary>
    public static bool IsEmpty(string path) {
        if (!DirectoryUtils.Exists(path)) return true;
        return !Directory.EnumerateFileSystemEntries(PathUtils.WithLongPath(path)).Any();
    }

    /// <summary>
    /// 复制文件夹。
    /// 若原文件夹不存在，则不执行。
    /// </summary>
    public static void Copy(string sourceFolder, string destFolder, Action<double>? progressHandler = null) {
        if (!DirectoryUtils.Exists(sourceFolder)) {
            Logger.Info($"尝试复制文件夹，但原文件夹不存在，已跳过复制：{sourceFolder} → {destFolder}");
            progressHandler?.Invoke(1);
            return;
        }
        sourceFolder = PathUtils.Normalize(sourceFolder, true);
        destFolder = PathUtils.Normalize(destFolder, true);
        int doneCount = 0;
        Lazy<int> totalCount = new(() => DirectoryUtils.GetFiles(sourceFolder).Count());
        Logger.Trace($"复制文件夹：{sourceFolder} → {destFolder}");
        foreach (var file in DirectoryUtils.GetFiles(sourceFolder)) {
            FileUtils.Copy(file, file.Replace(sourceFolder, destFolder));
            doneCount++;
            if (progressHandler is not null) progressHandler?.Invoke((double) doneCount / totalCount.Value);
        }
    }

    /// <summary>
    /// 剪切文件夹。
    /// </summary>
    public static void Move(string sourceFolder, string destFolder) {
        if (!DirectoryUtils.Exists(sourceFolder)) return;
        DirectoryUtils.Delete(destFolder); // Move 要求不存在对应文件夹
        Logger.Trace($"剪切文件夹：{sourceFolder} → {destFolder}");
        Directory.Move(PathUtils.WithLongPath(sourceFolder), PathUtils.WithLongPath(destFolder));
    }

    /// <summary>
    /// 删除文件夹。
    /// </summary>
    public static void Delete(string folder, bool toRecycleBin = false) {
        // 安全检查
        if (!DirectoryUtils.Exists(folder)) return;
        Logger.Trace($"{(toRecycleBin ? "将文件夹删除到回收站" : "删除文件夹")}：{folder}");
        folder = PathUtils.Normalize(folder, true);
        if (folder == Path.GetPathRoot(folder))
            throw new UnauthorizedAccessException($"不应删除磁盘根目录：{folder}");
        if (criticalFolders.Value.Any(f => f.StartsWithF(folder, ignoreCase: true)))
            throw new UnauthorizedAccessException($"不应删除的文件夹：{folder}");
        // 删除
        if (toRecycleBin) {
            FileUtils.DeleteToRecycleBin(folder); // 删除到回收站
        } else {
            DeleteInternal(PathUtils.WithLongPath(folder)); // 永久删除
            static void DeleteInternal(string folder) {
                try {
                    foreach (string filePath in DirectoryUtils.GetFiles(folder, true)) FileUtils.Delete(filePath); // 删除文件
                    foreach (string str in DirectoryUtils.GetDirectories(folder, true)) DeleteInternal(str); // 递归删除子文件夹
                    ResilientUtils.RetryOn<IOException>(() => Directory.Delete(folder, true)); // 最终删除文件夹
                } catch (DirectoryNotFoundException ex) { // #4549，也可能已被其他线程删除
                    if (DirectoryUtils.Exists(folder)) {
                        Logger.Warn(ex, $"该文件夹可能为孤立的符号链接，尝试直接删除（{folder}）");
                        Directory.Delete(folder, true);
                    } else {
                        throw;
                    }
                }
            }
        }
    }
    private static readonly Lazy<HashSet<string>> criticalFolders = new(() => new(new[] {
        PathUtils.CurrentFolder,
        Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
        Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
        Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads") // 下载文件夹没有 SpecialFolder 枚举
    }.Select(f => PathUtils.Normalize(f, true))));

}
