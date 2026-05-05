using System.IO.Compression;

namespace MeloongCore.Tests;
public class FileUtilsTest : IDisposable {

    #region 压缩包

    [Fact(DisplayName = nameof(OpenZip))]
    public void OpenZip() {
        // GB 编码
        using var gbArchive = FileUtils.OpenZip(TestUtils.GetTestFile(nameof(FileUtils), "GB Encoding.zip"));
        Assert.Contains(gbArchive.Entries, e => e.Name == "中文文件.txt");
        Assert.Contains(gbArchive.Entries, e => e.Name == "fabricloader.log");
        Assert.DoesNotContain(gbArchive.Entries, e => e.Name == "fabricloader.log2");
        // UTF8 编码
        using var utfArchive = FileUtils.OpenZip(TestUtils.GetTestFile(nameof(FileUtils), "UTF8 Encoding.zip"));
        Assert.Contains(utfArchive.Entries, e => e.Name == "中文文件.txt");
        Assert.Contains(utfArchive.Entries, e => e.Name == "fabricloader.log");
        Assert.DoesNotContain(utfArchive.Entries, e => e.Name == "fabricloader.log2");
        // 损坏的文件
        Assert.Throws<InvalidDataException>(() => FileUtils.OpenZip(TestUtils.GetTestFile(nameof(FileUtils), "Corrupted.zip")));
        // 非压缩文件
        Assert.Throws<InvalidDataException>(() => FileUtils.OpenZip(TestUtils.GetTestFile(nameof(FileUtils), "Not zip.zip")));
    }

    [Fact(DisplayName = nameof(ExtractToDirectory))]
    public void ExtractToDirectory() {





        //const string fileContent = "hello extract";
        //string zipPath = CreateZipWithEncoding("single.zip", new UTF8Encoding(false, true),
        //    ("file.txt", fileContent));
        //string outDir = Path.Combine(_tempDir, "out_single");
        //
        //FileUtils.ExtractToDirectory(zipPath, outDir);
        //
        //string extractedFile = Path.Combine(outDir, "file.txt");
        //Assert.True(File.Exists(extractedFile));
        //Assert.Equal(fileContent, ReadFile(extractedFile));
        //
        //string zipPath = CreateZipWithEncoding("multi_extract.zip", new UTF8Encoding(false, true),
        //    ("a.txt", "aaa"),
        //    ("b.txt", "bbb"));
        //string outDir = Path.Combine(_tempDir, "out_multi");
        //
        //FileUtils.ExtractToDirectory(zipPath, outDir);
        //
        //Assert.True(File.Exists(Path.Combine(outDir, "a.txt")));
        //Assert.True(File.Exists(Path.Combine(outDir, "b.txt")));
        //
        //string zipPath = CreateZipWithEncoding("subdir.zip", new UTF8Encoding(false, true),
        //    ("subdir/nested.txt", "nested content"));
        //string outDir = Path.Combine(_tempDir, "out_subdir");
        //
        //FileUtils.ExtractToDirectory(zipPath, outDir);
        //
        //string nestedFile = Path.Combine(outDir, "subdir", "nested.txt");
        //Assert.True(File.Exists(nestedFile));
        //Assert.Equal("nested content", ReadFile(nestedFile));
        //
        //string zipPath = CreateZipWithEncoding("autocreate.zip", new UTF8Encoding(false, true),
        //    ("f.txt", "x"));
        //string outDir = Path.Combine(_tempDir, "nonexistent", "deep", "dir");
        //
        //FileUtils.ExtractToDirectory(zipPath, outDir);
        //
        //Assert.True(Directory.Exists(outDir));
        //
        //string zipPath = CreateZipWithEncoding("overwrite.zip", new UTF8Encoding(false, true),
        //    ("f.txt", "new content"));
        //string outDir = Path.Combine(_tempDir, "out_overwrite");
        //Directory.CreateDirectory(outDir);
        //File.WriteAllText(Path.Combine(outDir, "f.txt"), "old content");
        //
        //FileUtils.ExtractToDirectory(zipPath, outDir);
        //
        //Assert.Equal("new content", ReadFile(Path.Combine(outDir, "f.txt")));
        //
        //// 手动构造一个包含路径穿越条目（../../evil.txt）的 zip
        //string zipPath = Path.Combine(_tempDir, "zipslip.zip");
        //using (var stream = File.Create(zipPath))
        //using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: false, new UTF8Encoding(false, true))) {
        //    var entry = archive.CreateEntry("../../evil.txt");
        //    using var writer = new StreamWriter(entry.Open());
        //    writer.Write("pwned");
        //}
        //string outDir = Path.Combine(_tempDir, "out_zipslip");
        //Directory.CreateDirectory(outDir);
        //
        //Assert.Throws<UnauthorizedAccessException>(() => FileUtils.ExtractToDirectory(zipPath, outDir));
        //
        //const string innerContent = "gzip content";
        //const string innerFileName = "inner.txt";
        //string gzPath = CreateGzFile(innerFileName, innerContent);
        //string outDir = Path.Combine(_tempDir, "out_gz");
        //
        //FileUtils.ExtractToDirectory(gzPath, outDir);
        //
        //string extractedFile = Path.Combine(outDir, innerFileName);
        //Assert.True(File.Exists(extractedFile));
        //Assert.Equal(innerContent, ReadFile(extractedFile));
        //
        //// jar 文件实际上是 zip 格式
        //string zipPath = CreateZipWithEncoding("app.jar", new UTF8Encoding(false, true),
        //    ("META-INF/MANIFEST.MF", "Manifest-Version: 1.0\r\n"));
        //string outDir = Path.Combine(_tempDir, "out_jar");
        //
        //FileUtils.ExtractToDirectory(zipPath, outDir);
        //
        //Assert.True(File.Exists(Path.Combine(outDir, "META-INF", "MANIFEST.MF")));
    }

