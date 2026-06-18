using System.Security;
using System.Security.Cryptography;

namespace MeloongCore;
public static class CryptographyUtils {

    #region Hash

    public enum HashMethod {
        /// <summary>
        /// 使用 <see cref="MD5"/> 算法。在 16 进制下哈希为 32 长度字符串。
        /// </summary>
        Md5,
        /// <summary>
        /// 使用 <see cref="SHA1"/> 算法。在 16 进制下哈希为 40 长度字符串。
        /// </summary>
        Sha1,
        /// <summary>
        /// 使用 <see cref="SHA256"/> 算法。在 16 进制下哈希为 64 长度字符串。
        /// </summary>
        Sha256,
        /// <summary>
        /// 使用 <see cref="SHA512"/> 算法。在 16 进制下哈希为 128 长度字符串。
        /// </summary>
        Sha512
    }
    private static HashAlgorithm GetHashAlgorithm(HashMethod method) => method switch {
        HashMethod.Md5 => MD5.Create(),
        HashMethod.Sha1 => SHA1.Create(),
        HashMethod.Sha256 => SHA256.Create(),
        HashMethod.Sha512 => SHA512.Create(),
        _ => throw new ArgumentOutOfRangeException(nameof(method))
    };

    /// <summary>
    /// 计算文件的 Hash。返回 16 进制小写字符串。
    /// </summary>
    public static string ComputeFileHash(string filePath, HashMethod method = HashMethod.Md5) {
        using HashAlgorithm hashImpl = GetHashAlgorithm(method);
        Logger.Trace($"计算文件 {method}：{filePath}");
        using var file = FileUtils.ReadAsStream(filePath);
        return BitConverter.ToString(hashImpl.ComputeHash(file)).Replace("-", "").Lower();
    }

    /// <summary>
    /// 计算字节数组的 Hash。返回 16 进制小写字符串。
    /// </summary>
    public static string ComputeHash(byte[] input, HashMethod method = HashMethod.Md5) {
        using HashAlgorithm hashImpl = GetHashAlgorithm(method);
        return BitConverter.ToString(hashImpl.ComputeHash(input)).Replace("-", "").Lower();
    }
    /// <summary>
    /// 计算字符串的 Hash。返回 16 进制小写字符串。
    /// <para/> 使用 UTF-8 将字符串转换为字节数组。
    /// </summary>
    public static string ComputeHash(string input, HashMethod method = HashMethod.Md5) 
        => ComputeHash(Encoding.UTF8.GetBytes(input), method);

    #endregion

    #region ECDSA

    /// <summary>
    /// 进行 ECDSA P-256 非对称加密签名验证。
    /// 如果验证失败则抛出 <see cref="CryptographicException"/>。
    /// </summary>
    public static void EcdsaVerify(string sourceString, string sign, string publicKey) {
        using var sha256 = GetHashAlgorithm(HashMethod.Sha256);
        byte[] hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(sourceString));
        IntPtr algorithmHandle = IntPtr.Zero;
        IntPtr keyHandle = IntPtr.Zero;
        // 直接调用 DLL，以避免 .NET API 依赖的系统服务存在问题
        try {
            int status = BCryptOpenAlgorithmProvider(out algorithmHandle, "ECDSA_P256", null, 0);
            if (status < 0) throw new CryptographicException($"{nameof(BCryptOpenAlgorithmProvider)} 失败，错误码 {status}");
            status = BCryptImportKeyPair(algorithmHandle, IntPtr.Zero, "ECCPUBLICBLOB", out keyHandle, Convert.FromBase64String(publicKey), 72, 0);
            if (status < 0) throw new CryptographicException($"{nameof(BCryptImportKeyPair)} 失败，错误码 {status}");
            status = BCryptVerifySignature(keyHandle, IntPtr.Zero, hash, hash.Length, Convert.FromBase64String(sign), 64, 0);
            if (status == unchecked((int) 0xC000A000)) throw new SecurityException("签名验证失败");
            if (status < 0) throw new CryptographicException($"{nameof(BCryptVerifySignature)} 失败，错误码 {status}");
        } finally {
            if (keyHandle != IntPtr.Zero) BCryptDestroyKey(keyHandle);
            if (algorithmHandle != IntPtr.Zero) BCryptCloseAlgorithmProvider(algorithmHandle, 0);
        }
    }

    /// <summary>
    /// 使用 ECDSA 算法，为一个双方均知晓的字符串生成签名。
    /// </summary>
    public static string EcdsaSign(string sourceString, string privateKey) {
        using var sha256 = GetHashAlgorithm(HashMethod.Sha256);
        byte[] hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(sourceString));
        byte[] privateKeyBytes = Convert.FromBase64String(privateKey);
        IntPtr algorithmHandle = IntPtr.Zero;
        IntPtr keyHandle = IntPtr.Zero;
        // 直接调用 DLL，避免 .NET API 依赖的系统服务存在问题
        try {
            int status = BCryptOpenAlgorithmProvider(out algorithmHandle, "ECDSA_P256", null, 0);
            if (status < 0) throw new CryptographicException($"{nameof(BCryptOpenAlgorithmProvider)} 失败，错误码 {status}");
            status = BCryptImportKeyPair(algorithmHandle, IntPtr.Zero, "ECCPRIVATEBLOB", out keyHandle, privateKeyBytes, privateKeyBytes.Length, 0);
            if (status < 0) throw new CryptographicException($"{nameof(BCryptImportKeyPair)} 失败，错误码 {status}");
            status = BCryptSignHash(keyHandle, IntPtr.Zero, hash, hash.Length, null, 0, out int signSize, 0);
            if (status < 0) throw new CryptographicException($"{nameof(BCryptSignHash)} 失败，错误码 {status}");
            byte[] sign = new byte[signSize];
            status = BCryptSignHash(keyHandle, IntPtr.Zero, hash, hash.Length, sign, sign.Length, out int actualSignSize, 0);
            if (status < 0) throw new CryptographicException($"{nameof(BCryptSignHash)} 失败，错误码 {status}");
            if (actualSignSize != sign.Length) Array.Resize(ref sign, actualSignSize);
            return Convert.ToBase64String(sign);
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
    [DllImport("bcrypt.dll")] private static extern int BCryptSignHash(IntPtr keyHandle, IntPtr paddingInfo, byte[] hash, int hashSize, byte[]? signature, int signatureSize, out int resultSize, int flags);

    #endregion

}
