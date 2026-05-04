namespace MeloongCore;
public static class DirectoryUtils {

    /// <summary>
    /// 创建文件夹，或文件所在的文件夹。
    /// 文件夹已存在时不会抛出异常。
    /// </summary>
    public static void Create(string path, bool isFilePath = false) {
        if (isFilePath) path = Path.GetDirectoryName(path);
        Directory.CreateDirectory(PathUtils.ToLongPath(path));
    }

    /// <summary>
    /// 判断文件夹是否存在。
    /// </summary>
    public static bool Exists(string path) => 
        Directory.Exists(PathUtils.ToLongPath(path));

    /// <summary>
    /// 创建 <see cref="DirectoryInfo"/> 对象。
    /// </summary>
    public static DirectoryInfo GetInfo(string path) => 
        new(PathUtils.ToLongPath(path));

    /// <summary>
    /// 返回指定路径下的所有文件。
    /// </summary>
    public static IEnumerable<string> GetFiles(string path, bool topDirectoryOnly = false, string searchPattern = "*") {
        if (!Exists(path)) return [];
        return Directory.EnumerateFiles(PathUtils.ToLongPath(path), searchPattern, topDirectoryOnly ? SearchOption.TopDirectoryOnly : SearchOption.AllDirectories);
    }

    /// <summary>
    /// 返回指定路径下的所有文件夹，不以分隔符结尾。
    /// </summary>
    public static IEnumerable<string> GetDirectories(string path, bool topDirectoryOnly = false, string searchPattern = "*") {
        if (!Exists(path)) return [];
        return Directory.EnumerateDirectories(PathUtils.ToLongPath(path), searchPattern, topDirectoryOnly ? SearchOption.TopDirectoryOnly : SearchOption.AllDirectories);
    }

    /// <summary>
    /// 该文件夹是否为空。
    /// 如果文件夹不存在，也返回 true。
    /// </summary>
    public static bool IsEmpty(string path) {
        if (!Exists(path)) return true;
        return !Directory.EnumerateFileSystemEntries(PathUtils.ToLongPath(path)).Any();
    }

    /// <summary>
    /// 剪切文件夹。
    /// </summary>
    public static void Move(string sourceDirName, string destDirName) {
        if (!Exists(sourceDirName)) return;
        Create(destDirName);
        Directory.Move(PathUtils.ToLongPath(sourceDirName), PathUtils.ToLongPath(destDirName));
    }
}
