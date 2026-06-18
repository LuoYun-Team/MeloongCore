using System.Collections.Concurrent;
using System.Security.Cryptography;

namespace MeloongCore;

public interface IConfigProvider {
    /// <summary>
    /// 将原始数据存入缓存。
    /// </summary>
    void Set(string key, string value, bool encrypted = false);
    string? Read(string key, string? defaultValue = null, bool encrypted = false);
    bool HasValue(string key);
    void Remove(string key);
    void Save();
    void ClearCache();
}

public class JsonConfigProvider : IConfigProvider {
    private readonly string filePath;
    public readonly ConcurrentDictionary<string, string?> DecryptedContent = new();

    public JsonConfigProvider(string filePath) {
        this.filePath = filePath;
        ResetContent();
    }

    /// <summary>
    /// 读取到的原始内容。
    /// </summary>
    public Lazy<JObject> Content { get; private set; }
    private void ResetContent() {
        Content = new(() => {
            try {
                return FileUtils.Exists(filePath) ? ((JObject?) FileUtils.ReadAsJson(filePath) ?? []) : [];
            } catch (Exception ex) {
                Logger.Error(ex, $"读取配置文件失败：{filePath}", LogBehavior.Toast);
                return [];
            }
        });
    }

    public void Set(string key, string? value, bool encrypted = false) {
        if (encrypted) {
            Content.Value[key] = CryptographyUtils.AesEncrypt(value); // UNDONE: 改为识别码
            DecryptedContent[key] = value;
        } else {
            Content.Value[key] = value;
        }
    }
    public string? Read(string key, string? defaultValue = null, bool encrypted = false) {
        if (!Content.Value.ContainsKey(key)) return defaultValue;
        if (encrypted && DecryptedContent.TryGetValue(key, out var cachedValue)) return cachedValue; // 读取缓存中已经解密的值
        // 读取值
        string value = Content.Value[key]!.ToString();
        if (!encrypted) return value;
        // 解密读取的数据
        try {
            return DecryptedContent.GetOrAdd(key, _ => CryptographyUtils.AesDecrypt(value));
        } catch (CryptographicException ex) {
            Logger.Error(ex, $"解密设置失败，该设置将被重置：{key}", LogBehavior.Toast);
            Remove(key);
            Save();
            return defaultValue;
        }
    }

    public bool HasValue(string key)
        => Content.Value.ContainsKey(key);
    public void Remove(string key) {
        Content.Value.Remove(key);
        DecryptedContent.TryRemove(key, out _);
    }
    public void Save()
        => FileUtils.Write(filePath, Content.Value.ToString(Formatting.Indented));
    public void ClearCache() {
        ResetContent();
        DecryptedContent.Clear();
    }
}

public static class Configs {
    public static readonly JsonConfigProvider AppData = new(Path.Combine(Paths.AppDataThenName, "config.json"));

    /// <summary>
    /// 从 secret.json 中读取特定密钥，如果未找到对应密钥则抛出异常。
    /// </summary>
    public static string GetSecret(string key) => secret.Read(key)?.ToString() ?? throw new KeyNotFoundException($"Secret not found: {key}");
    private static readonly JsonConfigProvider secret = new(Path.Combine(Paths.AppData, "secret.json"));
}

public class ConfigEntry<T>(string key, T? defaultValue, IConfigProvider? defaultProvider = null, bool encrypted = false) {
    private IConfigProvider GetProvider(IConfigProvider? provider)
        => provider ?? defaultProvider ?? Configs.AppData;

    /// <summary>
    /// 当设置项的值实际被改变时触发，参数为新值。
    /// </summary>
    public event Action<T?>? OnChanged;


    /*public T? Value {
        get {
            return GetProvider(null).Read(key, JToken.FromObject(defaultValue))?.ToObject<T>();
        }
        set {
            var provider = GetProvider(null);
            var oldValue = Value;
            if (!Equals(oldValue, value)) {
                provider.Write(key, JToken.FromObject(value));
                OnChanged?.Invoke(value);
            }
        }
    }

    public T? GetValue(IConfigProvider? provider = null) {
        return GetProvider(provider).Read(key, JToken.FromObject(defaultValue))?.ToObject<T>();
    }*/

    public bool HasValue(IConfigProvider? provider = null)
        => GetProvider(provider).HasValue(key);
    public void Remove(IConfigProvider? provider = null)
        => GetProvider(provider).Remove(key);
    public void Save(IConfigProvider? provider = null)
        => GetProvider(provider).Save();
}