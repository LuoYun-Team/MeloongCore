namespace MeloongCore.Tests;
public class DirectoryUtilsTest : TestWithFolder {

    #region 复制

    [Test]
    public async Task 复制_自身到自身() {
        var src = Path.Combine(tempFolder, "SelfCopy");
        DirectoryUtils.Create(src);
        FileUtils.Write(Path.Combine(src, "file.txt"), "content");

        DirectoryUtils.Copy(src, src);

        await Assert.That(DirectoryUtils.Exists(src)).IsTrue();
        await Assert.That(FileUtils.Exists(Path.Combine(src, "file.txt"))).IsTrue();
    }

    [Test]
    public async Task 复制_大小写不同() {
        // 建立源文件夹（全大写）
        var src = Path.Combine(tempFolder, "CASECOPY");
        DirectoryUtils.Create(src);
        FileUtils.Write(Path.Combine(src, "file.txt"), "content");

        // 目标路径仅大小写不同，Copy 应等效于重命名
        var dest = Path.Combine(tempFolder, "casecopy");
        DirectoryUtils.Copy(src, dest);

        await Assert.That(FileUtils.Exists(Path.Combine(dest, "file.txt"))).IsTrue();
    }

    [Test]
    public async Task 复制_不同路径() {
        var src = Path.Combine(tempFolder, "CopySource");
        DirectoryUtils.Create(Path.Combine(src, "sub"));
        FileUtils.Write(Path.Combine(src, "a.txt"), "aaa");
        FileUtils.Write(Path.Combine(src, "sub", "b.txt"), "bbb");

        var dest = Path.Combine(tempFolder, "CopyDest");
        DirectoryUtils.Copy(src, dest);

        // 源文件夹和文件仍存在
        await Assert.That(DirectoryUtils.Exists(src)).IsTrue();
        await Assert.That(FileUtils.Exists(Path.Combine(src, "a.txt"))).IsTrue();
        // 目标文件夹的文件已正确复制
        await Assert.That(FileUtils.Exists(Path.Combine(dest, "a.txt"))).IsTrue();
        await Assert.That(FileUtils.Exists(Path.Combine(dest, "sub", "b.txt"))).IsTrue();
        await Assert.That(FileUtils.ReadAsString(Path.Combine(dest, "a.txt"))).IsEqualTo("aaa");
    }

    #endregion

    #region 剪切

    [Test]
    public async Task 剪切_自身到自身() {
        var src = Path.Combine(tempFolder, "SelfMove");
        DirectoryUtils.Create(src);
        FileUtils.Write(Path.Combine(src, "file.txt"), "content");

        DirectoryUtils.Move(src, src);

        await Assert.That(DirectoryUtils.Exists(src)).IsTrue();
        await Assert.That(FileUtils.Exists(Path.Combine(src, "file.txt"))).IsTrue();
    }

    [Test]
    public async Task 剪切_大小写不同() {
        var src = Path.Combine(tempFolder, "CASEMOVE");
        DirectoryUtils.Create(src);
        FileUtils.Write(Path.Combine(src, "file.txt"), "content");

        // 目标路径仅大小写不同，Move 应借助临时目录完成重命名
        var dest = Path.Combine(tempFolder, "casemove");
        DirectoryUtils.Move(src, dest);

        await Assert.That(FileUtils.Exists(Path.Combine(dest, "file.txt"))).IsTrue();
    }

    [Test]
    public async Task 剪切_同一磁盘() {
        var src = Path.Combine(tempFolder, "MoveSrc");
        DirectoryUtils.Create(Path.Combine(src, "sub"));
        FileUtils.Write(Path.Combine(src, "a.txt"), "aaa");
        FileUtils.Write(Path.Combine(src, "sub", "b.txt"), "bbb");

        var dest = Path.Combine(tempFolder, "MoveDest");
        DirectoryUtils.Move(src, dest);

        await Assert.That(DirectoryUtils.Exists(src)).IsFalse();
        await Assert.That(FileUtils.Exists(Path.Combine(dest, "a.txt"))).IsTrue();
        await Assert.That(FileUtils.Exists(Path.Combine(dest, "sub", "b.txt"))).IsTrue();
        await Assert.That(FileUtils.ReadAsString(Path.Combine(dest, "a.txt"))).IsEqualTo("aaa");
    }

