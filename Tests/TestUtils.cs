namespace MeloongCore.Tests;
public static class TestUtils {

    /// <summary>
    /// 获取一个在极端路径下的测试文件夹。
    /// </summary>
    public static string GetTempFolder(string? identifier = null) {
        string folderPath = Path.Combine(
            Path.GetTempPath(), "PCL", "Tests", 
            (identifier ?? "Test") + "-" + Guid.NewGuid().ToString(), 
            "文件夹 Dir_!@#$%^&()_+={}[];',_",
            new string('X', 200), new string('X', 200));
        Directory.CreateDirectory(@"\\?\" + folderPath);
        return folderPath;
    }

    /// <summary>
    /// 将指定的测试用文件输出到一个极端路径下，然后返回文件路径。
    /// </summary>
    public static string GetTestFile(string directoryName, string fileName) {
        var sourceFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TestFiles", directoryName, fileName);
        var distPath = Path.Combine(GetTempFolder("GetTestFile"), Path.GetFileName(sourceFilePath));
        File.Copy(sourceFilePath, @"\\?\" + distPath);
        return distPath;
    }

}
