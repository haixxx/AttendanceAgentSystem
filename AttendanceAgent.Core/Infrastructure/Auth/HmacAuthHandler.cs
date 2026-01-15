using System.Security.Cryptography;
using System.Text;

namespace AttendanceAgent.Core.Infrastructure.Auth;

public class HmacAuthHandler
{
    private readonly string _clientId;
    private readonly byte[] _secretBytes;

    public HmacAuthHandler(string clientId, string secretKey)
    {
        _clientId = clientId ?? throw new ArgumentNullException(nameof(clientId));

        if (string.IsNullOrWhiteSpace(secretKey))
            throw new ArgumentException("Secret key cannot be empty", nameof(secretKey));

        _secretBytes = Encoding.UTF8.GetBytes(secretKey);
    }

    /// <summary>
    /// Generates HMAC Authorization header value
    /// Format:  HMAC {client_id}:{epoch_seconds}:{signature_hex}
    /// </summary>
    public string GenerateAuthHeader(string jsonBody)
    {
        // 1. Get epoch seconds (UTC)
        var epochSeconds = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        // 2. Prepare message:  body_bytes + timestamp_bytes
        var bodyBytes = Encoding.UTF8.GetBytes(jsonBody);
        var tsBytes = Encoding.UTF8.GetBytes(epochSeconds.ToString());

        var message = new byte[bodyBytes.Length + tsBytes.Length];
        Buffer.BlockCopy(bodyBytes, 0, message, 0, bodyBytes.Length);
        Buffer.BlockCopy(tsBytes, 0, message, bodyBytes.Length, tsBytes.Length);

        // 3. Compute HMAC-SHA256
        using var hmac = new HMACSHA256(_secretBytes);
        var hash = hmac.ComputeHash(message);
        var sigHex = BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();

        // 4. Return header value
        return $"HMAC {_clientId}:{epochSeconds}:{sigHex}";
    }
}