using System.Security.Cryptography;

namespace MeloongCore;

public interface IConfigProvider {
    /// <summary>
    /// 将数据写入缓存。
    /// <para/> 这不会写入数据，还需要调用 <see cref="Save"/> 以保存。
    /// </summary>
    void SetToCache<T>(string key, T? value, bool encrypted);
    /// <summary>
    /// 将指定数据从缓存中移除。
    /// <para/> 这不会写入数据，还需要调用 <see cref="Save"/> 以保存。
    /// </summary>
    void RemoveFromCache(string key);
    T? Read<T>(string key, T? defaultValue, bool encrypted);
    bool HasValue(string key);
    void DiscardCache();
    void Save();
}

public class JsonConfigProvider : IConfigProvider {
    private readonly string filePath;
    public JsonConfigProvider(string filePath) {
        this.filePath = filePath;
        saveAction = Throttler.Throttle(SaveImmediately, TimeSpan.FromMilliseconds(500), leading: false, trailing: true);
        InitJson();
        AppDomain.CurrentDomain.ProcessExit += (_, _) => SaveBeforeExit();
    }

    // 原始文件缓存
    private Lazy<JObject> json;
    // 反序列化后的结果缓存
    private readonly ConcurrentDictionary<string, object?> cache = new();
    private void InitJson() {
        json = new(() => {
            try {
                return FileUtils.Exists(filePath) ? ((JObject?) FileUtils.ReadAsJson(filePath) ?? []) : [];
            } catch (Exception ex) {
                Logger.Error(ex, $"读取配置文件失败（{filePath}）", LogBehavior.Alert);
                return [];
            }
        });
    }

    // ================================= 设置、读取、缓存 =================================

    public void SetToCache<T>(string key, T? value, bool encrypted) {
        cache[key] = value;
        lock (json) {
            if (value is null) {
                // 直接在 JSON 中表示为 null
                json.Value[key] = JValue.CreateNull();
            } else if (encrypted) {
                // 需要加密，在 JSON 中保存密文字符串
                json.Value[key] = CryptographyUtils.AesEncrypt(value is string str ? str : JsonConvert.SerializeObject(value)); // TODO: 加密改为使用识别码
            } else {
                // 用 JToken 保留原始结构
                json.Value[key] = JToken.FromObject(value);
            }
        }
    }
    public T? Read<T>(string key, T? defaultValue, bool encrypted) {
        if (cache.TryGetValue(key, out var cachedValue)) return (T?) cachedValue; // 读取缓存
        JToken entry;
        lock (json) {
            if (!json.Value.TryGetValue(key, out entry!)) return defaultValue; // 未找到该键
            entry = entry.DeepClone();
        }
        try {
            T? result;
            if (entry.Type is JTokenType.Null or JTokenType.Undefined) {
                result = default;
            } else if (encrypted) {
                string plainText = CryptographyUtils.AesDecrypt(entry.Value<string>())!;
                result = typeof(T) == typeof(string) ? (T) (object) plainText : JsonConvert.DeserializeObject<T>(plainText);
            } else {
                result = entry.ToObject<T>();
            }
            cache[key] = result;
            return result;
        } catch (CryptographicException ex) {
            Logger.Error(ex, $"解密配置失败，该配置将被重置（{key}）", LogBehavior.Alert);
            RemoveFromCache(key);
            Save();
            return defaultValue;
        }
    }

    public bool HasValue(string key) {
        lock (json) return json.Value.ContainsKey(key);
    }
    public void RemoveFromCache(string key) {
        lock (json) json.Value.Remove(key);
        cache.TryRemove(key, out _);
    }
    public void DiscardCache() {
        lock (json) InitJson();
        cache.Clear();
    }

    // ===================================== 保存 =====================================

    private readonly RateLimitedAction saveAction;
    private bool isDirty = false;
    public void Save() {
        lock (json) isDirty = true;
        saveAction.Invoke();
    }
    private void SaveBeforeExit() {
        bool shouldSave;
        lock (json) shouldSave = isDirty;
        if (shouldSave) SaveImmediately();
        saveAction.Dispose();
    }
    private void SaveImmediately() {
        lock (json) {
            FileUtils.Write(filePath, json.Value.ToString(Formatting.Indented));
            isDirty = false;
        }
    }
}

public static class ConfigUtils {
    public static readonly JsonConfigProvider AppData = new(Path.Combine(Paths.AppDataThenName, "config.json"));

    /// <summary>
    /// 从 secret.json 中读取特定密钥，如果未找到对应密钥则抛出异常。
    /// </summary>
    public static string GetSecret(string key) 
        => secret.Read<string>(key, null, false) ?? throw new KeyNotFoundException($"Secret not found: {key}");
    private static readonly JsonConfigProvider secret = new(Path.Combine(Paths.AppData, "secret.json"));
}

public class ConfigEntry<T>(string key, T? defaultValue, IConfigProvider? defaultProvider = null, bool encrypted = false) {

    public IConfigProvider DefaultProvider => defaultProvider ?? ConfigUtils.AppData;
    /// <summary>
    /// 当设置项的值实际被改变时触发，参数为新值。
    /// </summary>
    public event Action<T?, IConfigProvider>? Changed;

    public void Edit(Func<T?, T?> editFunc, IConfigProvider? providerOverride = null) {
        var provider = providerOverride ?? DefaultProvider;
        Set(editFunc(Get(provider)), provider);
    }
    public void Edit(RefAction editAction, IConfigProvider? providerOverride = null) {
        var provider = providerOverride ?? DefaultProvider;
        var current = Get(provider);
        editAction(ref current);
        Set(current, provider);
    }
    public delegate void RefAction(ref T? value);

    public void Set(T? value, IConfigProvider? providerOverride = null) {
        var provider = providerOverride ?? DefaultProvider;
        T? current = Get(provider);
        if ((current is null && value is null) || (current is not null && value is not null && current.Equals(value))) return; // 值未改变，无需更新
        provider.SetToCache(key, value, encrypted);
        provider.Save();
        Changed?.Invoke(value, provider);
    }
    public void Reset(IConfigProvider? providerOverride = null) {
        var provider = providerOverride ?? DefaultProvider;
        if (!provider.HasValue(key)) return; // 值未设置，无需重置
        provider.RemoveFromCache(key);
        provider.Save();
        Changed?.Invoke(defaultValue, provider);
    }
    public T? Get(IConfigProvider? providerOverride = null)
        => (providerOverride ?? DefaultProvider).Read(key, defaultValue, encrypted);
    public bool HasValue(IConfigProvider? providerOverride = null)
        => (providerOverride ?? DefaultProvider).HasValue(key);
    public static implicit operator T?(ConfigEntry<T> entry) => entry.Get();
}
