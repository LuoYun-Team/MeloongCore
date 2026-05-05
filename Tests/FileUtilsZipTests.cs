using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Text;
using MeloongCore;
using Xunit;

namespace MeloongCore.Tests;

/// <summary>
/// 仅在 Windows 上运行的 xUnit Fact 属性。
/// FileUtils 内部使用 PathUtils.WithLongPath 添加 \\?\ 长路径前缀，该行为仅在 Windows 上有效。
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class WindowsOnlyFactAttribute : FactAttribute {
    public WindowsOnlyFactAttribute() {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            Skip = "FileUtils 使用 Windows 专用的长路径前缀（\\\\?\\），此测试仅在 Windows 上运行。";
    }
}

/// <summary>
/// FileUtils 压缩包相关方法的单元测试。
/// 覆盖 OpenZip、ExtractToDirectory、CreateZipFromDirectory 和 CreateZipFromFiles。
/// </summary>
public class FileUtilsZipTests : IDisposable {

    private readonly string _tempDir;

    public FileUtilsZipTests() {
        _tempDir = Path.Combine(Path.GetTempPath(), "MeloongCoreTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose() {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    // ──────────────────────────────────────────────────────────────────────────
    #region 辅助方法

    /// <summary>创建一个包含若干文件的临时文件夹，并返回该文件夹路径。</summary>
    private string CreateSourceDirectory(params (string relativePath, string content)[] files) {
        string dir = Path.Combine(_tempDir, "src_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        foreach (var (relativePath, content) in files) {
            string fullPath = Path.Combine(dir, relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
            File.WriteAllText(fullPath, content, Encoding.UTF8);
        }
        return dir;
    }

    /// <summary>创建一个只含 ASCII 文件名的合法 ZIP，使用指定编码写入文件名。</summary>
    private string CreateZipWithEncoding(string zipName, Encoding encoding, params (string entryName, string content)[] entries) {
        string zipPath = Path.Combine(_tempDir, zipName);
        using var stream = File.Create(zipPath);
        using var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: false, encoding);
        foreach (var (entryName, content) in entries) {
            var entry = archive.CreateEntry(entryName);
            using var writer = new StreamWriter(entry.Open(), Encoding.UTF8);
            writer.Write(content);
        }
        return zipPath;
    }

    /// <summary>创建一个用于 GZip 解压测试的 .gz 文件。</summary>
    private string CreateGzFile(string innerFileName, string content) {
        string gzPath = Path.Combine(_tempDir, innerFileName + ".gz");
        byte[] contentBytes = Encoding.UTF8.GetBytes(content);
        using var fileStream = File.Create(gzPath);
        using var gzStream = new GZipStream(fileStream, CompressionMode.Compress);
        gzStream.Write(contentBytes, 0, contentBytes.Length);
        return gzPath;
    }

    /// <summary>读取文件内容（兼容长路径）。</summary>
    private static string ReadFile(string path) => File.ReadAllText(path, Encoding.UTF8);

    #endregion

    // ──────────────────────────────────────────────────────────────────────────
    #region OpenZip 测试

    [WindowsOnlyFact]
    public void OpenZip_ValidUtf8Zip_OpensSuccessfully() {
        string zipPath = CreateZipWithEncoding("utf8.zip", new UTF8Encoding(false, true),
            ("hello.txt", "world"));

        using var archive = FileUtils.OpenZip(zipPath);

        Assert.Single(archive.Entries);
        Assert.Equal("hello.txt", archive.Entries[0].FullName);
    }

    [WindowsOnlyFact]
    public void OpenZip_ValidUtf8Zip_EntryContentIsCorrect() {
        const string expectedContent = "测试内容";
        string zipPath = CreateZipWithEncoding("utf8content.zip", new UTF8Encoding(false, true),
            ("data.txt", expectedContent));

        using var archive = FileUtils.OpenZip(zipPath);
        using var entryStream = archive.Entries[0].Open();
        using var reader = new StreamReader(entryStream, Encoding.UTF8);

        Assert.Equal(expectedContent, reader.ReadToEnd());
    }

    [WindowsOnlyFact]
    public void OpenZip_MultipleEntries_AllEntriesAccessible() {
        string zipPath = CreateZipWithEncoding("multi.zip", new UTF8Encoding(false, true),
            ("a.txt", "aaa"),
            ("b.txt", "bbb"),
            ("c.txt", "ccc"));

        using var archive = FileUtils.OpenZip(zipPath);

        Assert.Equal(3, archive.Entries.Count);
    }

    [WindowsOnlyFact]
    public void OpenZip_InvalidFile_ThrowsInvalidDataException() {
        string invalidPath = Path.Combine(_tempDir, "invalid.zip");
        File.WriteAllBytes(invalidPath, [0x00, 0x01, 0x02, 0x03]); // 非 zip 数据

        Assert.Throws<InvalidDataException>(() => FileUtils.OpenZip(invalidPath));
    }

    [WindowsOnlyFact]
    public void OpenZip_EmptyZip_ReturnsEmptyEntries() {
        string zipPath = CreateZipWithEncoding("empty.zip", new UTF8Encoding(false, true));

        using var archive = FileUtils.OpenZip(zipPath);

        Assert.Empty(archive.Entries);
    }

    #endregion

    // ──────────────────────────────────────────────────────────────────────────
    #region ExtractToDirectory 测试

    [WindowsOnlyFact]
    public void ExtractToDirectory_ZipWithSingleFile_ExtractsCorrectly() {
        const string fileContent = "hello extract";
        string zipPath = CreateZipWithEncoding("single.zip", new UTF8Encoding(false, true),
            ("file.txt", fileContent));
        string outDir = Path.Combine(_tempDir, "out_single");

        FileUtils.ExtractToDirectory(zipPath, outDir);

        string extractedFile = Path.Combine(outDir, "file.txt");
        Assert.True(File.Exists(extractedFile));
        Assert.Equal(fileContent, ReadFile(extractedFile));
    }

    [WindowsOnlyFact]
    public void ExtractToDirectory_ZipWithMultipleFiles_AllExtracted() {
        string zipPath = CreateZipWithEncoding("multi_extract.zip", new UTF8Encoding(false, true),
            ("a.txt", "aaa"),
            ("b.txt", "bbb"));
        string outDir = Path.Combine(_tempDir, "out_multi");

        FileUtils.ExtractToDirectory(zipPath, outDir);

        Assert.True(File.Exists(Path.Combine(outDir, "a.txt")));
        Assert.True(File.Exists(Path.Combine(outDir, "b.txt")));
    }

    [WindowsOnlyFact]
    public void ExtractToDirectory_ZipWithSubdirectory_DirectoryStructurePreserved() {
        string zipPath = CreateZipWithEncoding("subdir.zip", new UTF8Encoding(false, true),
            ("subdir/nested.txt", "nested content"));
        string outDir = Path.Combine(_tempDir, "out_subdir");

        FileUtils.ExtractToDirectory(zipPath, outDir);

        string nestedFile = Path.Combine(outDir, "subdir", "nested.txt");
        Assert.True(File.Exists(nestedFile));
        Assert.Equal("nested content", ReadFile(nestedFile));
    }

    [WindowsOnlyFact]
    public void ExtractToDirectory_OutputDirectoryAutoCreated() {
        string zipPath = CreateZipWithEncoding("autocreate.zip", new UTF8Encoding(false, true),
            ("f.txt", "x"));
        string outDir = Path.Combine(_tempDir, "nonexistent", "deep", "dir");

        FileUtils.ExtractToDirectory(zipPath, outDir);

        Assert.True(Directory.Exists(outDir));
    }

    [WindowsOnlyFact]
    public void ExtractToDirectory_ProgressHandler_CalledForEachEntry() {
        string zipPath = CreateZipWithEncoding("progress.zip", new UTF8Encoding(false, true),
            ("a.txt", "a"),
            ("b.txt", "b"),
            ("c.txt", "c"));
        string outDir = Path.Combine(_tempDir, "out_progress");

        var progressValues = new List<double>();
        FileUtils.ExtractToDirectory(zipPath, outDir, progressValues.Add);

        Assert.Equal(3, progressValues.Count);
        // 进度值应单调递增，最后一个值为 1.0
        for (int i = 1; i < progressValues.Count; i++)
            Assert.True(progressValues[i] > progressValues[i - 1]);
        Assert.Equal(1.0, progressValues[^1], precision: 5);
    }

    [WindowsOnlyFact]
    public void ExtractToDirectory_ProgressHandler_Null_DoesNotThrow() {
        string zipPath = CreateZipWithEncoding("noprogress.zip", new UTF8Encoding(false, true),
            ("f.txt", "data"));
        string outDir = Path.Combine(_tempDir, "out_noprogress");

        var ex = Record.Exception(() => FileUtils.ExtractToDirectory(zipPath, outDir, null));
        Assert.Null(ex);
    }

    [WindowsOnlyFact]
    public void ExtractToDirectory_OverwritesExistingFile() {
        string zipPath = CreateZipWithEncoding("overwrite.zip", new UTF8Encoding(false, true),
            ("f.txt", "new content"));
        string outDir = Path.Combine(_tempDir, "out_overwrite");
        Directory.CreateDirectory(outDir);
        File.WriteAllText(Path.Combine(outDir, "f.txt"), "old content");

        FileUtils.ExtractToDirectory(zipPath, outDir);

        Assert.Equal("new content", ReadFile(Path.Combine(outDir, "f.txt")));
    }

    [WindowsOnlyFact]
    public void ExtractToDirectory_ZipSlip_ThrowsUnauthorizedAccessException() {
        // 手动构造一个包含路径穿越条目（../../evil.txt）的 zip
        string zipPath = Path.Combine(_tempDir, "zipslip.zip");
        using (var stream = File.Create(zipPath))
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: false, new UTF8Encoding(false, true))) {
            var entry = archive.CreateEntry("../../evil.txt");
            using var writer = new StreamWriter(entry.Open());
            writer.Write("pwned");
        }
        string outDir = Path.Combine(_tempDir, "out_zipslip");
        Directory.CreateDirectory(outDir);

        Assert.Throws<UnauthorizedAccessException>(() => FileUtils.ExtractToDirectory(zipPath, outDir));
    }

