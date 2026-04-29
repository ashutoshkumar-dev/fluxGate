using System.Net.Http.Json;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace Gateway.Infrastructure.Security;

/// <summary>
/// Fetches the RS256 public key from the Auth Service JWKS endpoint and caches it.
/// The key is refreshed on startup and every 6 hours.
/// </summary>
public sealed class JwksService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IMemoryCache _cache;
    private readonly string _jwksUrl;

    private const string CacheKey = "jwks_signing_keys";
    private static readonly TimeSpan CacheDuration = TimeSpan.FromHours(6);

    public JwksService(IHttpClientFactory httpClientFactory, IMemoryCache cache, string jwksUrl)
    {
        _httpClientFactory = httpClientFactory;
        _cache = cache;
        _jwksUrl = jwksUrl;
    }

    /// <summary>
    /// Returns cached signing keys, fetching from the JWKS endpoint if the cache is empty or stale.
    /// </summary>
    public async Task<IEnumerable<SecurityKey>> GetSigningKeysAsync(CancellationToken ct = default)
    {
        if (_cache.TryGetValue(CacheKey, out IEnumerable<SecurityKey>? cached) && cached is not null)
            return cached;

        return await RefreshAsync(ct);
    }

    /// <summary>Forces a refresh from the JWKS endpoint and updates the cache.</summary>
    public async Task<IEnumerable<SecurityKey>> RefreshAsync(CancellationToken ct = default)
    {
        var client = _httpClientFactory.CreateClient("jwks");
        var jwks = await client.GetFromJsonAsync<JwksDocument>(_jwksUrl, ct)
            ?? throw new InvalidOperationException($"JWKS endpoint returned null: {_jwksUrl}");

        var keys = new List<SecurityKey>();
        foreach (var key in jwks.Keys)
        {
            if (key.Kty != "RSA") continue;

            var rsaParams = new System.Security.Cryptography.RSAParameters
            {
                Modulus = Base64UrlEncoder.DecodeBytes(key.N),
                Exponent = Base64UrlEncoder.DecodeBytes(key.E)
            };

            var rsa = System.Security.Cryptography.RSA.Create();
            rsa.ImportParameters(rsaParams);

            keys.Add(new RsaSecurityKey(rsa) { KeyId = key.Kid });
        }

        _cache.Set(CacheKey, (IEnumerable<SecurityKey>)keys, CacheDuration);
        return keys;
    }

    // ── JSON model ──────────────────────────────────────────────────────────

    private sealed record JwksDocument(JwksKey[] Keys);
    private sealed record JwksKey(string Kty, string Use, string Kid, string Alg, string N, string E);
}
