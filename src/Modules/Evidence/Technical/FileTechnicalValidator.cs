using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Sgol.Evidence.Contracts;

namespace Sgol.Evidence.Technical;

public sealed partial class FileTechnicalValidator : IFileTechnicalValidator
{
    private const int MaximumDimension = 20_000;
    private const long MaximumPixels = 40_000_000;
    private const int PdfTailBytes = 1_024;
    private static readonly uint[] CrcTable = BuildCrcTable();

    public async Task<FileTechnicalValidationResult> ValidateAsync(
        Stream content,
        string declaredMediaType,
        string originalFileName,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(content);

        if (!TryResolveExpectedType(declaredMediaType, originalFileName, out var expectedType))
        {
            return FileTechnicalValidationResult.Invalid("TYPE_DECLARATION");
        }

        var temporaryPath = Path.Combine(Path.GetTempPath(), $"sgol-evidence-{Guid.NewGuid():N}.tmp");
        var temporary = new FileStream(
            temporaryPath,
            FileMode.CreateNew,
            FileAccess.ReadWrite,
            FileShare.None,
            EvidenceFileLimits.BufferBytes,
            FileOptions.Asynchronous | FileOptions.SequentialScan | FileOptions.DeleteOnClose);

        try
        {
            using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            var buffer = new byte[EvidenceFileLimits.BufferBytes];
            long size = 0;
            while (true)
            {
                var read = await content.ReadAsync(buffer, cancellationToken);
                if (read == 0)
                {
                    break;
                }

                size += read;
                if (size > EvidenceFileLimits.MaximumBytes)
                {
                    await temporary.DisposeAsync();
                    return FileTechnicalValidationResult.Invalid("SIZE_LIMIT");
                }

                hash.AppendData(buffer.AsSpan(0, read));
                await temporary.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
            }

            if (size < EvidenceFileLimits.MinimumBytes)
            {
                await temporary.DisposeAsync();
                return FileTechnicalValidationResult.Invalid("EMPTY");
            }

            await temporary.FlushAsync(cancellationToken);
            temporary.Position = 0;

            var detected = DetectType(temporary);
            if (detected != expectedType)
            {
                await temporary.DisposeAsync();
                return FileTechnicalValidationResult.Invalid("TYPE_MISMATCH");
            }

            temporary.Position = 0;
            var structurallyValid = detected switch
            {
                EvidenceMediaType.Jpeg => ValidateJpeg(temporary),
                EvidenceMediaType.Png => ValidatePng(temporary),
                EvidenceMediaType.Pdf => ValidatePdf(temporary),
                _ => false
            };

            if (!structurallyValid)
            {
                await temporary.DisposeAsync();
                return FileTechnicalValidationResult.Invalid("STRUCTURE");
            }

            temporary.Position = 0;
            return FileTechnicalValidationResult.Valid(new ValidatedEvidenceFile(
                temporary,
                detected.Value,
                size,
                Convert.ToHexStringLower(hash.GetHashAndReset())));
        }
        catch
        {
            await temporary.DisposeAsync();
            throw;
        }
    }

    private static bool TryResolveExpectedType(
        string declaredMediaType,
        string originalFileName,
        out EvidenceMediaType mediaType)
    {
        mediaType = default;
        if (string.IsNullOrWhiteSpace(declaredMediaType) || string.IsNullOrWhiteSpace(originalFileName))
        {
            return false;
        }

        var extension = Path.GetExtension(Path.GetFileName(originalFileName));
        if (declaredMediaType.Equals("image/jpeg", StringComparison.OrdinalIgnoreCase) &&
            (extension.Equals(".jpg", StringComparison.OrdinalIgnoreCase) ||
             extension.Equals(".jpeg", StringComparison.OrdinalIgnoreCase)))
        {
            mediaType = EvidenceMediaType.Jpeg;
            return true;
        }

        if (declaredMediaType.Equals("image/png", StringComparison.OrdinalIgnoreCase) &&
            extension.Equals(".png", StringComparison.OrdinalIgnoreCase))
        {
            mediaType = EvidenceMediaType.Png;
            return true;
        }

        if (declaredMediaType.Equals("application/pdf", StringComparison.OrdinalIgnoreCase) &&
            extension.Equals(".pdf", StringComparison.OrdinalIgnoreCase))
        {
            mediaType = EvidenceMediaType.Pdf;
            return true;
        }

        return false;
    }

    private static EvidenceMediaType? DetectType(Stream stream)
    {
        Span<byte> header = stackalloc byte[8];
        var read = stream.Read(header);
        stream.Position = 0;
        if (read >= 3 && header[0] == 0xff && header[1] == 0xd8 && header[2] == 0xff)
        {
            return EvidenceMediaType.Jpeg;
        }

        ReadOnlySpan<byte> png = [137, 80, 78, 71, 13, 10, 26, 10];
        if (read == 8 && header.SequenceEqual(png))
        {
            return EvidenceMediaType.Png;
        }

        if (read >= 8 && header[..5].SequenceEqual("%PDF-"u8))
        {
            return EvidenceMediaType.Pdf;
        }

        return null;
    }

