namespace MeloongCore.Tests;
public class FileUtilsTest : TestWithFolder {

    #region 压缩包

    [Theory]
    [InlineData("GB Encoding.zip")]
    [InlineData("UTF8 Encoding.zip")]
    public void Compression_ReadZip(string testFile) {
        double progress = 0;
        string output = Path.Combine(tempFolder, "Extracted");
        FileUtils.ExtractToDirectory(GetTestFile(testFile), output, p => progress = p);
        Assert.Equal(1, progress);
        Assert.True(DirectoryUtils.Exists(Path.Combine(output, "文件夹")));
        Assert.False(DirectoryUtils.Exists(Path.Combine(output, "空文件夹")));
        Assert.Contains("FabricLoader", File.ReadAllText(PathUtils.WithLongPath(Path.Combine(output, "fabricloader.log"))));
        Assert.Contains("测试内容", File.ReadAllText(PathUtils.WithLongPath(Path.Combine(output, "文件夹", "中文文件.txt"))));
    }

    [Theory]
    [InlineData("GZ.gz", "LTCat")]
    public void Compression_ReadGz(string testFile, string containsText) {
        double progress = 0;
        string output = Path.Combine(tempFolder, "Extracted");
        FileUtils.ExtractToDirectory(GetTestFile(testFile), output, p => progress = p);
        Assert.Equal(1, progress);
        Assert.Contains(containsText, File.ReadAllText(PathUtils.WithLongPath(Path.Combine(output, Path.GetFileNameWithoutExtension(testFile)))));
    }

    [Theory]
    [InlineData("Corrupted.zip")]
    [InlineData("Not zip.zip")]
    public void Compression_ReadBad(string testFile) {
        Assert.Throws<InvalidDataException>(() => FileUtils.ExtractToDirectory(GetTestFile(testFile), tempFolder));
    }

    [Theory]
    [InlineData("DotDot ZipSlip.zip")]
    [InlineData("AbsPath ZipSlip.zip")]
    public void Compression_ZipSlip(string testFile) {
        Assert.Throws<UnauthorizedAccessException>(() => FileUtils.ExtractToDirectory(GetTestFile(testFile), tempFolder));
    }

    #endregion




    /*
     * 
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
    */
}
