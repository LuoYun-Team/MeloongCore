namespace MeloongCore.Tests;
public class DirectoryUtilsTest : TestWithFolder {

    // 在 tempFolder 下创建固定的测试目录结构：
    //   root/
    //     a.txt
    //     b.json
    //     sub/
    //       c.txt
    //       nested/
    private void CreateTestStructure(string root) {
        DirectoryUtils.Create(Path.Combine(root, "sub", "nested"));
        FileUtils.Write(Path.Combine(root, "a.txt"), "a");
        FileUtils.Write(Path.Combine(root, "b.json"), "b");
        FileUtils.Write(Path.Combine(root, "sub", "c.txt"), "c");
    }

    #region GetFiles

    [Test]
    [Arguments(false, "*",      3)]   // 递归：a.txt b.json sub/c.txt
    [Arguments(true,  "*",      2)]   // 仅顶层：a.txt b.json
    [Arguments(false, "*.txt",  2)]   // 递归 + 过滤：a.txt sub/c.txt
    [Arguments(true,  "*.txt",  1)]   // 仅顶层 + 过滤：a.txt
    public async Task GetFiles_Options(bool topDirectoryOnly, string searchPattern, int expectedCount) {
        var root = Path.Combine(tempFolder, "root");
        CreateTestStructure(root);
        var files = DirectoryUtils.GetFiles(root, topDirectoryOnly, searchPattern).ToList();
        await Assert.That(files.Count).IsEqualTo(expectedCount);
    }

    [Test]
    public async Task GetFiles_NotExist_ReturnsEmpty() {
        var files = DirectoryUtils.GetFiles(Path.Combine(tempFolder, "none"));
        await Assert.That(files.Any()).IsFalse();
    }

