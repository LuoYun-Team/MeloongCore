namespace MeloongCore;
public static class PathUtils {

    #region 分隔符

    /// <summary>
    /// 检查路径是否以文件夹分隔符结尾。
    /// </summary>
    public static bool EndsWithSeparator(string path) => path.EndsWithF(Path.DirectorySeparatorChar) || path.EndsWithF(Path.AltDirectorySeparatorChar);

    /// <summary>
    /// 确保路径的结尾包含文件夹分隔符。
    /// </summary>
    public static string WithSeparator(string folder) => EndsWithSeparator(folder) ? folder : folder + Path.DirectorySeparatorChar;

    /// <summary>
    /// 确保路径的结尾不包含文件夹分隔符。
    /// </summary>
    public static string WithoutSeparator(string folder) => EndsWithSeparator(folder) ? folder.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) : folder;

    #endregion

    #region 长路径

    /// <summary>
    /// 若路径较长，则尽量将其转换为短路径。
    /// 若输入的是文件夹路径，不保证其结尾是否有文件夹分隔符。
    /// </summary>
    public static string ToShortPath(string fullName, bool keepFileName = false) {
        if (string.IsNullOrEmpty(fullName)) return fullName;
        fullName = PathUtils.WithoutLongPath( fullName.Replace('/', '\\'));
        if (fullName.Length <= 200) return fullName;

        // 保留文件名
        string pathToKeep = "";
        string pathToShorten = fullName;
        if (fullName.EndsWithF(".jar", true)) keepFileName = true; // jar 文件的文件名需要保留原样，否则会导致 Forge 1.20.1 无法通过文件名识别模块名
        if (keepFileName && FileUtils.Exists(fullName)) {
            pathToKeep = Path.GetFileName(fullName);
            pathToShorten = Path.GetDirectoryName(fullName);
        }

        // 逐级向上寻找已存在的文件夹，将不存在的部分挪到 suffix，不再缩短
        while (!DirectoryUtils.Exists(pathToShorten) && !FileUtils.Exists(pathToShorten)) { // 如果路径不存在
            string parentPath = Path.GetDirectoryName(pathToShorten);
            if (string.IsNullOrEmpty(parentPath) || parentPath == pathToShorten) return fullName; // 已经到达根目录，全都不存在，直接返回
            pathToKeep = Path.Combine(Path.GetFileName(pathToShorten), pathToKeep);
            pathToShorten = parentPath;
        }
        if (pathToShorten.Length <= 10) return fullName;

        // 缩短路径
        char[] buffer = new char[260];
        int result = GetShortPathNameW(pathToShorten, buffer, buffer.Length);
        if (result == 0) return fullName;
        string shortPath = new(buffer, 0, result);
        return Path.Combine(shortPath, pathToKeep);
    }
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetShortPathNameW(string lpszLongPath, [Out] char[] buffer, int cchBuffer);

    /// <summary>
    /// 将路径转换为以 \\?\ 开头的标准长路径格式。
    /// 这会去除路径末尾的分隔符，且将 / 替换为 \。
    /// </summary>
    public static string WithLongPath(string path) {
        if (string.IsNullOrWhiteSpace(path)) return path;
        if (path.StartsWithF(@"\\")) return path; // 已有长路径前缀
        path = WithoutSeparator(path).Replace('/', '\\'); // API 要求这个格式……
        return $@"\\?\{path}";
    }

    /// <summary>
    /// 去除路径开头的 \\?\。
    /// </summary>
    public static string WithoutLongPath(string path) =>
        path.AfterLast(@"\\?\");

    #endregion

}
