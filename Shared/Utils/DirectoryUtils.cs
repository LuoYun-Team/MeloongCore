namespace MeloongCore;
public static class DirectoryUtils {

    /// <summary>
    /// 创建文件夹，或文件所在的文件夹。
    /// 文件夹已存在时不会抛出异常。
    /// </summary>
    public static void Create(string path, bool isFilePath = false) {
        if (isFilePath) path = Path.GetDirectoryName(path);
        if (Exists(path)) return;
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
    /// 创建 <see cref="DirectoryInfo"/> 对象。
    /// </summary>
    public static DirectoryInfo GetInfo(string path) 
        => new(PathUtils.WithLongPath(path));

    /// <summary>
    /// 返回指定路径下的所有文件，不以 \\?\ 开头。
    /// </summary>
    public static IEnumerable<string> GetFiles(string path, bool topDirectoryOnly = false, string searchPattern = "*") {
        if (!Exists(path)) return [];
        Logger.Trace($"枚举文件（{searchPattern}，递归：{!topDirectoryOnly}）：{path}");
        return Directory.EnumerateFiles(PathUtils.WithLongPath(path), searchPattern, topDirectoryOnly ? SearchOption.TopDirectoryOnly : SearchOption.AllDirectories).
            Select(PathUtils.WithoutLongPath);
    }

    /// <summary>
    /// 返回指定路径下的所有文件夹，不以分隔符结尾，不以 \\?\ 开头。
    /// </summary>
    public static IEnumerable<string> GetDirectories(string path, bool topDirectoryOnly = false, string searchPattern = "*") {
        if (!Exists(path)) return [];
        Logger.Trace($"枚举文件夹（{searchPattern}，递归：{!topDirectoryOnly}）：{path}");
        return Directory.EnumerateDirectories(PathUtils.WithLongPath(path), searchPattern, topDirectoryOnly ? SearchOption.TopDirectoryOnly : SearchOption.AllDirectories).
            Select(PathUtils.WithoutLongPath);
    }

    /// <summary>
    /// 该文件夹是否为空。
    /// 如果文件夹不存在，也返回 true。
    /// </summary>
    public static bool IsEmpty(string path) {
        if (!Exists(path)) return true;
        return !Directory.EnumerateFileSystemEntries(PathUtils.WithLongPath(path)).Any();
    }

    /// <summary>
    /// 剪切文件夹。
    /// </summary>
    public static void Move(string sourceDirName, string destDirName) {
        if (!Exists(sourceDirName)) return;
        DirectoryUtils.Delete(destDirName); // Move 要求不存在对应文件夹
        Logger.Trace($"剪切文件夹：{sourceDirName} → {destDirName}");
        Directory.Move(PathUtils.WithLongPath(sourceDirName), PathUtils.WithLongPath(destDirName));
    }

    /// <summary>
    /// 删除文件夹。
    /// </summary>
    public static void Delete(string dirPath, bool toRecycleBin = false) {
        if (!Exists(dirPath)) return;
        Logger.Trace($"{(toRecycleBin ? "将文件夹删除到回收站" : "删除文件夹")}：{dirPath}");
        if (toRecycleBin) {
            FileUtils.DeleteToRecycleBin(dirPath);
        } else {
            Directory.Delete(PathUtils.WithLongPath(dirPath), recursive:true);
        }
    }
}
