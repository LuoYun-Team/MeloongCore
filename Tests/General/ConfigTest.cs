namespace MeloongCore.Tests;

public class ConfigTest : TestWithFolder {

    [Test]
    public async Task JsonConfigProvider_EncryptedReadWrite() {
        var filePath = Path.Combine(tempFolder, "config.json");
        var provider = new JsonConfigProvider(filePath);

        provider.Set("token", "plain token", encrypted: true);
        provider.Save();

        await Assert.That(FileUtils.ReadAsString(filePath).Contains("plain token")).IsFalse();
        await Assert.That(provider.Read("token", encrypted: true)).IsEqualTo("plain token");
        await Assert.That(provider.DecryptedContent.TryGetValue("token", out var decrypted) && decrypted == "plain token").IsTrue();
    }

    [Test]
    public async Task JsonConfigProvider_ClearCache() {
        var filePath = Path.Combine(tempFolder, "config.json");
        var provider = new JsonConfigProvider(filePath);

        provider.Set("token", "old token", encrypted: true);
        provider.Save();
        await Assert.That(provider.Read("token", encrypted: true)).IsEqualTo("old token");

        var secondProvider = new JsonConfigProvider(filePath);
        secondProvider.Set("token", "new token", encrypted: true);
        secondProvider.Save();
        provider.ClearCache();

        await Assert.That(provider.DecryptedContent.IsEmpty).IsTrue();
        await Assert.That(provider.Read("token", encrypted: true)).IsEqualTo("new token");
    }

}
