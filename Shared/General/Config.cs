using System.Collections.Concurrent;
using System.Security.Cryptography;

namespace MeloongCore;

public interface IConfigProvider {
    /// <summary>
    /// 将数据写入缓存。
    /// <para/> 这不会写入数据，还需要调用 <see cref="Save"/> 以保存。
    /// </summary>
    void SetToCache(string key, string? value, bool encrypted = false);
    /// <summary>
    /// 将指定数据从缓存中移除。
    /// <para/> 这不会写入数据，还需要调用 <see cref="Save"/> 以保存。
    /// </summary>
    void RemoveFromCache(string key);
    string? Read(string key, string? defaultValue = null, bool encrypted = false);
    bool HasValue(string key);
    void ClearCache();
    void Save();
}

public class JsonConfigProvider : IConfigProvider {
    private readonly string filePath;
    private readonly ConcurrentDictionary<string, string?> decryptedContentCache = new();

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
                Logger.Error(ex, $"读取配置文件失败（{filePath}）", LogBehavior.Toast);
                return [];
            }
        });
    }

    public void SetToCache(string key, string? value, bool encrypted = false) {
        if (encrypted) {
            Content.Value[key] = CryptographyUtils.AesEncrypt(value); // UNDONE: 改为识别码
            decryptedContentCache[key] = value;
        } else {
            Content.Value[key] = value;
        }
    }
    public string? Read(string key, string? defaultValue = null, bool encrypted = false) {
        if (!Content.Value.ContainsKey(key)) return defaultValue;
        if (encrypted && decryptedContentCache.TryGetValue(key, out var cachedValue)) return cachedValue; // 读取缓存中已经解密的值
        // 读取值
        string? value = Content.Value[key]?.ToString();
        if (!encrypted) return value;
        // 解密读取的数据
        try {
            return decryptedContentCache.GetOrAdd(key, _ => CryptographyUtils.AesDecrypt(value));
        } catch (CryptographicException ex) {
            Logger.Error(ex, $"解密配置失败（{key}）", LogBehavior.Toast);
            RemoveFromCache(key);
            Save();
            return defaultValue;
        }
    }

    public bool HasValue(string key)
        => Content.Value.ContainsKey(key);
    public void RemoveFromCache(string key) {
        Content.Value.Remove(key);
        decryptedContentCache.TryRemove(key, out _);
    }
    public void ClearCache() {
        ResetContent();
        decryptedContentCache.Clear();
    }
    public void Save()
        => FileUtils.Write(filePath, Content.Value.ToString(Formatting.Indented));
}

public static class Configs {
    public static readonly JsonConfigProvider AppData = new(Path.Combine(Paths.AppDataThenName, "config.json"));

    /// <summary>
    /// 从 secret.json 中读取特定密钥，如果未找到对应密钥则抛出异常。
    /// </summary>
    public static string GetSecret(string key) => secret.Read(key)?.ToString() ?? throw new KeyNotFoundException($"Secret not found: {key}");
    private static readonly JsonConfigProvider secret = new(Path.Combine(Paths.AppData, "secret.json"));
}

public class ConfigEntry<T>(string key, T? defaultValue, IConfigProvider? defaultProvider = null, bool encrypted = false) where T : IEquatable<T> {
    private IConfigProvider GetProvider(IConfigProvider? provider)
        => provider ?? defaultProvider ?? Configs.AppData;

    /// <summary>
    /// 当设置项的值实际被改变时触发，参数为新值。
    /// </summary>
    public event Action<T?>? OnChanged;

    public T? Get(IConfigProvider? providerOverride = null) {
        var provider = GetProvider(providerOverride);
        if (!provider.HasValue(key)) return defaultValue;
        string? value = provider.Read(key, defaultValue?.ToString(), encrypted);
        try {
            if (value is null) return default;
            var type = typeof(T);
            if (type == typeof(string)) return (T) (object) value;
            if (typeof(JToken).IsAssignableFrom(type)) return (T) (object) value.DeserializeJson()!;
            return (T) Convert.ChangeType(value, type);
        } catch (Exception ex) {
            Logger.Error(ex, $"读取配置项失败（{key}）", LogBehavior.Toast);
            return defaultValue;
        }
    }
    public void Set(T? value, IConfigProvider? providerOverride = null) {
        var provider = GetProvider(providerOverride);
        T? current = Get(provider);
        if ((current is null && value is null) || (current is not null && value is not null && current.Equals(value))) return; // 值未改变，无需更新
        provider.SetToCache(key, value?.ToString(), encrypted);
        provider.Save();
        OnChanged?.Invoke(value);
    }
    public bool HasValue(IConfigProvider? providerOverride = null) {
        var provider = GetProvider(providerOverride);
        return provider.HasValue(key);
    }
    public void Reset(IConfigProvider? providerOverride = null) {
        var provider = GetProvider(providerOverride);
        if (!provider.HasValue(key)) return; // 值未设置，无需重置
        provider.RemoveFromCache(key);
        provider.Save();
        OnChanged?.Invoke(defaultValue);
    }
    public void Save(IConfigProvider? providerOverride = null) {
        var provider = GetProvider(providerOverride);
        provider.Save();
    }
}
