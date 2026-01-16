using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;

namespace AttendanceAgent.Core.Infrastructure.Auth;

public class HmacAuthHandler : DelegatingHandler
{
    private readonly string _clientId;
    private readonly string _secretKey;
    private readonly ILogger<HmacAuthHandler>? _logger;

    public HmacAuthHandler(string clientId, string secretKey, ILogger<HmacAuthHandler>? logger = null)
    {
        if (string.IsNullOrEmpty(clientId))
            throw new ArgumentNullException(nameof(clientId));
        if (string.IsNullOrEmpty(secretKey))
            throw new ArgumentNullException(nameof(secretKey));

        _clientId = clientId;
        _secretKey = secretKey;
        _logger = logger;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        // Only add HMAC for /raw-events/ endpoints
        if (request.RequestUri?.PathAndQuery.Contains("/raw-events/") == true)
        {
            var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

            byte[] bodyBytes = Array.Empty<byte>();
            if (request.Content != null)
            {
                bodyBytes = await request.Content.ReadAsByteArrayAsync(cancellationToken);
            }

            var signature = ComputeHmacSignature(bodyBytes, timestamp, _secretKey);

            // Format:  Authorization:  HMAC client_id:timestamp:signature
            var authValue = $"HMAC {_clientId}:{timestamp}:{signature}";

            // CRITICAL: Remove existing Authorization header first! 
            request.Headers.Remove("Authorization");

            // Then add HMAC
            request.Headers.TryAddWithoutValidation("Authorization", authValue);

            _logger?.LogDebug("HMAC Auth - ClientId: {ClientId}, Timestamp: {Timestamp}, Signature:  {Signature}",
                _clientId, timestamp, signature.Substring(0, 16) + "...");
        }

        return await base.SendAsync(request, cancellationToken);
    }

    private static string ComputeHmacSignature(byte[] bodyBytes, long timestamp, string secret)
    {
        // message = body_bytes + timestamp_str.encode('utf-8')
        var timestampBytes = Encoding.UTF8.GetBytes(timestamp.ToString());
        var messageBytes = new byte[bodyBytes.Length + timestampBytes.Length];

        Array.Copy(bodyBytes, 0, messageBytes, 0, bodyBytes.Length);
        Array.Copy(timestampBytes, 0, messageBytes, bodyBytes.Length, timestampBytes.Length);

        // HMAC-SHA256
        var keyBytes = Encoding.UTF8.GetBytes(secret);
        using var hmac = new HMACSHA256(keyBytes);
        var hash = hmac.ComputeHash(messageBytes);

        // Return hex lowercase
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}