    [Test]
    public async Task 剪切_不同磁盘() {
        // 找到两个不同的就绪固定磁盘；若只有一个则跳过此分支
        var drives = DriveInfo.GetDrives()
            .Where(d => d.IsReady && d.DriveType == DriveType.Fixed)
            .ToArray();
        if (drives.Length < 2) return;

        var id = Guid.NewGuid().ToString("N");
        var srcRoot = Path.Combine(drives[0].RootDirectory.FullName, "Temp", $"MeloongTest_{id}");
        var destRoot = Path.Combine(drives[1].RootDirectory.FullName, "Temp", $"MeloongTest_{id}");
        var src = Path.Combine(srcRoot, "src");
        var dest = Path.Combine(destRoot, "dest");
        try {
            DirectoryUtils.Create(src);
            FileUtils.Write(Path.Combine(src, "a.txt"), "aaa");

            DirectoryUtils.Move(src, dest);

            await Assert.That(DirectoryUtils.Exists(src)).IsFalse();
            await Assert.That(FileUtils.Exists(Path.Combine(dest, "a.txt"))).IsTrue();
            await Assert.That(FileUtils.ReadAsString(Path.Combine(dest, "a.txt"))).IsEqualTo("aaa");
        } finally {
            DirectoryUtils.Delete(srcRoot);
            DirectoryUtils.Delete(destRoot);
        }
    }

    [Test]
    public void 剪切_CurrentFolder_安全检查() {
        var dest = Path.Combine(tempFolder, "SafetyCheckDest");
        Assert.Throws<UnauthorizedAccessException>(() => DirectoryUtils.Move(PathUtils.CurrentFolder, dest));
    }

    #endregion

    #region 删除

    [Test]
    public async Task 删除_子文件夹中带孤立符号链接() {
        // 创建一个目标文件夹，供符号链接指向
        var target = Path.Combine(tempFolder, "SymlinkTarget");
        DirectoryUtils.Create(target);

        // 创建父文件夹及其子文件夹
        var parent = Path.Combine(tempFolder, "SymlinkParent");
        var child = Path.Combine(parent, "child");
        DirectoryUtils.Create(child);

        // 在子文件夹中建立指向目标的目录链接；若无权限则跳过
        var link = Path.Combine(child, "link");
        if (!TryCreateDirectoryLink(link, target)) return;
        await Assert.That(DirectoryUtils.Exists(link)).IsTrue();

        // 删除目标，使链接成为孤立链接
        Directory.Delete(PathUtils.ForApi(target));
        await Assert.That(DirectoryUtils.Exists(target)).IsFalse();

        // 删除含有孤立链接的父文件夹，不应抛出异常
        DirectoryUtils.Delete(parent);
        await Assert.That(DirectoryUtils.Exists(parent)).IsFalse();
    }

    /// <summary>
    /// 创建指向目标文件夹的目录链接（通过 P/Invoke，不经过 Shell，避免路径中特殊字符的问题）。
    /// Windows 下使用目录符号链接（需开发者模式）；Linux/macOS 下使用符号链接。
    /// 若当前环境无权创建符号链接，则跳过测试（返回 <see langword="false"/>）。
    /// </summary>
    private static bool TryCreateDirectoryLink(string link, string target) {
        if (Environment.OSVersion.Platform == PlatformID.Win32NT) {
            // SYMBOLIC_LINK_FLAG_DIRECTORY (1) | SYMBOLIC_LINK_FLAG_ALLOW_UNPRIVILEGED_CREATE (2)
            bool ok = NativeMethods.CreateSymbolicLink(link, target, 3u);
            if (!ok) {
                int err = Marshal.GetLastWin32Error();
                // ERROR_PRIVILEGE_NOT_HELD (1314) 或 ERROR_ACCESS_DENIED (5)：无权限，跳过
                if (err == 1314 || err == 5) return false;
                throw new InvalidOperationException($"CreateSymbolicLink 失败，Win32 错误码：{err}");
            }
            return true;
        } else {
            int ret = NativeMethods.Symlink(target, link);
            if (ret != 0) throw new InvalidOperationException($"symlink 失败，errno：{Marshal.GetLastWin32Error()}");
            return true;
        }
    }

    private static class NativeMethods {
        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode, EntryPoint = "CreateSymbolicLinkW")]
        [return: MarshalAs(UnmanagedType.I1)]
        internal static extern bool CreateSymbolicLink(string lpSymlinkFileName, string lpTargetFileName, uint dwFlags);

        [DllImport("libc", SetLastError = true, EntryPoint = "symlink")]
        internal static extern int Symlink(string target, string linkpath);
    }

    #endregion

}
