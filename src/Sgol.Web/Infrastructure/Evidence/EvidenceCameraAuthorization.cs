using Microsoft.AspNetCore.DataProtection;
using System.Security.Cryptography;
using System.Text.Json;

namespace Sgol.Web.Infrastructure.Evidence;

public sealed record EvidenceCameraGrant(Guid Id, Guid Actor, Guid Obligation, Guid Requirement, DateTimeOffset ExpiresAt);
public sealed class EvidenceCameraAuthorization(IDataProtectionProvider provider)
{
    private readonly IDataProtector protector = provider.CreateProtector("SGOL.camera-session.v1");
    public string Issue(EvidenceCameraGrant grant) => protector.Protect(JsonSerializer.Serialize(grant));
    public EvidenceCameraGrant? Read(string? value, Guid actor, Guid obligation, Guid requirement, DateTimeOffset now)
    {
        if (string.IsNullOrEmpty(value) || value.Length > 4096) return null;
        try
        {
            var grant = JsonSerializer.Deserialize<EvidenceCameraGrant>(protector.Unprotect(value));
            return grant is not null && grant.Id != Guid.Empty && grant.Actor == actor && grant.Obligation == obligation &&
                grant.Requirement == requirement && grant.ExpiresAt > now ? grant : null;
        }
        catch (Exception e) when (e is CryptographicException or JsonException) { return null; }
    }
}
