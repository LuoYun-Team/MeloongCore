using System.Security.Cryptography;

namespace MeloongCore;
public static class HashUtils {
    public enum HashMethod { Md5, Sha1, Sha256, Sha512 }

    /// <summary>
    /// 计算文件的哈希值（十六进制小写）。
    /// </summary>
    public static string ComputeFromFile(string filePath, HashMethod method) {
        using var file = FileUtils.ReadAsStream(filePath);
        using HashAlgorithm hashImpl = method switch {
            HashMethod.Md5 => MD5.Create(),
            HashMethod.Sha1 => SHA1.Create(),
            HashMethod.Sha256 => SHA256.Create(),
            HashMethod.Sha512 => SHA512.Create(),
            _ => throw new ArgumentOutOfRangeException(nameof(method))
        };
        return BitConverter.ToString(hashImpl.ComputeHash(file)).Replace("-", "").ToLower();
    }

}