    [Test]
    public async Task GetFiles_PathsHaveNoLongPathPrefix() {
        var root = Path.Combine(tempFolder, "root");
        CreateTestStructure(root);
        foreach (var file in DirectoryUtils.GetFiles(root))
            await Assert.That(file.StartsWithF(@"\\?\")).IsFalse();
    }

    #endregion

    #region GetDirectories

    [Test]
    [Arguments(false, 2)]   // 递归：sub sub/nested
    [Arguments(true,  1)]   // 仅顶层：sub
    public async Task GetDirectories_TopDirectoryOnly(bool topDirectoryOnly, int expectedCount) {
        var root = Path.Combine(tempFolder, "root");
        CreateTestStructure(root);
        var dirs = DirectoryUtils.GetDirectories(root, topDirectoryOnly).ToList();
        await Assert.That(dirs.Count).IsEqualTo(expectedCount);
    }

    [Test]
    public async Task GetDirectories_NotExist_ReturnsEmpty() {
        var dirs = DirectoryUtils.GetDirectories(Path.Combine(tempFolder, "none"));
        await Assert.That(dirs.Any()).IsFalse();
    }

    [Test]
    public async Task GetDirectories_PathsHaveNoTrailingSeparator() {
        var root = Path.Combine(tempFolder, "root");
        CreateTestStructure(root);
        foreach (var dir in DirectoryUtils.GetDirectories(root))
            await Assert.That(dir.EndsWithF(Path.DirectorySeparatorChar)).IsFalse();
    }

    #endregion

    #region IsEmpty

    [Test]
    [Arguments(false, false, true)]    // 文件夹不存在 → true
    [Arguments(true,  false, true)]    // 文件夹存在但为空 → true
    [Arguments(true,  true,  false)]   // 文件夹存在且含有文件 → false
    public async Task IsEmpty_Scenarios(bool createFolder, bool addFile, bool expectedResult) {
        var folder = Path.Combine(tempFolder, "target");
        if (createFolder) DirectoryUtils.Create(folder);
        if (addFile) FileUtils.Write(Path.Combine(folder, "f.txt"), "x");
        await Assert.That(DirectoryUtils.IsEmpty(folder)).IsEqualTo(expectedResult);
    }

    [Test]
    public async Task IsEmpty_WithSubDir_IsFalse() {
        var folder = Path.Combine(tempFolder, "target");
        DirectoryUtils.Create(Path.Combine(folder, "sub"));
        await Assert.That(DirectoryUtils.IsEmpty(folder)).IsFalse();
    }

    #endregion

    #region Copy

    [Test]
    public async Task Copy_SamePath_IsNoOp() {
        var src = Path.Combine(tempFolder, "src");
        FileUtils.Write(Path.Combine(src, "f.txt"), "hello");
        DirectoryUtils.Copy(src, src);
        // 来源依然存在且内容不变
        await Assert.That(DirectoryUtils.Exists(src)).IsTrue();
        await Assert.That(FileUtils.ReadAsString(Path.Combine(src, "f.txt"))).IsEqualTo("hello");
    }

    [Test]
    [Arguments("hello", "world")]
    [Arguments("内容A", "内容B")]
    public async Task Copy_NewPath_DuplicatesFilesWithContent(string content1, string content2) {
        var src  = Path.Combine(tempFolder, "cp_src");
        var dest = Path.Combine(tempFolder, "cp_dest");
        FileUtils.Write(Path.Combine(src, "a.txt"), content1);
        FileUtils.Write(Path.Combine(src, "sub", "b.txt"), content2);
        DirectoryUtils.Copy(src, dest);
        // 来源保留，目标存在且内容一致
        await Assert.That(DirectoryUtils.Exists(src)).IsTrue();
        await Assert.That(FileUtils.ReadAsString(Path.Combine(dest, "a.txt"))).IsEqualTo(content1);
        await Assert.That(FileUtils.ReadAsString(Path.Combine(dest, "sub", "b.txt"))).IsEqualTo(content2);
    }

    #endregion

    #region Move

    [Test]
    public async Task Move_SamePath_IsNoOp() {
        var src = Path.Combine(tempFolder, "mv_src");
        FileUtils.Write(Path.Combine(src, "f.txt"), "data");
        DirectoryUtils.Move(src, src);
        await Assert.That(DirectoryUtils.Exists(src)).IsTrue();
        await Assert.That(FileUtils.ReadAsString(Path.Combine(src, "f.txt"))).IsEqualTo("data");
    }

    [Test]
    [Arguments("content1")]
    [Arguments("内容2")]
    public async Task Move_NewPath_MovesAllFiles(string fileContent) {
        var src  = Path.Combine(tempFolder, "mv2_src");
        var dest = Path.Combine(tempFolder, "mv2_dest");
        FileUtils.Write(Path.Combine(src, "a.txt"), fileContent);
        FileUtils.Write(Path.Combine(src, "sub", "b.txt"), "nested");
        DirectoryUtils.Move(src, dest);
        // 来源已消失，目标文件内容正确
        await Assert.That(DirectoryUtils.Exists(src)).IsFalse();
        await Assert.That(FileUtils.ReadAsString(Path.Combine(dest, "a.txt"))).IsEqualTo(fileContent);
        await Assert.That(FileUtils.Exists(Path.Combine(dest, "sub", "b.txt"))).IsTrue();
    }

    [Test]
    public void Move_SafetyCheck_ThrowsOnRoot()
        => Assert.Throws<UnauthorizedAccessException>(() => DirectoryUtils.Move(@"C:\", @"C:\Temp"));

    #endregion

    #region Delete

    [Test]
    public void Delete_NotExist_NoOp() {
        // 不应抛出异常
        DirectoryUtils.Delete(Path.Combine(tempFolder, "del_none"));
    }

    [Test]
    [Arguments(true,  false)]   // 仅含文件
    [Arguments(false, true)]    // 仅含子文件夹
    [Arguments(true,  true)]    // 文件与子文件夹均有
    public async Task Delete_WithContent_DeletesRecursively(bool addFile, bool addSubDir) {
        var folder = Path.Combine(tempFolder, "del");
        if (addFile)   FileUtils.Write(Path.Combine(folder, "f.txt"), "x");
        if (addSubDir) DirectoryUtils.Create(Path.Combine(folder, "sub"));
        DirectoryUtils.Delete(folder);
        await Assert.That(DirectoryUtils.Exists(folder)).IsFalse();
    }

    [Test]
    public void Delete_SafetyCheck_ThrowsOnRoot()
        => Assert.Throws<UnauthorizedAccessException>(() => DirectoryUtils.Delete(@"C:\"));

    #endregion

}
