namespace MeloongCore.Tests;

/// <summary>
/// 用于带文件测试的基类。
/// 会在极端路径下创建测试用的临时文件夹。
/// </summary>
public abstract class TestWithFolder {

    /// <summary>
    /// 测试用的临时文件夹路径。
    /// 这是一个包含特殊字符的长路径，以 \ 结尾。
    /// </summary>
    private readonly string tempFolder;
    public TestWithFolder() {
        tempFolder = Path.Combine(
            Path.GetTempPath(), "PCL", "Tests",
            $"{GetType().Name}-{Guid.NewGuid()}",
            "文件夹 Dir_!@#$%^&()_+={}[];',_",
            new string('X', 200), new string('X', 200)) + @"\";
        Directory.CreateDirectory(@"\\?\" + tempFolder);
    }

    /// <summary>
    /// 输出指定的测试用文件，返回文件路径。
    /// </summary>
    public string GetTestFile(string fileName) {
        var sourceFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TestFiles", GetType().Name.Replace("Test", ""), fileName);
        var distPath = Path.Combine(tempFolder, Path.GetFileName(sourceFilePath));
        File.Copy(sourceFilePath, @"\\?\" + distPath);
        return distPath;
    }

}