    private static bool ValidateJpeg(Stream stream)
    {
        if (stream.ReadByte() != 0xff || stream.ReadByte() != 0xd8)
        {
            return false;
        }

        var hasSof = false;
        var hasSos = false;
        while (stream.Position < stream.Length)
        {
            if (stream.ReadByte() != 0xff)
            {
                return false;
            }

            int marker;
            do
            {
                marker = stream.ReadByte();
            }
            while (marker == 0xff);

            if (marker == 0xd9)
            {
                return hasSof && hasSos && stream.Position == stream.Length;
            }

            if (marker is 0x01 or >= 0xd0 and <= 0xd7)
            {
                continue;
            }

            if (!TryReadBigEndianUInt16(stream, out var segmentLength) || segmentLength < 2)
            {
                return false;
            }

            var payloadLength = segmentLength - 2;
            if (stream.Position + payloadLength > stream.Length)
            {
                return false;
            }

            if (IsStartOfFrame(marker))
            {
                if (payloadLength < 6 || stream.ReadByte() < 0 ||
                    !TryReadBigEndianUInt16(stream, out var height) ||
                    !TryReadBigEndianUInt16(stream, out var width) ||
                    !HasSafeDimensions(width, height))
                {
                    return false;
                }

                stream.Position += payloadLength - 5;
                hasSof = true;
                continue;
            }

            stream.Position += payloadLength;
            if (marker != 0xda)
            {
                continue;
            }

            hasSos = true;
            if (!SkipJpegEntropyData(stream))
            {
                return false;
            }
        }

        return false;
    }

    private static bool SkipJpegEntropyData(Stream stream)
    {
        while (stream.Position < stream.Length)
        {
            if (stream.ReadByte() != 0xff)
            {
                continue;
            }

            var markerPosition = stream.Position - 1;
            int next;
            do
            {
                next = stream.ReadByte();
            }
            while (next == 0xff);

            if (next == 0x00 || next is >= 0xd0 and <= 0xd7)
            {
                continue;
            }

            stream.Position = markerPosition;
            return true;
        }

        return false;
    }

    private static bool ValidatePng(Stream stream)
    {
        Span<byte> signature = stackalloc byte[8];
        ReadOnlySpan<byte> expectedSignature = [137, 80, 78, 71, 13, 10, 26, 10];
        if (stream.Read(signature) != signature.Length ||
            !signature.SequenceEqual(expectedSignature))
        {
            return false;
        }

        var first = true;
        var hasIdat = false;
        var hasIend = false;
        var buffer = new byte[EvidenceFileLimits.BufferBytes];
        while (stream.Position < stream.Length)
        {
            if (!TryReadBigEndianUInt32(stream, out var dataLength) || dataLength > EvidenceFileLimits.MaximumBytes)
            {
                return false;
            }

            Span<byte> type = stackalloc byte[4];
            if (stream.Read(type) != type.Length || stream.Position + dataLength + 4 > stream.Length)
            {
                return false;
            }

            var typeText = Encoding.ASCII.GetString(type);
            if (first && (typeText != "IHDR" || dataLength != 13))
            {
                return false;
            }

            var crc = UpdateCrc(uint.MaxValue, type);
            uint? width = null;
            uint? height = null;
            long remaining = dataLength;
            while (remaining > 0)
            {
                var requested = (int)Math.Min(buffer.Length, remaining);
                var read = stream.Read(buffer, 0, requested);
                if (read != requested)
                {
                    return false;
                }

                if (first && width is null && read >= 8)
                {
                    width = BinaryPrimitives.ReadUInt32BigEndian(buffer.AsSpan(0, 4));
                    height = BinaryPrimitives.ReadUInt32BigEndian(buffer.AsSpan(4, 4));
                }

                crc = UpdateCrc(crc, buffer.AsSpan(0, read));
                remaining -= read;
            }

            if (!TryReadBigEndianUInt32(stream, out var expectedCrc) || ~crc != expectedCrc)
            {
                return false;
            }

            if (first && (width is null || height is null || !HasSafeDimensions(width.Value, height.Value)))
            {
                return false;
            }

            first = false;
            if (typeText == "IDAT")
            {
                hasIdat = true;
            }

            if (typeText != "IEND")
            {
                continue;
            }

            hasIend = dataLength == 0;
            break;
        }

        return hasIdat && hasIend && stream.Position == stream.Length;
    }

