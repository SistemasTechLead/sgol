using System.Buffers.Binary;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.DataProtection;
using Sgol.Identity.Contracts;

namespace Sgol.Web.Infrastructure.Persistence.Identity;

internal sealed class TotpSecretProtector(IDataProtectionProvider provider)
{
    private const string Purpose = "SGOL.Identity.Totp.v1";

    public string Protect(Guid userId, string secret) =>
        provider.CreateProtector(Purpose, userId.ToString("D")).Protect(secret);

    public string Unprotect(Guid userId, string protectedSecret) =>
        provider.CreateProtector(Purpose, userId.ToString("D")).Unprotect(protectedSecret);
}

internal static class TotpCodes
{
    private const string Base32Alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";
    private const int PeriodSeconds = 30;

    public static string GenerateSecret() => EncodeBase32(RandomNumberGenerator.GetBytes(20));

    public static string CreateOtpAuthUri(string userName, string secret)
    {
        const string issuer = "SGOL Loretta";
        var label = Uri.EscapeDataString($"{issuer}:{userName}");
        return $"otpauth://totp/{label}?secret={secret}&issuer={Uri.EscapeDataString(issuer)}&algorithm=SHA1&digits=6&period={PeriodSeconds}";
    }

    public static bool TryValidate(
        string secret,
        string? candidate,
        DateTimeOffset now,
        long? lastAcceptedTimeStep,
        out long acceptedTimeStep)
    {
        acceptedTimeStep = 0;
        if (candidate is null || candidate.Length != 6 || candidate.Any(character => character is < '0' or > '9'))
        {
            return false;
        }

        var currentStep = now.ToUnixTimeSeconds() / PeriodSeconds;
        for (var offset = -1; offset <= 1; offset++)
        {
            var step = currentStep + offset;
            if (lastAcceptedTimeStep is long last && step <= last)
            {
                continue;
            }

            var expected = Compute(secret, step);
            if (CryptographicOperations.FixedTimeEquals(
                    Encoding.ASCII.GetBytes(expected),
                    Encoding.ASCII.GetBytes(candidate)))
            {
                acceptedTimeStep = step;
                return true;
            }
        }

        return false;
    }

    public static IReadOnlyList<string> GenerateRecoveryCodes(int count = 10)
    {
        var result = new string[count];
        for (var index = 0; index < count; index++)
        {
            var raw = EncodeBase32(RandomNumberGenerator.GetBytes(13))[..20];
            result[index] = string.Join('-', Enumerable.Range(0, 5).Select(group => raw.Substring(group * 4, 4)));
        }

        return result;
    }

    private static string Compute(string secret, long timeStep)
    {
        var key = DecodeBase32(secret);
        Span<byte> counter = stackalloc byte[8];
        BinaryPrimitives.WriteInt64BigEndian(counter, timeStep);
#pragma warning disable CA5350 // RFC 6238 interoperability requires HMAC-SHA1 for this profile.
        var hash = HMACSHA1.HashData(key, counter);
#pragma warning restore CA5350
        var offset = hash[^1] & 0x0f;
        var binary = ((hash[offset] & 0x7f) << 24) |
            (hash[offset + 1] << 16) |
            (hash[offset + 2] << 8) |
            hash[offset + 3];
        return (binary % 1_000_000).ToString("D6", CultureInfo.InvariantCulture);
    }

    private static string EncodeBase32(ReadOnlySpan<byte> data)
    {
        var output = new StringBuilder((data.Length * 8 + 4) / 5);
        var buffer = 0;
        var bitsLeft = 0;
        foreach (var value in data)
        {
            buffer = (buffer << 8) | value;
            bitsLeft += 8;
            while (bitsLeft >= 5)
            {
                output.Append(Base32Alphabet[(buffer >> (bitsLeft - 5)) & 31]);
                bitsLeft -= 5;
            }
        }

        if (bitsLeft > 0)
        {
            output.Append(Base32Alphabet[(buffer << (5 - bitsLeft)) & 31]);
        }

        return output.ToString();
    }

    private static byte[] DecodeBase32(string value)
    {
        var output = new List<byte>(value.Length * 5 / 8);
        var buffer = 0;
        var bitsLeft = 0;
        foreach (var character in value)
        {
            var digit = Base32Alphabet.IndexOf(char.ToUpperInvariant(character));
            if (digit < 0)
            {
                throw new CryptographicException("The protected TOTP secret is invalid.");
            }

            buffer = (buffer << 5) | digit;
            bitsLeft += 5;
            if (bitsLeft >= 8)
            {
                output.Add((byte)(buffer >> (bitsLeft - 8)));
                bitsLeft -= 8;
            }
        }

        return output.ToArray();
    }
}
