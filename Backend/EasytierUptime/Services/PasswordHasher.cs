using System.Security.Cryptography;

namespace EasytierUptime.Services;

/// <summary>
/// 提供密码哈希与验证功能的工具类。
/// </summary>
public static class PasswordHasher
{
    /// <summary>
    /// 计算指定密码的哈希值。
    /// </summary>
    public static string Hash(string password)
    {
        const int iter = 100_000;
        const int saltSize = 16;
        const int keySize = 32;
        Span<byte> salt = stackalloc byte[saltSize];
        RandomNumberGenerator.Fill(salt);
        var hash = Rfc2898DeriveBytes.Pbkdf2(password, salt.ToArray(), iter, HashAlgorithmName.SHA256, keySize);
        return $"1${iter}${Convert.ToBase64String(salt)}${Convert.ToBase64String(hash)}";
    }

    /// <summary>
    /// 验证密码与存储哈希是否匹配。
    /// </summary>
    public static bool Verify(string password, string stored)
    {
        var parts = stored.Split('$');
        if (parts.Length != 4) return false;
        if (!int.TryParse(parts[1], out var iter)) return false;
        var salt = Convert.FromBase64String(parts[2]);
        var hash = Convert.FromBase64String(parts[3]);
        var actual = Rfc2898DeriveBytes.Pbkdf2(password, salt, iter, HashAlgorithmName.SHA256, hash.Length);
        return CryptographicOperations.FixedTimeEquals(hash, actual);
    }
}