    [WindowsOnlyFact]
    public void ExtractToDirectory_GzFile_ExtractsInnerFile() {
        const string innerContent = "gzip content";
        const string innerFileName = "inner.txt";
        string gzPath = CreateGzFile(innerFileName, innerContent);
        string outDir = Path.Combine(_tempDir, "out_gz");

        FileUtils.ExtractToDirectory(gzPath, outDir);

        string extractedFile = Path.Combine(outDir, innerFileName);
        Assert.True(File.Exists(extractedFile));
        Assert.Equal(innerContent, ReadFile(extractedFile));
    }

    [WindowsOnlyFact]
    public void ExtractToDirectory_JarFile_ExtractsAsZip() {
        // jar 文件实际上是 zip 格式
        string zipPath = CreateZipWithEncoding("app.jar", new UTF8Encoding(false, true),
            ("META-INF/MANIFEST.MF", "Manifest-Version: 1.0\r\n"));
        string outDir = Path.Combine(_tempDir, "out_jar");

        FileUtils.ExtractToDirectory(zipPath, outDir);

        Assert.True(File.Exists(Path.Combine(outDir, "META-INF", "MANIFEST.MF")));
    }

    #endregion

    // ──────────────────────────────────────────────────────────────────────────
    #region CreateZipFromDirectory 测试

