using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;

namespace AttendanceAgent.Core.Infrastructure.Auth;

public class HmacAuthHandler : DelegatingHandler
{
    private readonly string _clientId;
    private readonly string _secretKey;

    public HmacAuthHandler(string clientId, string secretKey)
    {
        if (string.IsNullOrEmpty(clientId))
            throw new ArgumentNullException(nameof(clientId));
        if (string.IsNullOrEmpty(secretKey))
            throw new ArgumentNullException(nameof(secretKey));

        _clientId = clientId;
        _secretKey = secretKey;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
        var nonce = Guid.NewGuid().ToString("N");

        string contentHash = string.Empty;
        if (request.Content != null)
        {
            var content = await request.Content.ReadAsByteArrayAsync(cancellationToken);
            contentHash = Convert.ToBase64String(SHA256.HashData(content));
        }

        var method = request.Method.Method.ToUpperInvariant();
        var path = request.RequestUri?.PathAndQuery ?? "/";
        var signatureData = $"{_clientId}:{method}:{path}:{timestamp}:{nonce}:{contentHash}";

        var signature = ComputeHmacSignature(signatureData, _secretKey);

        request.Headers.Add("X-Client-Id", _clientId);
        request.Headers.Add("X-Timestamp", timestamp);
        request.Headers.Add("X-Nonce", nonce);
        request.Headers.Add("X-Signature", signature);

        if (!string.IsNullOrEmpty(contentHash))
        {
            request.Headers.Add("X-Content-Hash", contentHash);
        }

        return await base.SendAsync(request, cancellationToken);
    }

    private static string ComputeHmacSignature(string data, string key)
    {
        var keyBytes = Encoding.UTF8.GetBytes(key);
        var dataBytes = Encoding.UTF8.GetBytes(data);

        using var hmac = new HMACSHA256(keyBytes);
        var hash = hmac.ComputeHash(dataBytes);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}