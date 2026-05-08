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
    public void 路径处理_RemoveLastPart(string? input, string expected) 
        => Assert.Equal(expected, PathUtils.RemoveLastPart(input));

}