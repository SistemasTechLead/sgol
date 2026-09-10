using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Buffers.Binary;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Sgol.Evidence.Contracts;
using Sgol.Evidence.Technical;
using Sgol.JobInfrastructure;
using Sgol.Testing;
using Sgol.Web.Infrastructure.Evidence;
using Xunit;

namespace Sgol.UnitTests;

public sealed class EvidenceInfrastructureTests
{
    [Fact]
    public void WebEvidenceCompositionRegistersInspectionOutboxHandlerExactlyOnce()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder().Build();
        var environment = new TestHostEnvironment();

        services.AddSgolEvidenceInfrastructure(configuration, environment);
        services.AddSgolEvidenceInfrastructure(configuration, environment);

        var registrations = services.Where(descriptor =>
            descriptor.ServiceType == typeof(IOutboxHandler) &&
            descriptor.ImplementationType == typeof(EvidenceInspectionOutboxHandler));
        Assert.Single(registrations);
    }

    public static TheoryData<byte[], string, string, EvidenceMediaType> ValidFiles => new()
    {
        { EvidenceCorpus.Jpeg(), "image/jpeg", "synthetic.jpg", EvidenceMediaType.Jpeg },
        { EvidenceCorpus.Png(), "image/png", "synthetic.png", EvidenceMediaType.Png },
        { EvidenceCorpus.Pdf(), "application/pdf", "synthetic.pdf", EvidenceMediaType.Pdf }
    };

    private sealed class TestHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Production;
        public string ApplicationName { get; set; } = "Sgol.UnitTests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }

    [Theory]
    [MemberData(nameof(ValidFiles))]
    public async Task ValidatorAcceptsOnlyTheClosedSyntheticCorpus(
        byte[] content,
        string declaredType,
        string fileName,
        EvidenceMediaType expectedType)
    {
        var result = await new FileTechnicalValidator().ValidateAsync(
            new MemoryStream(content), declaredType, fileName, CancellationToken.None);

        Assert.True(result.IsValid);
        Assert.NotNull(result.File);
        await using var file = result.File;
        Assert.Equal(expectedType, file.MediaType);
        Assert.Equal(content.Length, file.SizeBytes);
        Assert.Equal(Convert.ToHexStringLower(SHA256.HashData(content)), file.Sha256);
        Assert.Matches("^[0-9a-f]{64}$", file.Sha256);
    }

    [Theory]
    [InlineData("image/png", "synthetic.jpg")]
    [InlineData("application/pdf", "synthetic.png")]
    [InlineData("image/jpeg", "synthetic.exe")]
    [InlineData("text/plain", "synthetic.pdf")]
    public async Task ValidatorRejectsDeclaredTypeOrExtensionMismatch(string declaredType, string fileName)
    {
        var result = await new FileTechnicalValidator().ValidateAsync(
            new MemoryStream(EvidenceCorpus.Jpeg()), declaredType, fileName,
            CancellationToken.None);

        Assert.False(result.IsValid);
        Assert.Null(result.File);
        Assert.Equal("TYPE_DECLARATION", result.ErrorCode);
    }

    [Fact]
    public async Task ValidatorRejectsEmptyMalformedAndOversizedFiles()
    {
        var validator = new FileTechnicalValidator();
        var empty = await validator.ValidateAsync(new MemoryStream(), "image/png", "x.png",
            CancellationToken.None);
        var malformed = await validator.ValidateAsync(new MemoryStream("%PDF-1.4\n%%EOF"u8.ToArray()),
            "application/pdf", "x.pdf", CancellationToken.None);
        var oversized = await validator.ValidateAsync(
            new RepeatingStream(EvidenceFileLimits.MaximumBytes + 1),
            "image/jpeg",
            "x.jpg",
            CancellationToken.None);

        Assert.Equal("EMPTY", empty.ErrorCode);
        Assert.Equal("STRUCTURE", malformed.ErrorCode);
        Assert.Equal("SIZE_LIMIT", oversized.ErrorCode);
    }

    [Theory]
    [MemberData(nameof(InvalidStructures))]
    public async Task ValidatorRejectsInvalidSignaturesAndTruncatedStructures(
        byte[] content,
        string declaredType,
        string fileName,
        string expectedError)
    {
        var result = await new FileTechnicalValidator().ValidateAsync(
            new MemoryStream(content), declaredType, fileName, CancellationToken.None);

        Assert.False(result.IsValid);
        Assert.Null(result.File);
        Assert.Equal(expectedError, result.ErrorCode);
    }

    public static TheoryData<byte[], string, string, string> InvalidStructures => new()
    {
        { EvidenceCorpus.InvalidSignature(), "image/jpeg", "x.jpg", "TYPE_MISMATCH" },
        { [0xff, 0xd8, 0xff, 0xd9], "image/jpeg", "x.jpg", "STRUCTURE" },
        { [137, 80, 78, 71, 13, 10, 26, 10], "image/png", "x.png", "STRUCTURE" },
        { EvidenceCorpus.PdfWithoutEndMarker(), "application/pdf", "x.pdf", "STRUCTURE" },
        { EvidenceCorpus.PdfWithOutOfRangeXref(), "application/pdf", "x.pdf", "STRUCTURE" }
    };

    [Fact]
    public void ObjectKeysUseOnlyCryptographicOpaqueTokens()
    {
        var values = Enumerable.Range(0, 64)
            .Select(_ => new CryptographicEvidenceObjectKeyFactory().Create().Value)
            .ToArray();

        Assert.Equal(values.Length, values.Distinct(StringComparer.Ordinal).Count());
        Assert.All(values, value => Assert.Matches("^v1/[0-9a-f]{2}/[0-9a-f]{2}/[0-9a-f]{64}$", value));
        Assert.All(values, value => Assert.DoesNotContain("synthetic", value, StringComparison.OrdinalIgnoreCase));
        Assert.All(values, value => Assert.True(EvidenceObjectKey.TryParse(value, out _)));
    }

    [Fact]
    public void ObjectKeyFormatIsDeterministicForInjectedTestEntropy()
    {
        var entropy = Enumerable.Range(0, 32).Select(value => (byte)value).ToArray();
        var key = new CryptographicEvidenceObjectKeyFactory(length =>
        {
            Assert.Equal(32, length);
            return entropy;
        }).Create();

        Assert.Equal(
            "v1/00/01/000102030405060708090a0b0c0d0e0f101112131415161718191a1b1c1d1e1f",
            key.Value);
    }

    [Theory]
    [InlineData("stream: OK", EvidenceScanResult.Limpio)]
    [InlineData("stream: Eicar-Test-Signature FOUND", EvidenceScanResult.Infectado)]
    [InlineData("stream: broken ERROR", EvidenceScanResult.ErrorEscaneo)]
    [InlineData("unknown", EvidenceScanResult.ErrorEscaneo)]
    [InlineData("", EvidenceScanResult.ErrorEscaneo)]
    public void ScannerMapsOnlyClosedProtocolResponses(string response, EvidenceScanResult expected)
    {
        var outcome = ClamAvScanner.ParseResponse(response);
        Assert.Equal(expected, outcome.Result);
        if (expected != EvidenceScanResult.Limpio)
        {
            Assert.NotEqual(EvidenceScanResult.Limpio, outcome.Result);
        }
    }

    [Fact]
    public async Task ScannerUnknownResponseIsNeverClean()
    {
        await using var server = await SyntheticClamServer.StartAsync("unexpected\0");
        var scanner = CreateScanner(server.Port);
        await using var content = new MemoryStream(EvidenceCorpus.Jpeg());

        var result = await scanner.ScanAsync(content, content.Length, CancellationToken.None);

        Assert.Equal(EvidenceScanResult.ErrorEscaneo, result.Result);
        Assert.Equal("PROTOCOL", result.ErrorCode);
        Assert.Equal(1, server.ConnectionCount);
    }

    [Fact]
    public async Task ScannerUnavailableIsErrorAndMakesOneAttempt()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        var scanner = CreateScanner(port);
        await using var content = new MemoryStream(EvidenceCorpus.Jpeg());

        var result = await scanner.ScanAsync(content, content.Length, CancellationToken.None);

        Assert.Equal(EvidenceScanResult.ErrorEscaneo, result.Result);
        Assert.Equal("UNAVAILABLE", result.ErrorCode);
    }

    [Fact]
    public async Task ScannerTimeoutIsErrorAndCancellationIsPropagated()
    {
        await using var server = await SyntheticClamServer.StartAsync("stream: OK\0", TimeSpan.FromSeconds(2));
        var scanner = CreateScanner(server.Port, scanTimeoutSeconds: 1);
        await using var content = new MemoryStream(EvidenceCorpus.Jpeg());

        var timedOut = await scanner.ScanAsync(content, content.Length, CancellationToken.None);

        Assert.Equal(EvidenceScanResult.ErrorEscaneo, timedOut.Result);
        Assert.Equal("TIMEOUT", timedOut.ErrorCode);
        Assert.Equal(1, server.ConnectionCount);

        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        content.Position = 0;
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            scanner.ScanAsync(content, content.Length, cancellation.Token));
    }

    [Fact]
    public async Task PipelinePromotesOnlyCleanObjects()
    {
        var results = new[]
        {
            EvidenceScanResult.Limpio,
            EvidenceScanResult.Infectado,
            EvidenceScanResult.ErrorEscaneo
        };

        foreach (var scanResult in results)
        {
            var storage = new MemoryStorage();
            var pipeline = new EvidenceInspectionPipeline(
                new FileTechnicalValidator(),
                new CryptographicEvidenceObjectKeyFactory(),
                storage,
                new StubScanner(scanResult));

            var receipt = await pipeline.InspectAsync(
                new MemoryStream(EvidenceCorpus.Png()),
                "image/png",
                "safe.png",
                CancellationToken.None);

            Assert.Equal(scanResult, receipt.Result);
            Assert.Equal(scanResult == EvidenceScanResult.Limpio, storage.Promoted);
        }
    }

    [Fact]
    public async Task PipelineReturnsInvalidWithoutCreatingAQuarantineObject()
    {
        var storage = new MemoryStorage();
        var pipeline = new EvidenceInspectionPipeline(
            new FileTechnicalValidator(),
            new CryptographicEvidenceObjectKeyFactory(),
            storage,
            new StubScanner(EvidenceScanResult.Limpio));

        var receipt = await pipeline.InspectAsync(
            new MemoryStream("not-a-pdf"u8.ToArray()),
            "application/pdf",
            "synthetic.pdf",
            CancellationToken.None);

        Assert.Equal(EvidenceScanResult.Invalido, receipt.Result);
        Assert.Null(receipt.Metadata);
        Assert.False(storage.Quarantined);
        Assert.False(storage.Promoted);
    }

    [Theory]
    [InlineData("Production", "http://localhost:8333", true, false)]
    [InlineData("Development", "http://example.com:8333", true, false)]
    [InlineData("Development", "http://127.0.0.1:8333", false, false)]
    [InlineData("Development", "http://127.0.0.1:8333", true, true)]
    [InlineData("Production", "https://s3.example.com", false, true)]
    public void StorageOptionsFailClosed(
        string environment,
        string endpoint,
        bool allowInsecure,
        bool expected)
    {
        var options = new EvidenceStorageOptions
        {
            Endpoint = endpoint,
            Region = "us-east-1",
            QuarantineBucket = "sgol-evidence-quarantine",
            CleanBucket = "sgol-evidence-clean",
            AccessKey = "external-access",
            SecretKey = "external-secret",
            AllowedUploadOrigins = environment == "Production" ? ["https://sgol.example.com"] : ["http://127.0.0.1:5000"],
            AllowInsecureTransport = allowInsecure
        };

        Assert.Equal(expected, EvidenceOptionsValidation.IsStorageValid(options, environment));
    }

    [Fact]
    public void IncompleteStorageAndScannerOptionsFailClosed()
    {
        var storage = new EvidenceStorageOptions
        {
            Endpoint = "https://s3.example.com",
            Region = "us-east-1",
            QuarantineBucket = "sgol-evidence-quarantine",
            CleanBucket = "sgol-evidence-clean",
            AccessKey = "external-access",
            SecretKey = string.Empty,
            AllowedUploadOrigins = ["https://sgol.example.com"]
        };
        var scanner = new EvidenceScannerOptions
        {
            Host = string.Empty,
            Port = 3310,
            ConnectTimeoutSeconds = 3,
            ScanTimeoutSeconds = 30
        };

        Assert.False(EvidenceOptionsValidation.IsStorageValid(storage, "Production"));
        Assert.False(EvidenceOptionsValidation.IsScannerValid(scanner));
    }

    [Theory]
    [InlineData("Production", "http://sgol.example.com")]
    [InlineData("Production", "https://sgol.example.com/path")]
    [InlineData("Production", "https://*.example.com")]
    [InlineData("Development", "http://public.example.com")]
    public void UploadOriginAllowlistRejectsUnsafeValues(string environment, string origin)
    {
        var options = new EvidenceStorageOptions
        {
            Endpoint = "https://s3.example.com",
            Region = "us-east-1",
            QuarantineBucket = "sgol-evidence-quarantine",
            CleanBucket = "sgol-evidence-clean",
            AccessKey = "external-access",
            SecretKey = "external-secret",
            AllowedUploadOrigins = [origin]
        };

        Assert.False(EvidenceOptionsValidation.IsStorageValid(options, environment));
    }

    private static ClamAvScanner CreateScanner(int port, int scanTimeoutSeconds = 30) => new(Options.Create(new EvidenceScannerOptions
    {
        Host = IPAddress.Loopback.ToString(),
        Port = port,
        ConnectTimeoutSeconds = 3,
        ScanTimeoutSeconds = scanTimeoutSeconds
    }));

    private sealed class RepeatingStream(long length) : Stream
    {
        private long position;
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => length;
        public override long Position { get => position; set => throw new NotSupportedException(); }
        public override void Flush() { }
        public override int Read(byte[] buffer, int offset, int count)
        {
            var read = (int)Math.Min(count, length - position);
            Array.Fill<byte>(buffer, 0, offset, read);
            position += read;
            return read;
        }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }

    private sealed class StubScanner(EvidenceScanResult result) : IFileMalwareScanner
    {
        public Task<EvidenceScanOutcome> ScanAsync(Stream validatedContent, long sizeBytes,
            CancellationToken cancellationToken) =>
            Task.FromResult(new EvidenceScanOutcome(result));
        public Task CheckAvailabilityAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class MemoryStorage : IPrivateObjectStorage
    {
        private byte[] bytes = [];
        private EvidenceObjectMetadata metadata = default!;
        public bool Quarantined { get; private set; }
        public bool Promoted { get; private set; }

        public Task<EvidenceUploadAuthorization> CreateQuarantineUploadAuthorizationAsync(
            EvidenceObjectMetadata value, DateTimeOffset expiresAt, CancellationToken cancellationToken) =>
            Task.FromResult(new EvidenceUploadAuthorization(new Uri("https://upload.example.test/object"), expiresAt,
                new EvidenceUploadHeaders(value.MediaType.ToMediaType(), value.SizeBytes.ToString(System.Globalization.CultureInfo.InvariantCulture), "*", value.Sha256,
                    value.MediaType.ToString(), value.SizeBytes.ToString(System.Globalization.CultureInfo.InvariantCulture))));

        public async Task PutQuarantineAsync(EvidenceObjectMetadata value, Stream content,
            CancellationToken cancellationToken)
        {
            Quarantined = true;
            metadata = value;
            using var copy = new MemoryStream();
            await content.CopyToAsync(copy, cancellationToken);
            bytes = copy.ToArray();
        }

        public Task<EvidenceObjectMetadata> GetMetadataAsync(EvidenceStorageArea area, EvidenceObjectKey key,
            CancellationToken cancellationToken) => Task.FromResult(metadata);
        public Task<Stream> OpenReadAsync(EvidenceStorageArea area, EvidenceObjectKey key,
            CancellationToken cancellationToken) => Task.FromResult<Stream>(new MemoryStream(bytes));
        public Task PromoteToCleanAsync(EvidenceObjectMetadata expected, CancellationToken cancellationToken)
        {
            Promoted = true;
            return Task.CompletedTask;
        }
        public Task DeleteAsync(EvidenceStorageArea area, EvidenceObjectKey key,
            CancellationToken cancellationToken) => Task.CompletedTask;
        public Task CheckAvailabilityAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class SyntheticClamServer : IAsyncDisposable
    {
        private readonly TcpListener listener;
        private readonly Task serverTask;
        private int connectionCount;

        private SyntheticClamServer(TcpListener listener, string response, TimeSpan responseDelay)
        {
            this.listener = listener;
            serverTask = ServeAsync(response, responseDelay);
        }

        internal int Port => ((IPEndPoint)listener.LocalEndpoint).Port;
        internal int ConnectionCount => connectionCount;

        internal static Task<SyntheticClamServer> StartAsync(string response, TimeSpan responseDelay = default)
        {
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            return Task.FromResult(new SyntheticClamServer(listener, response, responseDelay));
        }

        private async Task ServeAsync(string response, TimeSpan responseDelay)
        {
            using var client = await listener.AcceptTcpClientAsync();
            Interlocked.Increment(ref connectionCount);
            await using var stream = client.GetStream();
            var command = new byte[10];
            await ReadExactlyAsync(stream, command);
            while (true)
            {
                var lengthBytes = new byte[4];
                await ReadExactlyAsync(stream, lengthBytes);
                var length = BinaryPrimitives.ReadInt32BigEndian(lengthBytes);
                if (length == 0)
                {
                    break;
                }

                await ReadExactlyAsync(stream, new byte[length]);
            }
            if (responseDelay > TimeSpan.Zero)
            {
                await Task.Delay(responseDelay);
            }
            try
            {
                await stream.WriteAsync(System.Text.Encoding.ASCII.GetBytes(response));
            }
            catch (IOException)
            {
                // A timeout test deliberately closes the client before the delayed response.
            }
        }

        private static async Task ReadExactlyAsync(Stream stream, Memory<byte> destination)
        {
            var read = 0;
            while (read < destination.Length)
            {
                var current = await stream.ReadAsync(destination[read..]);
                if (current == 0)
                {
                    throw new EndOfStreamException();
                }

                read += current;
            }
        }

        public async ValueTask DisposeAsync()
        {
            listener.Stop();
            await serverTask;
        }
    }
}
