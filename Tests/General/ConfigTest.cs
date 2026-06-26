namespace MeloongCore.Tests;
public class ConfigTest : TestWithFolder {

    #region JsonConfigProvider

    [Test]
    public async Task JsonConfigProvider_字符串列表() {
        string configFile = Path.Combine(tempFolder, "config.json");
        var entry = new ConfigEntry<List<string>>("list", [], new JsonConfigProvider(configFile));

        entry.Set(["Alpha", "中文", "123"]);

        var reloadedEntry = new ConfigEntry<List<string>>("list", [], new JsonConfigProvider(configFile));
        var reloadedValue = reloadedEntry.Get();
        await Assert.That(reloadedValue is not null).IsTrue();
        await Assert.That(string.Join("|", reloadedValue!)).IsEqualTo("Alpha|中文|123");
        await Assert.That(FileUtils.ReadAsString(configFile)).Contains("\"list\": [");
    }

    [Test]
    public async Task JsonConfigProvider_自定义类列表() {
        string configFile = Path.Combine(tempFolder, "config.json");
        var entry = new ConfigEntry<List<TestConfigItem>>("items", [], new JsonConfigProvider(configFile));

        entry.Set([
            new() { Name = "Fabric", Count = 2, Enabled = true },
            new() { Name = "Forge 中文", Count = 5, Enabled = false }
        ]);

        var reloadedEntry = new ConfigEntry<List<TestConfigItem>>("items", [], new JsonConfigProvider(configFile));
        var reloadedValue = reloadedEntry.Get();
        await Assert.That(reloadedValue is not null).IsTrue();
        await Assert.That(string.Join("|", reloadedValue!.Select(i => $"{i.Name}:{i.Count}:{i.Enabled}"))).IsEqualTo("Fabric:2:True|Forge 中文:5:False");

        string json = FileUtils.ReadAsString(configFile);
        await Assert.That(json).Contains("\"items\": [");
        await Assert.That(json).Contains("\"Name\": \"Fabric\"");
        await Assert.That(json).Contains("\"Count\": 2");
        await Assert.That(json).Contains("\"Enabled\": true");
        await Assert.That(json).Contains("\"Name\": \"Forge 中文\"");
        await Assert.That(json).Contains("\"Enabled\": false");
    }

    [Test]
    public async Task ConfigEntry_SetNull_RoundTripsThroughJson() {
        string configFile = Path.Combine(tempFolder, "config.json");
        var entry = new ConfigEntry<string>("nullable", "fallback", new JsonConfigProvider(configFile));

        entry.Set(null);

        var reloadedEntry = new ConfigEntry<string>("nullable", "fallback", new JsonConfigProvider(configFile));
        await Assert.That(reloadedEntry.HasValue()).IsTrue();
        await Assert.That(reloadedEntry.Get() is null).IsTrue();
        await Assert.That(FileUtils.ReadAsString(configFile)).Contains("\"nullable\": null");
    }

    [Test]
    public async Task ConfigEntry_SetString_RoundTripsThroughJson() {
        string configFile = Path.Combine(tempFolder, "config.json");
        var entry = new ConfigEntry<string>("text", "fallback", new JsonConfigProvider(configFile));

        entry.Set("plain text 中文");

        var reloadedEntry = new ConfigEntry<string>("text", "fallback", new JsonConfigProvider(configFile));
        await Assert.That(reloadedEntry.Get()).IsEqualTo("plain text 中文");
        await Assert.That(FileUtils.ReadAsString(configFile)).Contains("\"text\": \"plain text 中文\"");
    }

    [Test]
    public async Task JsonConfigProvider_SaveBeforeExit_WritesPendingChangesImmediately() {
        string configFile = Path.Combine(tempFolder, "config.json");
        var provider = new JsonConfigProvider(configFile);
        var entry = new ConfigEntry<string>("text", "fallback", provider);

        entry.Set("pending 中文");
        provider.SaveBeforeExit();
        provider.SaveBeforeExit();

        var reloadedEntry = new ConfigEntry<string>("text", "fallback", new JsonConfigProvider(configFile));
        await Assert.That(reloadedEntry.Get()).IsEqualTo("pending 中文");
        await Assert.That(FileUtils.ReadAsString(configFile)).Contains("\"text\": \"pending 中文\"");
    }

    public class TestConfigItem {
        public string Name { get; set; } = "";
        public int Count { get; set; }
        public bool Enabled { get; set; }
    }

    #endregion

}
