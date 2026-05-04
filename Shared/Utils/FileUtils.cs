using System.Runtime.ExceptionServices;

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
        using FileStream fileStream = OpenCreate(PathUtils.WithLongPath(filePath));
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
            ExceptionDispatchInfo? internalEx = null; // 捕获内部异常
            var thread = new Thread(() => { try { Run(target); } catch (Exception ex) { internalEx = ExceptionDispatchInfo.Capture(ex); } });
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
    public static FileStream OpenRead(string filePath) {
        return new(PathUtils.WithLongPath(filePath), FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
    }

    /// <summary>
    /// 在指定路径创建文件，并打开 <see cref="FileStream"/>。
    /// </summary>
    public static FileStream OpenCreate(string filePath) {
        DirectoryUtils.Create(filePath, isFilePath: true);
        return new(PathUtils.WithLongPath(filePath), FileMode.Create);
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
