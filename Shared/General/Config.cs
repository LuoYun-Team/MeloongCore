namespace MeloongCore;

public interface IConfigProvider {
    void Write(string key, JToken value);
    JToken? Read(string key, JToken? defaultValue = null);
    bool HasValue(string key);
    void Remove(string key);
    void Save();
}
public class JsonConfigProvider(string filePath) : IConfigProvider {
    public readonly Lazy<JObject> Content = new(() => {
        try {
            return FileUtils.Exists(filePath) ? ((JObject?) FileUtils.ReadAsJson(filePath) ?? []) : [];
        } catch (Exception ex) {
            Logger.Error(ex, $"读取配置文件失败：{filePath}", LogBehavior.Toast);
            return [];
        }
    });
    public void Write(string key, JToken value)
        => Content.Value[key] = value;
    public JToken? Read(string key, JToken? defaultValue = null)
        => Content.Value.TryGetValue(key, out var value) ? value : defaultValue;
    public bool HasValue(string key)
        => Content.Value.ContainsKey(key);
    public void Remove(string key)
        => Content.Value.Remove(key);
    public void Save()
        => FileUtils.Write(filePath, Content.Value.ToString(Formatting.Indented));
}
public static class Configs {
    public static readonly JsonConfigProvider AppData = new(Path.Combine(Paths.AppDataThenName, "config.json"));

    /// <summary>
    /// 从 secret.json 中读取特定密钥，如果不存在则抛出异常。
    /// </summary>
    public static string GetSecret(string key) => secret.Read(key)?.ToString() ?? throw new KeyNotFoundException($"Secret not found: {key}");
    private static readonly JsonConfigProvider secret = new(Path.Combine(Paths.AppData, "secret.json"));
}

public class ConfigEntry<T>(string key, T? defaultValue, IConfigProvider? defaultProvider = null, bool encrypted = false) {
    private readonly IConfigProvider defaultProvider = defaultProvider ?? Configs.AppData;
    public string Key = key;
    public bool Encrypted = encrypted;
    public T? DefaultValue = defaultValue;

    /// <summary>
    /// 当设置项的值实际被改变时触发，参数为新值。
    /// </summary>
    public event Action<T?>? OnChanged;

    public bool HasValue(IConfigProvider? provider = null) 
        => (provider ?? defaultProvider).HasValue(Key);
    public void Remove(IConfigProvider? provider = null) 
        => (provider ?? defaultProvider).Remove(Key);
    public void Save(IConfigProvider? provider = null) 
        => (provider ?? defaultProvider).Save();
}