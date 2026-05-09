namespace MeloongCore;
public static class PathUtils {

    #region 分隔符

    /// <summary>
    /// 确保路径的末尾包含任意文件夹分隔符。
    /// </summary>
    public static string WithSeparator(string folder) 
        => folder + (folder.EndsWithF(Path.DirectorySeparatorChar) || folder.EndsWithF(Path.AltDirectorySeparatorChar) ? "" : Path.DirectorySeparatorChar);

    /// <summary>
    /// 确保路径的结尾不包含文件夹分隔符。
    /// </summary>
    public static string WithoutSeparator(string folder) 
        => folder.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

    #endregion

    #region 长路径

    /// <summary>
    /// 若路径较长，则尽量将其转换为短路径。
    /// 若输入的是文件夹路径，不保证其结尾是否有文件夹分隔符。
    /// </summary>
    public static string ToShortPath(string fullName, bool keepFileName = false) {
        if (string.IsNullOrEmpty(fullName)) return fullName;
        if (!fullName.Contains(":")) return fullName;
        fullName = PathUtils.Normalize(fullName, false);
        if (fullName.Length <= 200) return fullName;

        // 保留文件名
        string pathToKeep = "";
        string pathToShorten = fullName;
        if (fullName.EndsWithF(".jar", true)) keepFileName = true; // jar 文件的文件名需要保留原样，否则会导致 Forge 1.20.1 无法通过文件名识别模块名
        if (keepFileName && FileUtils.Exists(fullName)) {
            pathToKeep = PathUtils.GetLastPart(fullName);
            pathToShorten = PathUtils.RemoveLastPart(fullName);
        }

        // 逐级向上寻找已存在的文件夹，将不存在的部分挪到 suffix，不再缩短
        while (!DirectoryUtils.Exists(pathToShorten) && !FileUtils.Exists(pathToShorten)) { // 如果路径不存在
            string parentPath = Path.GetDirectoryName(pathToShorten);
            if (string.IsNullOrEmpty(parentPath) || parentPath == pathToShorten) return fullName; // 已经到达根目录，全都不存在，直接返回
            pathToKeep = Path.Combine(PathUtils.GetLastPart(pathToShorten), pathToKeep);
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
    /// 将完整路径转换为以 <c>\\?\</c> 开头的标准长路径格式。
    /// 这会去除路径末尾的分隔符，且将 / 替换为 \。
    /// </summary>
    public static string WithLongPath(string path) {
        if (string.IsNullOrWhiteSpace(path)) return path;
        if (path.StartsWithF(@"\\?\")) return path; // 已经是长路径
        path = WithoutSeparator(path).Replace('/', '\\'); // API 要求这个格式……
        if (path.StartsWithF(@"\\")) {
            return $@"\\?\UNC\{path.Substring(2)}";
        } else {
            return $@"\\?\{path}";
        }
    }

    /// <summary>
    /// 去除路径开头的 <c>\\?\</c>。
    /// </summary>
    public static string WithoutLongPath(string path) {
        if (path.StartsWithF(@"\\?\UNC\")) return @"\\" + path.AfterLast(@"\\?\UNC\");
        if (path.StartsWithF(@"\\?\")) return path.AfterLast(@"\\?\");
        return path;
    }

    #endregion

    #region 路径处理

    /// <summary>
    /// 去除路径的最后一级，并移除末尾的分隔符。
    /// <code>
    /// "C:\foo\bar.txt" → "C:\foo"
    /// "C:\foo\bar\" → "C:\foo"
    /// "C:\foo\bar" → "C:\foo"
    /// "C:\foo\" → "C:"
    /// "https://foo.bar/file/pack.zip?arg=1" → "https://foo.bar/file"
    /// </code></summary>
    public static string RemoveLastPart(string path) {
        if (path!.Contains("://")) { // 网络路径
            path = PathUtils.WithoutSeparator(path.BeforeFirst("#").BeforeFirst("?")); // 去除参数
            return PathUtils.WithoutSeparator(path).BeforeLast("/");
        } else { // 文件路径
            return PathUtils.WithoutSeparator(path!).BeforeLast("\\");
        }
    }

    /// <summary>
    /// 获取路径的最后一级，即文件名，或当前文件夹名。
    /// <code>
    /// "C:\foo\bar.txt" → "bar.txt"
    /// "C:\foo\bar\" → "bar"
    /// "C:\foo\bar" → "bar"
    /// "https://foo.bar/file/pack.zip?arg=1" → "pack.zip"
    /// </code></summary>
    public static string GetLastPart(string path) {
        if (path!.Contains("://")) { // 网络路径
            path = PathUtils.WithoutSeparator(path.BeforeFirst("#").BeforeFirst("?")); // 去除参数
            return path.AfterLast("/");
        } else { // 文件路径
            return PathUtils.WithoutSeparator(path).AfterLast("\\");
        }
    }

    /// <summary>
    /// 获取路径或文件名中，仅不包含最后一级扩展名的部分。
    /// <code>
    /// "C:\foo\bar.txt" → "bar"
    /// "create.jar.disabled" → "create.jar"
    /// "https://foo.bar/file/page.xaml.vb?arg=1" → "page.xaml"
    /// </code>
    /// </summary>
    public static string GetFileNameWithoutExtension(string path) 
        => GetLastPart(path).BeforeLast(".");

    /// <summary>
    /// 获取路径或文件名中的最后一级文件扩展名，不包含 <c>.</c>，固定小写。
    /// 如果没有 <c>.</c> 则返回空字符串。
    /// <code>
    /// "C:\FOO\BAR.TXT" → "txt"
    /// "create.jar.disabled" → "disabled"
    /// "C:\foo\bar" → ""
    /// "https://foo.bar/file/page.xaml.vb?arg=1" → "vb"
    /// </code>
    /// </summary>
    public static string GetExtension(string path) {
        var name = GetLastPart(path);
        return name.Contains(".") ? name.AfterLast(".").Lower() : "";
    }

    /// <summary>
    /// 将路径转换为标准格式：将短路径展开，将分隔符改为 \，去除前导的 <c>\\?\</c>，统一末尾是否包含分隔符。
    /// </summary>
    public static string Normalize(string path, bool withSeparator) {
        path = PathUtils.WithoutLongPath(Path.GetFullPath(path)).Replace("/", @"\");
        return withSeparator ? PathUtils.WithSeparator(path) : PathUtils.WithoutSeparator(path);
    }

    #endregion

    #region 常用路径

    /// <summary>
    /// 程序可执行文件的所在文件夹，以 \ 结尾。
    /// </summary>
    public static string CurrentFolder => PathUtils.WithSeparator(AppDomain.CurrentDomain.BaseDirectory);

    #endregion

}