    [WindowsOnlyFact]
    public void CreateZipFromDirectory_SingleFile_ZipContainsFile() {
        string srcDir = CreateSourceDirectory(("hello.txt", "hello world"));
        string outZip = Path.Combine(_tempDir, "from_dir.zip");

        FileUtils.CreateZipFromDirectory(outZip, srcDir);

        Assert.True(File.Exists(outZip));
        using var archive = ZipFile.OpenRead(outZip);
        Assert.Single(archive.Entries);
        Assert.Equal("hello.txt", archive.Entries[0].Name);
    }

    [WindowsOnlyFact]
    public void CreateZipFromDirectory_MultipleFiles_AllFilesIncluded() {
        string srcDir = CreateSourceDirectory(
            ("a.txt", "aaa"),
            ("b.txt", "bbb"),
            ("c.txt", "ccc"));
        string outZip = Path.Combine(_tempDir, "from_dir_multi.zip");

        FileUtils.CreateZipFromDirectory(outZip, srcDir);

        using var archive = ZipFile.OpenRead(outZip);
        Assert.Equal(3, archive.Entries.Count);
    }

    [WindowsOnlyFact]
    public void CreateZipFromDirectory_SubdirectoryFiles_StructurePreserved() {
        string srcDir = CreateSourceDirectory(
            ("sub/file.txt", "nested"),
            ("root.txt", "root"));
        string outZip = Path.Combine(_tempDir, "from_dir_sub.zip");

        FileUtils.CreateZipFromDirectory(outZip, srcDir);

        using var archive = ZipFile.OpenRead(outZip);
        var names = archive.Entries.Select(e => e.FullName.Replace('\\', '/')).ToList();
        Assert.Contains(names, n => n.Contains("sub/file.txt"));
        Assert.Contains(names, n => n.Contains("root.txt"));
    }

