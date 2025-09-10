using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Security.Cryptography;
using System.Text;
using WebhookService.Core.Interfaces;

namespace WebhookService.Infrastructure.Services;

/// <summary>
/// خدمة التوقيع والتشفير للويب هوك
/// Webhook signature and encryption service
/// </summary>
public class SignatureService : ISignatureService
{
    private readonly string _encryptionKey;
    private readonly ILogger<SignatureService> _logger;
    private const int MaxTimeDriftSeconds = 300; // ±5 minutes

    public SignatureService(IConfiguration configuration, ILogger<SignatureService> logger)
    {
        _encryptionKey = configuration["Security:EncryptionKey"] ?? 
            throw new InvalidOperationException("مفتاح التشفير غير محدد - Encryption key not configured");
        _logger = logger;
    }

    /// <summary>
    /// توليد التوقيع الرقمي للويب هوك
    /// Generate webhook signature
    /// Format: version + ":" + timestamp + ":" + eventId + ":" + SHA256(lowercase-hex(body))
    /// </summary>
    public string GenerateSignature(string version, long timestamp, Guid eventId, string body, string secret)
    {
        try
        {
            // حساب هاش SHA256 للمحتوى - Calculate SHA256 hash of body
            var bodyHash = ComputeSha256Hash(body);
            
            // بناء النص المراد توقيعه - Build text to sign
            var textToSign = $"{version}:{timestamp}:{eventId}:{bodyHash}";
            
            // حساب HMAC-SHA256 - Calculate HMAC-SHA256
            var signature = ComputeHmacSha256(textToSign, secret);
            
            _logger.LogDebug("تم توليد التوقيع للحدث {EventId}", eventId);
            
            return signature;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "خطأ في توليد التوقيع للحدث {EventId}", eventId);
            throw;
        }
    }

    /// <summary>
    /// التحقق من صحة التوقيع الرقمي
    /// Validate webhook signature
    /// </summary>
    public bool ValidateSignature(string signature, string version, long timestamp, Guid eventId, string body, string secret)
    {
        try
        {
            // التحقق من انحراف الوقت - Check time drift
            var currentTimestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var timeDrift = Math.Abs(currentTimestamp - timestamp);
            
            if (timeDrift > MaxTimeDriftSeconds)
            {
                _logger.LogWarning("انحراف الوقت كبير جداً: {TimeDrift} ثانية للحدث {EventId}", 
                    timeDrift, eventId);
                return false;
            }

            // توليد التوقيع المتوقع - Generate expected signature
            var expectedSignature = GenerateSignature(version, timestamp, eventId, body, secret);
            
            // مقارنة آمنة للتوقيعات - Secure signature comparison
            var isValid = SecureCompare(signature, expectedSignature);
            
            if (!isValid)
            {
                _logger.LogWarning("التوقيع غير صحيح للحدث {EventId}", eventId);
            }
            
            return isValid;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "خطأ في التحقق من التوقيع للحدث {EventId}", eventId);
            return false;
        }
    }

    /// <summary>
    /// تشفير المفتاح السري
    /// Encrypt secret key
    /// </summary>
    public string EncryptSecret(string secret)
    {
        try
        {
            using var aes = Aes.Create();
            aes.Key = DeriveKey(_encryptionKey);
            aes.GenerateIV();

            using var encryptor = aes.CreateEncryptor();
            var secretBytes = Encoding.UTF8.GetBytes(secret);
            var encryptedBytes = encryptor.TransformFinalBlock(secretBytes, 0, secretBytes.Length);
            
            // دمج IV مع البيانات المشفرة - Combine IV with encrypted data
            var result = new byte[aes.IV.Length + encryptedBytes.Length];
            Array.Copy(aes.IV, 0, result, 0, aes.IV.Length);
            Array.Copy(encryptedBytes, 0, result, aes.IV.Length, encryptedBytes.Length);
            
            return Convert.ToBase64String(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "خطأ في تشفير المفتاح السري");
            throw;
        }
    }

    /// <summary>
    /// فك تشفير المفتاح السري
    /// Decrypt secret key
    /// </summary>
    public string DecryptSecret(string encryptedSecret)
    {
        try
        {
            var encryptedData = Convert.FromBase64String(encryptedSecret);
            
            using var aes = Aes.Create();
            aes.Key = DeriveKey(_encryptionKey);
            
            // استخراج IV من البيانات - Extract IV from data
            var iv = new byte[aes.IV.Length];
            var cipherText = new byte[encryptedData.Length - iv.Length];
            
            Array.Copy(encryptedData, 0, iv, 0, iv.Length);
            Array.Copy(encryptedData, iv.Length, cipherText, 0, cipherText.Length);
            
            aes.IV = iv;
            
            using var decryptor = aes.CreateDecryptor();
            var decryptedBytes = decryptor.TransformFinalBlock(cipherText, 0, cipherText.Length);
            
            return Encoding.UTF8.GetString(decryptedBytes);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "خطأ في فك تشفير المفتاح السري");
            throw;
        }
    }

    /// <summary>
    /// حساب هاش SHA256 للنص
    /// Calculate SHA256 hash of text
    /// </summary>
    private static string ComputeSha256Hash(string text)
    {
        using var sha256 = SHA256.Create();
        var bytes = Encoding.UTF8.GetBytes(text);
        var hashBytes = sha256.ComputeHash(bytes);
        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }

    /// <summary>
    /// حساب HMAC-SHA256
    /// Calculate HMAC-SHA256
    /// </summary>
    private static string ComputeHmacSha256(string text, string secret)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var bytes = Encoding.UTF8.GetBytes(text);
        var hashBytes = hmac.ComputeHash(bytes);
        return Convert.ToBase64String(hashBytes);
    }

    /// <summary>
    /// مقارنة آمنة للنصوص لتجنب هجمات التوقيت
    /// Secure string comparison to prevent timing attacks
    /// </summary>
    private static bool SecureCompare(string a, string b)
    {
        if (a.Length != b.Length)
            return false;

        var result = 0;
        for (int i = 0; i < a.Length; i++)
        {
            result |= a[i] ^ b[i];
        }
        
        return result == 0;
    }

    /// <summary>
    /// اشتقاق مفتاح التشفير من النص
    /// Derive encryption key from text
    /// </summary>
    private static byte[] DeriveKey(string password)
    {
        using var sha256 = SHA256.Create();
        return sha256.ComputeHash(Encoding.UTF8.GetBytes(password));
    }
}
