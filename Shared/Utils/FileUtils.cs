namespace MeloongCore;
public static class FileUtils {

    #region 写入

    /// <summary>
    /// 写入文件。
    /// 如果文件或文件夹不存在，则会自动创建。若已存在，则会覆盖原文件。
    /// </summary>
    public static void Write(string filePath, string text, Encoding? encoding = null) {
        DirectoryUtils.Create(filePath, isFilePath: true);
        File.WriteAllText(PathUtils.ToLongPath(filePath), text, encoding ?? new UTF8Encoding());
    }

    /// <summary>
    /// 写入文件。
    /// 如果文件或文件夹不存在，则会自动创建。若已存在，则会覆盖原文件。
    /// </summary>
    public static void Write(string filePath, byte[] content) {
        DirectoryUtils.Create(filePath, isFilePath: true);
        File.WriteAllBytes(PathUtils.ToLongPath(filePath), content);
    }

    /// <summary>
    /// 写入文件。
    /// 如果文件或文件夹不存在，则会自动创建。若已存在，则会覆盖原文件。
    /// </summary>
    public static void Write(string filePath, IEnumerable<byte> content) {
        DirectoryUtils.Create(filePath, isFilePath: true);
        File.WriteAllBytes(PathUtils.ToLongPath(filePath), [..content]);
    }

    /// <summary>
    /// 将 <paramref name="stream" /> 写入文件。
    /// 会将流的位置主动重置到开头。
    /// 如果文件或文件夹不存在，则会自动创建。若已存在，则会覆盖原文件。
    /// </summary>
    public static void Write(string filePath, Stream stream) {
        DirectoryUtils.Create(filePath, isFilePath: true);
        using FileStream fileStream = OpenCreate(PathUtils.ToLongPath(filePath));
        if (stream.CanSeek && stream.Position != 0) stream.Seek(0, SeekOrigin.Begin);
        stream.CopyTo(fileStream);
    }

    #endregion

    #region FileStream

    /// <summary>
    /// 打开该文件的只读 <see cref="FileStream"/>。
    /// </summary>
    public static FileStream OpenRead(string filePath) =>
        new(PathUtils.ToLongPath(filePath), FileMode.Open, FileAccess.Read, FileShare.ReadWrite);

    /// <summary>
    /// 在指定路径创建文件，并打开 <see cref="FileStream"/>。
    /// </summary>
    public static FileStream OpenCreate(string filePath) {
        DirectoryUtils.Create(filePath, isFilePath: true);
        return new(PathUtils.ToLongPath(filePath), FileMode.Create);
    }

    #endregion

    /// <summary>
    /// 删除文件。
    /// </summary>
    public static void Delete(string filePath, bool toRecycleBin = false) {
        filePath = PathUtils.ToLongPath(filePath);
        if (toRecycleBin) {
            Microsoft.VisualBasic.FileIO.FileSystem.DeleteFile(filePath,
                Microsoft.VisualBasic.FileIO.UIOption.AllDialogs,
                Microsoft.VisualBasic.FileIO.RecycleOption.SendToRecycleBin,
                Microsoft.VisualBasic.FileIO.UICancelOption.ThrowException);
        } else {
            File.Delete(filePath);
        }
    }

    /// <summary>
    /// 确定指定的文件是否存在。
    /// </summary>
    public static bool Exists(string filePath) => 
        File.Exists(PathUtils.ToLongPath(filePath));

    /// <summary>
    /// 创建 <see cref="FileInfo"/> 对象。
    /// </summary>
    public static FileInfo GetInfo(string path) => 
        new(PathUtils.ToLongPath(path));

    /// <summary>
    /// 复制文件。
    /// 会创建对应文件夹、覆盖已有的文件。
    /// </summary>
    public static void Copy(string sourceFilePath, string destFilePath) {
        if (sourceFilePath == destFilePath) return; // 如果复制同一个文件则跳过
        DirectoryUtils.Create(destFilePath, isFilePath: true);
        File.Copy(PathUtils.ToLongPath(sourceFilePath), PathUtils.ToLongPath(destFilePath), true);
    }

    /// <summary>
    /// 剪切文件。
    /// 会创建对应文件夹、覆盖已有的文件。
    /// </summary>
    public static void Move(string sourceFilePath, string destFilePath) {
        if (sourceFilePath == destFilePath) return; // 如果移动同一个文件则跳过
        DirectoryUtils.Create(destFilePath, isFilePath: true);
        File.Move(PathUtils.ToLongPath(sourceFilePath), PathUtils.ToLongPath(destFilePath));
    }

}
