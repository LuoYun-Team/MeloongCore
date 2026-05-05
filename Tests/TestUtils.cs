namespace MeloongCore.Tests;
public static class TestUtils {

    /// <summary>
    /// 获取测试用文件的路径。
    /// </summary>
    public static string GetTestFile(string directoryName, string fileName) {
        return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TestFiles", directoryName, fileName);
    }

}