    #endregion





    #region 辅助方法

    private readonly string _tempDir;

    public FileUtilsTest() {
        _tempDir = Path.Combine(Path.GetTempPath(), "MeloongCoreTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose() {
        if (Directory.Exists(_tempDir)) Directory.Delete(_tempDir, recursive: true);
    }

    /// <summary>
    /// 创建一个包含若干文件的临时文件夹，并返回该文件夹路径。
    /// </summary>
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

    /// <summary>
    /// 创建一个只含 ASCII 文件名的合法 ZIP，使用指定编码写入文件名。
    /// </summary>
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

    /// <summary>
    /// 创建一个用于 GZip 解压测试的 .gz 文件。
    /// </summary>
    private string CreateGzFile(string innerFileName, string content) {
        string gzPath = Path.Combine(_tempDir, innerFileName + ".gz");
        byte[] contentBytes = Encoding.UTF8.GetBytes(content);
        using var fileStream = File.Create(gzPath);
        using var gzStream = new GZipStream(fileStream, CompressionMode.Compress);
        gzStream.Write(contentBytes, 0, contentBytes.Length);
        return gzPath;
    }

    /// <summary>
    /// 读取文件内容（兼容长路径）。
    /// </summary>
    private static string ReadFile(string path) {
        return File.ReadAllText(path, Encoding.UTF8);
    }

    #endregion

    #region CreateZipFromDirectory 测试

    
    public void CreateZipFromDirectory_SingleFile_ZipContainsFile() {
        string srcDir = CreateSourceDirectory(("hello.txt", "hello world"));
        string outZip = Path.Combine(_tempDir, "from_dir.zip");

        FileUtils.CreateZipFromDirectory(outZip, srcDir);

        Assert.True(File.Exists(outZip));
        using var archive = ZipFile.OpenRead(outZip);
        Assert.Single(archive.Entries);
        Assert.Equal("hello.txt", archive.Entries[0].Name);
    }

    
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

    
    public void CreateZipFromDirectory_OutputDirectoryAutoCreated() {
        string srcDir = CreateSourceDirectory(("f.txt", "x"));
        string outZip = Path.Combine(_tempDir, "newdir", "nested.zip");

        FileUtils.CreateZipFromDirectory(outZip, srcDir);

        Assert.True(File.Exists(outZip));
    }

    #endregion

    #region CreateZipFromFiles（params 版）测试

    
    public void CreateZipFromFiles_Params_SingleFile_ZipContainsFile() {
        string srcFile = Path.Combine(_tempDir, "single.txt");
        File.WriteAllText(srcFile, "content");
        string outZip = Path.Combine(_tempDir, "from_files_single.zip");

        FileUtils.CreateZipFromFiles(outZip, srcFile);

        using var archive = ZipFile.OpenRead(outZip);
        Assert.Single(archive.Entries);
        Assert.Equal("single.txt", archive.Entries[0].Name);
    }

    
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

    #region CreateZipFromFiles（IDictionary 版）测试

    
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
