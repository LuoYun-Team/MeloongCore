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
    public static bool Exists(string path) => Directory.Exists(PathUtils.ToLongPath(path));

    /// <summary>
    /// 创建 <see cref="DirectoryInfo"/> 对象。
    /// </summary>
    public static DirectoryInfo GetInfo(string path) => new(PathUtils.ToLongPath(path));

}
