using System.IO.Compression;

namespace MeloongCore;
public static class FileUtils {

    #region 写入

    /// <summary>
    /// 写入文件。
    /// 如果文件或文件夹不存在，则会自动创建。若已存在，则会覆盖原文件。
    /// </summary>
    public static void Write(string filePath, string text, Encoding? encoding = null) {
        DirectoryUtils.Create(filePath, isFilePath: true);
        File.WriteAllText(PathUtils.WithLongPath(filePath), text, encoding ?? new UTF8Encoding());
    }

    /// <summary>
    /// 写入文件。
    /// 如果文件或文件夹不存在，则会自动创建。若已存在，则会覆盖原文件。
    /// </summary>
    public static void Write(string filePath, byte[] content) {
        DirectoryUtils.Create(filePath, isFilePath: true);
        File.WriteAllBytes(PathUtils.WithLongPath(filePath), content);
    }

    /// <summary>
    /// 写入文件。
    /// 如果文件或文件夹不存在，则会自动创建。若已存在，则会覆盖原文件。
    /// </summary>
    public static void Write(string filePath, IEnumerable<byte> content) {
        DirectoryUtils.Create(filePath, isFilePath: true);
        File.WriteAllBytes(PathUtils.WithLongPath(filePath), [..content]);
    }

    /// <summary>
    /// 将 <paramref name="stream" /> 写入文件。
    /// 会将流的位置主动重置到开头。
    /// 如果文件或文件夹不存在，则会自动创建。若已存在，则会覆盖原文件。
    /// </summary>
    public static void Write(string filePath, Stream stream) {
        using FileStream fileStream = CreateAsStream(PathUtils.WithLongPath(filePath));
        if (stream.CanSeek && stream.Position != 0) stream.Seek(0, SeekOrigin.Begin);
        stream.CopyTo(fileStream);
    }

    #endregion

    #region 删除

    /// <summary>
    /// 删除文件。
    /// </summary>
    public static void Delete(string filePath, bool toRecycleBin = false) {
        if (toRecycleBin) {
            if (Exists(filePath)) DeleteToRecycleBin(filePath);
        } else {
            File.Delete(PathUtils.WithLongPath(filePath));
        }
    }

