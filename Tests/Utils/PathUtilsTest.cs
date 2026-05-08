namespace MeloongCore.Tests;
public class PathUtilsTests {

    [Theory]
    [InlineData(@"\\?\C:\foo\bar.txt", @"\\?\C:\foo")]
    [InlineData(@"C:\foo\bar.txt", @"C:\foo")]
    [InlineData(@"\\?\C:\foo\bar", @"\\?\C:\foo")]
    [InlineData(@"C:\foo\bar", @"C:\foo")]
    [InlineData(@"C:\foo\bar\", @"C:\foo")]
    [InlineData(@"\\?\C:\foo\", @"\\?\C:")]
    [InlineData(@"C:\foo", @"C:")]
    [InlineData(@"C:\foo\", @"C:")]
    [InlineData("https://foo.bar/file/long/pack.zip?arg=1", "https://foo.bar/file/long")]
    [InlineData("https://foo.bar/file/", "https://foo.bar")]
    [InlineData("https://foo.bar/file/?arg=1", "https://foo.bar")]
    [InlineData("https://foo.bar/file?arg=1", "https://foo.bar")]
    public void 路径处理_RemoveLastPart(string input, string expected) 
        => Assert.Equal(expected, PathUtils.RemoveLastPart(input));

    [Theory]
    [InlineData(@"\\?\C:\foo\bar.txt", "bar.txt")]
    [InlineData(@"C:\foo\bar.txt", "bar.txt")]
    [InlineData(@"\\?\C:\foo\bar", "bar")]
    [InlineData(@"C:\foo\bar", "bar")]
    [InlineData(@"C:\foo\bar\", "bar")]
    [InlineData(@"\\?\C:\foo\", "foo")]
    [InlineData(@"C:\foo", "foo")]
    [InlineData(@"C:\foo\", "foo")]
    [InlineData("https://foo.bar/file/long/pack.zip?arg=1", "pack.zip")]
    [InlineData("https://foo.bar/file/", "file")]
    [InlineData("https://foo.bar/file/?arg=1", "file")]
    [InlineData("https://foo.bar/file?arg=1", "file")]
    public void 路径处理_GetLastPart(string input, string expected)
        => Assert.Equal(expected, PathUtils.GetLastPart(input));

    [Theory]
    [InlineData(@"\\?\C:\foo\bar.txt", "bar")]
    [InlineData(@"C:\foo\bar.2.txt", "bar.2")]
    [InlineData(@"\\?\C:\foo\bar", "bar")]
    [InlineData(@"C:\foo\bar", "bar")]
    [InlineData(@"C:\foo.bar\how\", "how")]
    [InlineData(@"\\?\C:\foo\", "foo")]
    [InlineData(@"C:\foo", "foo")]
    [InlineData(@"C:\foo\", "foo")]
    [InlineData("create.jar.disabled", "create.jar")]
    [InlineData("create.jar", "create")]
    [InlineData("create", "create")]
    [InlineData("https://foo.bar/file/page.xaml.vb?arg=1", "page.xaml")]
    [InlineData("https://foo.bar/file/long/pack.zip?arg=1", "pack")]
    [InlineData("https://foo.bar/some.how/file/", "file")]
    [InlineData("https://foo.bar/file/?arg=1", "file")]
    [InlineData("https://foo.bar/file?arg=1", "file")]
    public void 路径处理_GetFileNameWithoutExtension(string input, string expected)
        => Assert.Equal(expected, PathUtils.GetFileNameWithoutExtension(input));

    [Theory]
    [InlineData(@"\\?\C:\FOO\BAR.TXT", "txt")]
    [InlineData(@"C:\foo\bar.txt", "txt")]
    [InlineData(@"\\?\C:\foo\bar", "")]
    [InlineData(@"C:\foo\bar", "")]
    [InlineData(@"C:\foo\bar\", "")]
    [InlineData(@"\\?\C:\foo\", "")]
    [InlineData(@"C:\foo", "")]
    [InlineData(@"C:\foo\", "")]
    [InlineData("create.jar.diSAbled", "disabled")]
    [InlineData("https://foo.bar/file/page.xaml.vb?arg=1", "vb")]
    [InlineData("https://foo.bar/file/long/pack.ZIP?arg=1", "zip")]
    [InlineData("https://foo.bar/file/", "")]
    [InlineData("https://foo.bar/file/?arg=1", "")]
    [InlineData("https://foo.bar/file?arg=1", "")]
    public void 路径处理_GetExtension(string input, string expected)
        => Assert.Equal(expected, PathUtils.GetExtension(input));

}