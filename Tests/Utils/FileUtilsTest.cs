namespace MeloongCore.Tests;
public class FileUtilsTest : TestWithFolder {

    #region 压缩包

    [Theory]
    [InlineData("GB Encoding.zip")]
    [InlineData("UTF8 Encoding.zip")]
    public void 压缩包_ReadZip(string testFile) {
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
    public void 压缩包_ReadGz(string testFile, string containsText) {
        double progress = 0;
        string output = Path.Combine(tempFolder, "Extracted");
        FileUtils.ExtractToDirectory(GetTestFile(testFile), output, p => progress = p);
        Assert.Equal(1, progress);
        Assert.Contains(containsText, File.ReadAllText(PathUtils.WithLongPath(Path.Combine(output, Path.GetFileNameWithoutExtension(testFile)))));
    }

    [Theory]
    [InlineData("Corrupted.zip")]
    [InlineData("Not zip.zip")]
    public void 压缩包_ReadBad(string testFile) {
        Assert.Throws<InvalidDataException>(() => FileUtils.ExtractToDirectory(GetTestFile(testFile), tempFolder));
    }

    [Theory]
    [InlineData("DotDot ZipSlip.zip")]
    [InlineData("AbsPath ZipSlip.zip")]
    public void 压缩包_ZipSlip(string testFile) {
        Assert.Throws<FileUtils.ZipSlipException>(() => FileUtils.ExtractToDirectory(GetTestFile(testFile), tempFolder));
    }

    #endregion

}