    /// <summary>
    /// 将文件或文件夹删除到回收站。
    /// </summary>
    internal static void DeleteToRecycleBin(string target) {
        // 实际的删除方法
        static void Run(string filePath) {
            IShellItem? item = null;
            IFileOperation? op = null;
            try {
                var iid = typeof(IShellItem).GUID;
                Marshal.ThrowExceptionForHR(SHCreateItemFromParsingName(PathUtils.WithoutLongPath(filePath), IntPtr.Zero, ref iid, out item));
                op = (IFileOperation) new FileOperation();
                op.SetOperationFlags(0x0040 | 0x0010 | 0x0004); // FOF_ALLOWUNDO | FOF_NOCONFIRMATION | FOF_SILENT
                op.DeleteItem(item, IntPtr.Zero);
                op.PerformOperations();
                op.GetAnyOperationsAborted(out bool aborted);
                if (aborted) throw new OperationCanceledException("Delete operation was aborted.");
            } finally {
                if (op != null && Marshal.IsComObject(op)) Marshal.FinalReleaseComObject(op);
                if (item != null && Marshal.IsComObject(item)) Marshal.FinalReleaseComObject(item);
            }
        }
        // 在 STA 线程中执行删除方法
        if (Thread.CurrentThread.GetApartmentState() == ApartmentState.STA) {
            Run(target);
        } else {
            System.Runtime.ExceptionServices.ExceptionDispatchInfo? internalEx = null; // 捕获内部异常
            var thread = new Thread(() => { try { Run(target); } catch (Exception ex) { internalEx = System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(ex); } });
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();
            internalEx?.Throw();
        }
    }
    // 用于删除到回收站的接口
    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    static extern int SHCreateItemFromParsingName([MarshalAs(UnmanagedType.LPWStr)] string pszPath, IntPtr pbc, ref Guid riid, [MarshalAs(UnmanagedType.Interface)] out IShellItem ppv);
    [ComImport, Guid("43826D1E-E718-42EE-BC55-A1E261C37BFE"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IShellItem { }
    [ComImport, Guid("3AD05575-8857-4850-9277-11B85BDB8E09")]
    class FileOperation { }
    [ComImport, Guid("947AAB5F-0A5C-4C13-B4D6-4BF7836FC9F8"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IFileOperation {
        void Advise(IntPtr pfops, out uint pdwCookie);
        void Unadvise(uint dwCookie);
        void SetOperationFlags(uint dwOperationFlags);
        void SetProgressMessage([MarshalAs(UnmanagedType.LPWStr)] string pszMessage);
        void SetProgressDialog(IntPtr popd);
        void SetProperties(IntPtr pproparray);
        void SetOwnerWindow(IntPtr hwndOwner);
        void ApplyPropertiesToItem(IShellItem psiItem);
        void ApplyPropertiesToItems(IntPtr punkItems);
        void RenameItem(IShellItem psiItem, [MarshalAs(UnmanagedType.LPWStr)] string pszNewName, IntPtr pfopsItem);
        void RenameItems(IntPtr pUnkItems, [MarshalAs(UnmanagedType.LPWStr)] string pszNewName);
        void MoveItem(IShellItem psiItem, IShellItem psiDestinationFolder, [MarshalAs(UnmanagedType.LPWStr)] string pszNewName, IntPtr pfopsItem);
        void MoveItems(IntPtr punkItems, IShellItem psiDestinationFolder);
        void CopyItem(IShellItem psiItem, IShellItem psiDestinationFolder, [MarshalAs(UnmanagedType.LPWStr)] string pszCopyName, IntPtr pfopsItem);
        void CopyItems(IntPtr punkItems, IShellItem psiDestinationFolder);
        void DeleteItem(IShellItem psiItem, IntPtr pfopsItem);
        void DeleteItems(IntPtr punkItems);
        void NewItem(IShellItem psiDestinationFolder, uint dwFileAttributes, [MarshalAs(UnmanagedType.LPWStr)] string pszName, [MarshalAs(UnmanagedType.LPWStr)] string pszTemplateName, IntPtr pfopsItem);
        void PerformOperations();
        void GetAnyOperationsAborted([MarshalAs(UnmanagedType.Bool)] out bool pfAnyOperationsAborted);
    }

    #endregion

    #region FileStream

    /// <summary>
    /// 打开该文件的只读 <see cref="FileStream"/>。
    /// </summary>
    public static FileStream ReadAsStream(string filePath) {
        return new(PathUtils.WithLongPath(filePath), FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
    }

    /// <summary>
    /// 在指定路径创建文件，并打开 <see cref="FileStream"/>。
    /// </summary>
    public static FileStream CreateAsStream(string filePath) {
        DirectoryUtils.Create(filePath, isFilePath: true);
        return new(PathUtils.WithLongPath(filePath), FileMode.Create);
    }

    #endregion

    #region 压缩包

    /// <summary>
    /// 尝试根据后缀名判断文件种类并解压文件，支持 gz 与 zip，会尝试将 jar 以 zip 方式解压。
    /// 会自动创建文件夹。会覆盖已有文件，但不会删除多余文件。
    /// 解压时会先尝试 UTF8 编码，失败后换用 GB18030。
    /// </summary>
    public static void ExtractToDirectory(string compressionFile, string outputDirectory, Action<double>? progressIncrementHandler = null) {
        compressionFile = PathUtils.WithLongPath(compressionFile);
        DirectoryUtils.Create(outputDirectory);
        // 解压 gz（gz 不需要考虑编码）
        if (compressionFile.EndsWithF(".gz", true)) {
            using var stream = new GZipStream(FileUtils.ReadAsStream(compressionFile), CompressionMode.Decompress);
            FileUtils.Write(Path.Combine(outputDirectory, Path.GetFileNameWithoutExtension(compressionFile)), stream);
            return;
        }
        // 解压 zip
        void ExtractWithEncoding(Encoding encoding) {
            using var archive = ZipFile.Open(compressionFile, ZipArchiveMode.Read, encoding);
            int entryCount = archive.Entries.Count;
            foreach (var entry in archive.Entries) {
                if (progressIncrementHandler != null && entryCount > 0) progressIncrementHandler(1.0 / entryCount);
                if (string.IsNullOrEmpty(entry.Name)) continue; // 跳过文件夹条目（ZipArchive 会将文件夹也作为一个 entry，但它们的 Name 为空）
                // ZipSlip 修复
                string outputFilePath = Path.GetFullPath(Path.Combine(outputDirectory, entry.FullName));
                if (!outputFilePath.StartsWithF(PathUtils.WithSeparator(Path.GetFullPath(outputDirectory)))) 
                    throw new UnauthorizedAccessException($"Zip 文件项 {entry.FullName} 的路径在压缩包之外，这可能导致安全问题");
                // 实际的解压
                using var entryStream = entry.Open();
                FileUtils.Write(outputFilePath, entryStream);
            }
        }
        try { // 尝试两种编码
            ExtractWithEncoding(new UTF8Encoding(false, true));
        } catch (InvalidDataException) {
            ExtractWithEncoding(Encoding.GetEncoding("GB18030"));
        }
    }

    /// <summary>
    /// 将指定文件夹的内容打包为一个 zip 压缩包。
    /// 会自动创建文件夹。会覆盖已有文件。
    /// </summary>
    public static void CreateZipFromDirectory(string outputFullPath, string sourceDirectory) {
        outputFullPath = PathUtils.WithLongPath(outputFullPath);
        sourceDirectory = PathUtils.WithLongPath(sourceDirectory);
        DirectoryUtils.Create(outputFullPath, isFilePath: true);
        FileUtils.Delete(outputFullPath);
        ZipFile.CreateFromDirectory(sourceDirectory, outputFullPath);
    }

    /// <summary>
    /// 将多个文件打包为一个压缩文件，所有文件都会被放在压缩文件的根目录。
    /// 会自动创建文件夹。会覆盖已有文件。
    /// </summary>
    public static void CreateZipFromFiles(string outputFullPath, params string[] sourceFiles) {
        outputFullPath = PathUtils.WithLongPath(outputFullPath);
        sourceFiles = sourceFiles.Select(PathUtils.WithLongPath).ToArray();
        DirectoryUtils.Create(outputFullPath, isFilePath: true);
        FileUtils.Delete(outputFullPath);
        using var archive = ZipFile.Open(outputFullPath, ZipArchiveMode.Create);
        foreach (var source in sourceFiles) {
            if (!FileUtils.Exists(source)) throw new FileNotFoundException($"未找到需要被压缩的文件（{source})", source);
            using var sourceStream = FileUtils.ReadAsStream(source);
            using var entryStream = archive.CreateEntry(Path.GetFileName(source), CompressionLevel.Optimal).Open();
            sourceStream.CopyTo(entryStream);
        }
    }

    #endregion

    /// <summary>
    /// 确定指定的文件是否存在。
    /// </summary>
    public static bool Exists(string filePath) {
        return File.Exists(PathUtils.WithLongPath(filePath));
    }

    /// <summary>
    /// 创建 <see cref="FileInfo"/> 对象。
    /// </summary>
    public static FileInfo GetInfo(string path) {
        return new(PathUtils.WithLongPath(path));
    }

    /// <summary>
    /// 复制文件。
    /// 会创建对应文件夹、覆盖已有的文件。
    /// </summary>
    public static void Copy(string sourceFilePath, string destFilePath) {
        if (sourceFilePath == destFilePath) return; // 如果复制同一个文件则跳过
        DirectoryUtils.Create(destFilePath, isFilePath: true);
        File.Copy(PathUtils.WithLongPath(sourceFilePath), PathUtils.WithLongPath(destFilePath), true);
    }

    /// <summary>
    /// 剪切文件。
    /// 会创建对应文件夹、覆盖已有的文件。
    /// </summary>
    public static void Move(string sourceFilePath, string destFilePath) {
        if (sourceFilePath == destFilePath) return; // 如果移动同一个文件则跳过
        DirectoryUtils.Create(destFilePath, isFilePath: true);
        File.Move(PathUtils.WithLongPath(sourceFilePath), PathUtils.WithLongPath(destFilePath));
    }

}
