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
    public static IEnumerable<string> EnumerateFiles(string path, string searchPattern = "*", bool topDirectoryOnly = false) {
        if (!Exists(path)) return [];
        return Directory.EnumerateFiles(PathUtils.ToLongPath(path), searchPattern, topDirectoryOnly ? SearchOption.TopDirectoryOnly : SearchOption.AllDirectories);
    }

    /// <summary>
    /// 返回指定路径下的所有文件夹。
    /// </summary>
    public static IEnumerable<string> EnumerateDirectories(string path, string searchPattern = "*", bool topDirectoryOnly = false) {
        if (!Exists(path)) return [];
        return Directory.EnumerateDirectories(PathUtils.ToLongPath(path), searchPattern, topDirectoryOnly ? SearchOption.TopDirectoryOnly : SearchOption.AllDirectories);
    }
}
