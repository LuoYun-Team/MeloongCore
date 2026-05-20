using System.Security;
using System.Security.Cryptography;

namespace MeloongCore;
public static class CryptographyUtils {

    #region Hash

    public enum HashMethod { Md5, Sha1, Sha256, Sha512 }
    private static HashAlgorithm GetHashAlgorithm(HashMethod method) => method switch {
        HashMethod.Md5 => MD5.Create(),
        HashMethod.Sha1 => SHA1.Create(),
        HashMethod.Sha256 => SHA256.Create(),
        HashMethod.Sha512 => SHA512.Create(),
        _ => throw new ArgumentOutOfRangeException(nameof(method))
    };

    /// <summary>
    /// 计算文件的 Hash。返回十六进制小写字符串。
    /// </summary>
    public static string ComputeFileHash(string filePath, HashMethod method) {
        using HashAlgorithm hashImpl = GetHashAlgorithm(method);
        Logger.Trace($"计算文件 {method}：{filePath}");
        using var file = FileUtils.ReadAsStream(filePath);
        return BitConverter.ToString(hashImpl.ComputeHash(file)).Replace("-", "").ToLower();
    }

    /// <summary>
    /// 计算字节数组的 Hash。返回十六进制小写字符串。
    /// </summary>
    public static string ComputeBytesHash(byte[] input, HashMethod method) {
        using HashAlgorithm hashImpl = GetHashAlgorithm(method);
        return BitConverter.ToString(hashImpl.ComputeHash(input)).Replace("-", "").ToLower();
    }

    /// <summary>
    /// 计算字符串的 Hash。返回十六进制小写字符串。
    /// 使用 UTF-8 将字符串转换为字节数组。
    /// </summary>
    public static string ComputeStringHash(string input, HashMethod method) 
        => ComputeBytesHash(Encoding.UTF8.GetBytes(input), method);

    #endregion

    #region ECDSA

    /// <summary>
    /// 进行 ECDSA P-256 签名验证。如果失败则抛出异常。
    /// </summary>
    public static void EcdsaVerify(string sourceString, string sign,
        string publicKeyBase64 = "RUNTMSAAAAC4QTUNAewh23Q4Q6koHkyIrDIIZUSbua23sf2DiZmIRwSzadISDRyTVTbuWniH3KR7rKj8XBsabms1be6i3c+S") {
        // 使用 Windows CNG API 直接处理原始公钥和签名数据，以避免 .NET API 的兼容性问题
        using var sha256 = GetHashAlgorithm(HashMethod.Sha256);
        byte[] hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(sourceString));
        IntPtr algorithmHandle = IntPtr.Zero;
        IntPtr keyHandle = IntPtr.Zero;
        try {
            int status = BCryptOpenAlgorithmProvider(out algorithmHandle, "ECDSA_P256", null, 0);
            if (status < 0) throw new CryptographicException($"{nameof(BCryptOpenAlgorithmProvider)} 失败，错误码 {status}");
            status = BCryptImportKeyPair(algorithmHandle, IntPtr.Zero, "ECCPUBLICBLOB", out keyHandle, Convert.FromBase64String(publicKeyBase64), 72, 0);
            if (status < 0) throw new CryptographicException($"{nameof(BCryptImportKeyPair)} 失败，错误码 {status}");
            status = BCryptVerifySignature(keyHandle, IntPtr.Zero, hash, hash.Length, Convert.FromBase64String(sign), 64, 0);
            if (status == unchecked((int) 0xC000A000)) throw new SecurityException("签名验证失败");
            if (status < 0) throw new CryptographicException($"{nameof(BCryptVerifySignature)} 失败，错误码 {status}");
        } finally {
            if (keyHandle != IntPtr.Zero) BCryptDestroyKey(keyHandle);
            if (algorithmHandle != IntPtr.Zero) BCryptCloseAlgorithmProvider(algorithmHandle, 0);
        }
    }
    [DllImport("bcrypt.dll", CharSet = CharSet.Unicode)] private static extern int BCryptOpenAlgorithmProvider(out IntPtr algorithmHandle, string algorithmId, string? implementation, int flags);
    [DllImport("bcrypt.dll", CharSet = CharSet.Unicode)] private static extern int BCryptImportKeyPair(IntPtr algorithmHandle, IntPtr importKey, string blobType, out IntPtr keyHandle, byte[] input, int inputSize, int flags);
    [DllImport("bcrypt.dll")] private static extern int BCryptVerifySignature(IntPtr keyHandle, IntPtr paddingInfo, byte[] hash, int hashSize, byte[] signature, int signatureSize, int flags);
    [DllImport("bcrypt.dll")] private static extern int BCryptDestroyKey(IntPtr keyHandle);
    [DllImport("bcrypt.dll")] private static extern int BCryptCloseAlgorithmProvider(IntPtr algorithmHandle, int flags);

    #endregion

}
