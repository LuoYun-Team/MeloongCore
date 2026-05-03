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
    /// 将 <paramref name="stream" /> 写入文件。
    /// 会将流的位置主动重置到开头。
    /// 如果文件或文件夹不存在，则会自动创建。若已存在，则会覆盖原文件。
    /// </summary>
    public static void Write(string filePath, Stream stream) {
        DirectoryUtils.Create(filePath, isFilePath: true);
        using FileStream fileStream = new(PathUtils.ToLongPath(filePath), FileMode.Create, FileAccess.Write);
        if (stream.CanSeek && stream.Position != 0) stream.Seek(0, SeekOrigin.Begin);
        stream.CopyTo(fileStream);
    }

    #endregion

}
