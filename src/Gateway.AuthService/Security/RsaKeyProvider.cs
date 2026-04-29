using System.Security.Cryptography;

namespace Gateway.AuthService.Security;

/// <summary>
/// Generates and holds the RSA key pair for the lifetime of the application.
/// In dev: generated fresh on startup.
/// In prod: load from file/vault via configuration.
/// </summary>
public class RsaKeyProvider
{
    private readonly RSA _rsa;

    public string KeyId { get; }

    public RsaKeyProvider()
    {
        _rsa = RSA.Create(2048);
        KeyId = Guid.NewGuid().ToString("N")[..12];
    }

    public RSA GetSigningKey() => _rsa;

    public RSAParameters GetPublicParameters()
        => _rsa.ExportParameters(includePrivateParameters: false);
}
