using System.ComponentModel.DataAnnotations;

namespace Sgol.Web.Infrastructure.Evidence;

public sealed class EvidenceStorageOptions
{
    public const string SectionName = "Evidence:Storage";

    [Required]
    public string Endpoint { get; init; } = string.Empty;

    [Required]
    public string Region { get; init; } = string.Empty;

    [Required]
    public string QuarantineBucket { get; init; } = string.Empty;

    [Required]
    public string CleanBucket { get; init; } = string.Empty;

    [Required]
    public string AccessKey { get; init; } = string.Empty;

    [Required]
    public string SecretKey { get; init; } = string.Empty;

    public bool AllowInsecureTransport { get; init; }
}

public sealed class EvidenceScannerOptions
{
    public const string SectionName = "Evidence:Scanner";

    [Required]
    public string Host { get; init; } = string.Empty;

    [Range(1, 65535)]
    public int Port { get; init; } = 3310;

    [Range(3, 3)]
    public int ConnectTimeoutSeconds { get; init; } = 3;

    [Range(30, 30)]
    public int ScanTimeoutSeconds { get; init; } = 30;
}

internal static class EvidenceOptionsValidation
{
    internal static bool IsStorageValid(EvidenceStorageOptions options, string environmentName)
    {
        if (!Uri.TryCreate(options.Endpoint, UriKind.Absolute, out var endpoint) ||
            endpoint.Scheme is not ("https" or "http") ||
            string.IsNullOrWhiteSpace(options.Region) ||
            string.IsNullOrWhiteSpace(options.AccessKey) ||
            string.IsNullOrWhiteSpace(options.SecretKey) ||
            !IsBucketName(options.QuarantineBucket) ||
            !IsBucketName(options.CleanBucket) ||
            string.Equals(options.QuarantineBucket, options.CleanBucket, StringComparison.Ordinal))
        {
            return false;
        }

        if (endpoint.Scheme == Uri.UriSchemeHttps)
        {
            return true;
        }

        return options.AllowInsecureTransport &&
               environmentName is ("Development" or "CI") &&
               IsPrivateEndpoint(endpoint.Host);
    }

    internal static bool IsScannerValid(EvidenceScannerOptions options) =>
        !string.IsNullOrWhiteSpace(options.Host) &&
        options.Port is >= 1 and <= 65535 &&
        options.ConnectTimeoutSeconds == 3 &&
        options.ScanTimeoutSeconds == 30;

    private static bool IsBucketName(string value)
    {
        if (value.Length is < 3 or > 63 || value[0] is '.' or '-' || value[^1] is '.' or '-')
        {
            return false;
        }

        return value.All(character => character is >= 'a' and <= 'z' or >= '0' and <= '9' or '.' or '-');
    }

    private static bool IsPrivateEndpoint(string host)
    {
        if (host.Equals("localhost", StringComparison.OrdinalIgnoreCase) ||
            host.Equals("sgol-evidence-s3", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (!System.Net.IPAddress.TryParse(host, out var address))
        {
            return !host.Contains('.', StringComparison.Ordinal);
        }

        var bytes = address.GetAddressBytes();
        return System.Net.IPAddress.IsLoopback(address) ||
               bytes is [10, _, _, _] ||
               bytes is [172, >= 16 and <= 31, _, _] ||
               bytes is [192, 168, _, _];
    }
}
