using System.Security.Cryptography;
using Sgol.Evidence.Contracts;

namespace Sgol.Evidence.Technical;

public sealed class CryptographicEvidenceObjectKeyFactory : IEvidenceObjectKeyFactory
{
    private readonly Func<int, byte[]> entropy;

    public CryptographicEvidenceObjectKeyFactory()
        : this(RandomNumberGenerator.GetBytes)
    {
    }

    internal CryptographicEvidenceObjectKeyFactory(Func<int, byte[]> entropy) =>
        this.entropy = entropy ?? throw new ArgumentNullException(nameof(entropy));

    public EvidenceObjectKey Create()
    {
        var random = entropy(32);
        if (random.Length != 32)
        {
            throw new InvalidOperationException("The entropy source returned an invalid length.");
        }

        var token = Convert.ToHexStringLower(random);
        return EvidenceObjectKey.Parse($"v1/{token[..2]}/{token[2..4]}/{token}");
    }
}