    [WindowsOnlyFact]
    public void CreateZipFromDirectory_OverwritesExistingZip() {
        string srcDir = CreateSourceDirectory(("new.txt", "new content"));
        string outZip = Path.Combine(_tempDir, "overwrite_dir.zip");

        // 先创建一个旧 zip
        string oldSrcDir = CreateSourceDirectory(("old.txt", "old content"));
        FileUtils.CreateZipFromDirectory(outZip, oldSrcDir);

        // 再用新目录覆盖
        FileUtils.CreateZipFromDirectory(outZip, srcDir);

        using var archive = ZipFile.OpenRead(outZip);
        Assert.Single(archive.Entries);
        Assert.Equal("new.txt", archive.Entries[0].Name);
    }

    [WindowsOnlyFact]
    public void CreateZipFromDirectory_OutputDirectoryAutoCreated() {
        string srcDir = CreateSourceDirectory(("f.txt", "x"));
        string outZip = Path.Combine(_tempDir, "newdir", "nested.zip");

        FileUtils.CreateZipFromDirectory(outZip, srcDir);

        Assert.True(File.Exists(outZip));
    }

    #endregion

    // ──────────────────────────────────────────────────────────────────────────
    #region CreateZipFromFiles（params 版）测试

    [WindowsOnlyFact]
    public void CreateZipFromFiles_Params_SingleFile_ZipContainsFile() {
        string srcFile = Path.Combine(_tempDir, "single.txt");
        File.WriteAllText(srcFile, "content");
        string outZip = Path.Combine(_tempDir, "from_files_single.zip");

        FileUtils.CreateZipFromFiles(outZip, srcFile);

        using var archive = ZipFile.OpenRead(outZip);
        Assert.Single(archive.Entries);
        Assert.Equal("single.txt", archive.Entries[0].Name);
    }

    [WindowsOnlyFact]
    public void CreateZipFromFiles_Params_MultipleFiles_AllIncluded() {
        string file1 = Path.Combine(_tempDir, "f1.txt");
        string file2 = Path.Combine(_tempDir, "f2.txt");
        File.WriteAllText(file1, "1");
        File.WriteAllText(file2, "2");
        string outZip = Path.Combine(_tempDir, "from_files_multi.zip");

        FileUtils.CreateZipFromFiles(outZip, file1, file2);

        using var archive = ZipFile.OpenRead(outZip);
        Assert.Equal(2, archive.Entries.Count);
        var names = archive.Entries.Select(e => e.Name).ToHashSet();
        Assert.Contains("f1.txt", names);
        Assert.Contains("f2.txt", names);
    }

    [WindowsOnlyFact]
    public void CreateZipFromFiles_Params_DuplicateFileNames_ThrowsArgumentException() {
        string file1 = Path.Combine(_tempDir, "dup", "a", "same.txt");
        string file2 = Path.Combine(_tempDir, "dup", "b", "same.txt");
        Directory.CreateDirectory(Path.GetDirectoryName(file1)!);
        Directory.CreateDirectory(Path.GetDirectoryName(file2)!);
        File.WriteAllText(file1, "1");
        File.WriteAllText(file2, "2");
        string outZip = Path.Combine(_tempDir, "dup_names.zip");

        Assert.Throws<ArgumentException>(() => FileUtils.CreateZipFromFiles(outZip, file1, file2));
    }

