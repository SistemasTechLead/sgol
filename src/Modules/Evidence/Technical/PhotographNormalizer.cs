using SkiaSharp;

namespace Sgol.Evidence.Technical;

// Called only after size, signature, structure and malware checks. Never processes PDFs.
public static class PhotographNormalizer
{
    private static readonly SemaphoreSlim Gate = new(2);

    public static async Task<byte[]> NormalizeAsync(Stream source, CancellationToken token)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
        timeout.CancelAfter(TimeSpan.FromSeconds(20));
        await Gate.WaitAsync(timeout.Token);
        try
        {
            source.Position = 0;
            using var input = new MemoryStream();
            await source.CopyToAsync(input, timeout.Token);
            if (input.Length is < 1 or > 15728640) throw new InvalidDataException("IMAGE_SIZE");
            using var data = SKData.CreateCopy(input.ToArray());
            using var codec = SKCodec.Create(data) ?? throw new InvalidDataException("IMAGE_DECODE");
            var info = codec.Info;
            if (codec.EncodedFormat is not (SKEncodedImageFormat.Jpeg or SKEncodedImageFormat.Png) ||
                codec.FrameCount > 1 || info.Width < 1 || info.Height < 1 || info.Width > 20000 || info.Height > 20000 ||
                (long)info.Width * info.Height > 40000000) throw new InvalidDataException("IMAGE_DIMENSIONS");
            // Full decode is bounded to 160 MB of pixels; only two decodes may run concurrently.
            using var pixels = new SKBitmap(new SKImageInfo(info.Width, info.Height, SKColorType.Rgba8888, SKAlphaType.Opaque));
            if (codec.GetPixels(pixels.Info, pixels.GetPixels()) != SKCodecResult.Success) throw new InvalidDataException("IMAGE_DECODE");
            timeout.Token.ThrowIfCancellationRequested();
            var scale = Math.Min(1d, 2560d / Math.Max(info.Width, info.Height));
            using var surface = SKSurface.Create(new SKImageInfo((int)Math.Round(info.Width * scale), (int)Math.Round(info.Height * scale)))
                ?? throw new InvalidDataException("IMAGE_MEMORY");
            surface.Canvas.Clear(SKColors.White);
            surface.Canvas.DrawBitmap(pixels, new SKRect(0, 0, surface.Canvas.DeviceClipBounds.Width, surface.Canvas.DeviceClipBounds.Height),
                new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.None));
            using var image = surface.Snapshot();
            // A fresh raster and JPEG encoder retain no EXIF, GPS or other original metadata.
            using var encoded = image.Encode(SKEncodedImageFormat.Jpeg, 90) ?? throw new InvalidDataException("IMAGE_ENCODE");
            timeout.Token.ThrowIfCancellationRequested();
            if (encoded.Size is < 1 or > 15728640) throw new InvalidDataException("IMAGE_SIZE");
            return encoded.ToArray();
        }
        finally { Gate.Release(); }
    }
}