    private static bool ValidatePdf(Stream stream)
    {
        Span<byte> header = stackalloc byte[8];
        if (stream.Read(header) != header.Length || !PdfHeaderRegex().IsMatch(Encoding.ASCII.GetString(header)))
        {
            return false;
        }

        var tailLength = (int)Math.Min(PdfTailBytes, stream.Length);
        var tail = new byte[tailLength];
        stream.Position = stream.Length - tailLength;
        if (stream.Read(tail) != tailLength)
        {
            return false;
        }

        var tailText = Encoding.ASCII.GetString(tail);
        var eof = tailText.LastIndexOf("%%EOF", StringComparison.Ordinal);
        if (eof < 0 || tailText[(eof + 5)..].Any(character => character is not (' ' or '\t' or '\r' or '\n' or '\f' or '\0')))
        {
            return false;
        }

        var beforeEof = tailText[..eof];
        var startXrefMarker = beforeEof.LastIndexOf("startxref", StringComparison.Ordinal);
        if (startXrefMarker < 0)
        {
            return false;
        }

        var offsetText = beforeEof[(startXrefMarker + "startxref".Length)..].Trim();
        var firstLine = offsetText.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
        if (!long.TryParse(firstLine, System.Globalization.NumberStyles.None,
                System.Globalization.CultureInfo.InvariantCulture, out var xrefOffset) ||
            xrefOffset < 0 || xrefOffset >= stream.Length)
        {
            return false;
        }

        stream.Position = xrefOffset;
        Span<byte> target = stackalloc byte[128];
        var targetRead = stream.Read(target);
        var targetText = Encoding.ASCII.GetString(target[..targetRead]);
        if (!targetText.StartsWith("xref", StringComparison.Ordinal) &&
            !XrefObjectRegex().IsMatch(targetText))
        {
            return false;
        }

        stream.Position = 0;
        return ContainsIndirectObject(stream);
    }

    private static bool ContainsIndirectObject(Stream stream)
    {
        var buffer = new byte[EvidenceFileLimits.BufferBytes + 32];
        var carry = 0;
        while (true)
        {
            var read = stream.Read(buffer, carry, EvidenceFileLimits.BufferBytes);
            if (read == 0)
            {
                return false;
            }

            var total = carry + read;
            if (IndirectObjectRegex().IsMatch(Encoding.ASCII.GetString(buffer, 0, total)))
            {
                return true;
            }

            carry = Math.Min(32, total);
            buffer.AsSpan(total - carry, carry).CopyTo(buffer);
        }
    }

    private static bool HasSafeDimensions(uint width, uint height) =>
        width > 0 && height > 0 && width <= MaximumDimension && height <= MaximumDimension &&
        (long)width * height <= MaximumPixels;

    private static bool IsStartOfFrame(int marker) => marker is
        0xc0 or 0xc1 or 0xc2 or 0xc3 or 0xc5 or 0xc6 or 0xc7 or 0xc9 or 0xca or 0xcb or 0xcd or 0xce or 0xcf;

    private static bool TryReadBigEndianUInt16(Stream stream, out ushort value)
    {
        Span<byte> buffer = stackalloc byte[2];
        value = 0;
        if (stream.Read(buffer) != buffer.Length)
        {
            return false;
        }

        value = BinaryPrimitives.ReadUInt16BigEndian(buffer);
        return true;
    }

    private static bool TryReadBigEndianUInt32(Stream stream, out uint value)
    {
        Span<byte> buffer = stackalloc byte[4];
        value = 0;
        if (stream.Read(buffer) != buffer.Length)
        {
            return false;
        }

        value = BinaryPrimitives.ReadUInt32BigEndian(buffer);
        return true;
    }

    private static uint UpdateCrc(uint crc, ReadOnlySpan<byte> data)
    {
        foreach (var value in data)
        {
            crc = CrcTable[(crc ^ value) & 0xff] ^ (crc >> 8);
        }

        return crc;
    }

    private static uint[] BuildCrcTable()
    {
        var table = new uint[256];
        for (uint index = 0; index < table.Length; index++)
        {
            var value = index;
            for (var bit = 0; bit < 8; bit++)
            {
                value = (value & 1) == 1 ? 0xedb88320U ^ (value >> 1) : value >> 1;
            }

            table[index] = value;
        }

        return table;
    }

    [GeneratedRegex(@"^%PDF-(?:1\.[0-7]|2\.0)$", RegexOptions.CultureInvariant)]
    private static partial Regex PdfHeaderRegex();

    [GeneratedRegex(@"^\d+\s+\d+\s+obj\b[\s\S]{0,96}/Type\s*/XRef\b", RegexOptions.CultureInvariant)]
    private static partial Regex XrefObjectRegex();

    [GeneratedRegex(@"\b\d+\s+\d+\s+obj\b", RegexOptions.CultureInvariant)]
    private static partial Regex IndirectObjectRegex();
}