    [WindowsOnlyFact]
    public void CreateZipFromFiles_Params_OverwritesExistingZip() {
        string oldFile = Path.Combine(_tempDir, "old.txt");
        string newFile = Path.Combine(_tempDir, "new.txt");
        File.WriteAllText(oldFile, "old");
        File.WriteAllText(newFile, "new");
        string outZip = Path.Combine(_tempDir, "overwrite_files.zip");

        FileUtils.CreateZipFromFiles(outZip, oldFile);
        FileUtils.CreateZipFromFiles(outZip, newFile);

        using var archive = ZipFile.OpenRead(outZip);
        Assert.Single(archive.Entries);
        Assert.Equal("new.txt", archive.Entries[0].Name);
    }

    #endregion

    // ──────────────────────────────────────────────────────────────────────────
    #region CreateZipFromFiles（IDictionary 版）测试

    [WindowsOnlyFact]
    public void CreateZipFromFiles_Dict_CustomEntryPath_EntryNameMatchesDictKey() {
        string srcFile = Path.Combine(_tempDir, "source.txt");
        File.WriteAllText(srcFile, "data");
        string outZip = Path.Combine(_tempDir, "from_dict.zip");
        var sources = new Dictionary<string, string> {
            ["custom/path/file.txt"] = srcFile
        };

        FileUtils.CreateZipFromFiles(outZip, sources);

        using var archive = ZipFile.OpenRead(outZip);
        Assert.Single(archive.Entries);
        Assert.Equal("custom/path/file.txt", archive.Entries[0].FullName);
    }

    [WindowsOnlyFact]
    public void CreateZipFromFiles_Dict_MultipleEntries_AllIncluded() {
        string f1 = Path.Combine(_tempDir, "d1.txt");
        string f2 = Path.Combine(_tempDir, "d2.txt");
        File.WriteAllText(f1, "1");
        File.WriteAllText(f2, "2");
        string outZip = Path.Combine(_tempDir, "from_dict_multi.zip");
        var sources = new Dictionary<string, string> {
            ["a/one.txt"] = f1,
            ["b/two.txt"] = f2
        };

        FileUtils.CreateZipFromFiles(outZip, sources);

        using var archive = ZipFile.OpenRead(outZip);
        Assert.Equal(2, archive.Entries.Count);
        var fullNames = archive.Entries.Select(e => e.FullName).ToHashSet();
        Assert.Contains("a/one.txt", fullNames);
        Assert.Contains("b/two.txt", fullNames);
    }

    [WindowsOnlyFact]
    public void CreateZipFromFiles_Dict_FileContentPreserved() {
        const string expectedContent = "preserved content";
        string srcFile = Path.Combine(_tempDir, "preserve.txt");
        File.WriteAllText(srcFile, expectedContent, Encoding.UTF8);
        string outZip = Path.Combine(_tempDir, "from_dict_content.zip");
        var sources = new Dictionary<string, string> { ["entry.txt"] = srcFile };

        FileUtils.CreateZipFromFiles(outZip, sources);

        string outDir = Path.Combine(_tempDir, "dict_content_out");
        Directory.CreateDirectory(outDir);
        using var archive = ZipFile.OpenRead(outZip);
        using var entryStream = archive.Entries[0].Open();
        using var reader = new StreamReader(entryStream, Encoding.UTF8);
        Assert.Equal(expectedContent, reader.ReadToEnd());
    }

    [WindowsOnlyFact]
    public void CreateZipFromFiles_Dict_BackslashInKey_NormalizedToForwardSlash() {
        string srcFile = Path.Combine(_tempDir, "bs.txt");
        File.WriteAllText(srcFile, "x");
        string outZip = Path.Combine(_tempDir, "backslash.zip");
        var sources = new Dictionary<string, string> {
            [@"dir\sub\file.txt"] = srcFile
        };

        FileUtils.CreateZipFromFiles(outZip, sources);

        using var archive = ZipFile.OpenRead(outZip);
        // 条目路径中的反斜杠应被规范化为正斜杠
        Assert.Equal("dir/sub/file.txt", archive.Entries[0].FullName);
    }

    #endregion

}
