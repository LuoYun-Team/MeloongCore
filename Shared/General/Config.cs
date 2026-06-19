using System.Collections.Concurrent;
using System.Security.Cryptography;

namespace MeloongCore;

public interface IConfigProvider {
    /// <summary>
    /// 将数据写入缓存。
    /// <para/> 这不会写入数据，还需要调用 <see cref="Save"/> 以保存。
    /// </summary>
    void SetToCache(string key, string? value, bool encrypted);
    /// <summary>
    /// 将指定数据从缓存中移除。
    /// <para/> 这不会写入数据，还需要调用 <see cref="Save"/> 以保存。
    /// </summary>
    void RemoveFromCache(string key);
    string? Read(string key, string? defaultValue, bool encrypted);
    bool HasValue(string key);
    void DiscardCache();
    void Save();
}

public class JsonConfigProvider : IConfigProvider {
    private readonly string filePath;
    private readonly ConcurrentDictionary<string, string?> decryptedContentCache = new();

    public JsonConfigProvider(string filePath) {
        this.filePath = filePath;
        ResetContent();
    }

    private Lazy<JObject> content;
    private void ResetContent() {
        content = new(() => {
            try {
                return FileUtils.Exists(filePath) ? ((JObject?) FileUtils.ReadAsJson(filePath) ?? []) : [];
            } catch (Exception ex) {
                Logger.Error(ex, $"读取配置文件失败（{filePath}）", LogBehavior.Alert);
                return [];
            }
        });
    }

    public void SetToCache(string key, string? value, bool encrypted) {
        if (encrypted) {
            content.Value[key] = CryptographyUtils.AesEncrypt(value); // UNDONE: 改为识别码
            decryptedContentCache[key] = value;
        } else {
            content.Value[key] = value;
        }
    }
    public string? Read(string key, string? defaultValue, bool encrypted) {
        if (encrypted && decryptedContentCache.TryGetValue(key, out var cachedValue)) return cachedValue; // 读取缓存中已经解密的值
        try {
            if (!content.Value.TryGetValue(key, out var result)) return defaultValue;
            if (encrypted) { // 解密
                result = CryptographyUtils.AesDecrypt(result?.ToString());
                decryptedContentCache.TryAdd(key, result?.ToString());
            }
            return result?.ToString();
        } catch (CryptographicException ex) {
            Logger.Error(ex, $"解密配置失败，该配置将被重置（{key}）", LogBehavior.Alert);
            RemoveFromCache(key);
            Save();
            return defaultValue;
        }
    }

    public bool HasValue(string key)
        => content.Value.ContainsKey(key);
    public void RemoveFromCache(string key) {
        content.Value.Remove(key);
        decryptedContentCache.TryRemove(key, out _);
    }
    public void DiscardCache() {
        ResetContent();
        decryptedContentCache.Clear();
    }
    public void Save()
        => FileUtils.Write(filePath, content.Value.ToString(Formatting.Indented));
}

public static class ConfigUtils {
    public static readonly JsonConfigProvider AppData = new(Path.Combine(Paths.AppDataThenName, "config.json"));

    /// <summary>
    /// 从 secret.json 中读取特定密钥，如果未找到对应密钥则抛出异常。
    /// </summary>
    public static string GetSecret(string key) 
        => secret.Read(key, null, false)?.ToString() ?? throw new KeyNotFoundException($"Secret not found: {key}");
    private static readonly JsonConfigProvider secret = new(Path.Combine(Paths.AppData, "secret.json"));
}

public class ConfigEntry<T>(string key, T? defaultValue, IConfigProvider? defaultProvider = null, bool encrypted = false) {
    private IConfigProvider GetProvider(IConfigProvider? provider)
        => provider ?? defaultProvider ?? ConfigUtils.AppData;

    /// <summary>
    /// 当设置项的值实际被改变时触发，参数为新值。
    /// </summary>
    public event Action<T?>? Changed;

    public T? Get(IConfigProvider? providerOverride = null) {
        var provider = GetProvider(providerOverride);
        if (!provider.HasValue(key)) return defaultValue;
        string? value = provider.Read(key, defaultValue?.ToString(), encrypted);
        try {
            if (value is null) return default;
            return typeof(T) == typeof(string) ? ((T) (object) value) : JsonConvert.DeserializeObject<T>(value);
        } catch (Exception ex) {
            Logger.Error(ex, $"读取配置项失败（{key}）", LogBehavior.Toast);
            return defaultValue;
        }
    }
    public void Set(T? value, IConfigProvider? providerOverride = null) {
        var provider = GetProvider(providerOverride);
        T? current = Get(provider);
        if ((current is null && value is null) || (current is not null && value is not null && current.Equals(value))) return; // 值未改变，无需更新
        provider.SetToCache(key, 
            typeof(T) == typeof(string) ? (value as string) : JsonConvert.SerializeObject(value), encrypted);
        provider.Save();
        Changed?.Invoke(value);
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
        Changed?.Invoke(defaultValue);
    }
    public void Save(IConfigProvider? providerOverride = null) {
        var provider = GetProvider(providerOverride);
        provider.Save();
    }
}
