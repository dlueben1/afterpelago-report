using System.Security.Cryptography;
using SkiaSharp;

namespace Afterpelago.Services;

public sealed record ConvertedGameImage(string Hash, int Width, int Height, long ByteLength, bool Animated);

public sealed class GameImageConverter
{
    public ConvertedGameImage Convert(byte[] input, string outputDirectory, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        using var data = SKData.CreateCopy(input);
        using var codec = SKCodec.Create(data) ?? throw new InvalidDataException("Unrecognized image data.");
        if (codec.EncodedFormat is not (SKEncodedImageFormat.Png or SKEncodedImageFormat.Jpeg or SKEncodedImageFormat.Gif or SKEncodedImageFormat.Webp))
            throw new InvalidDataException("Only PNG, JPEG, GIF and WebP icons are supported.");
        var info = codec.Info;
        if (info.Width <= 0 || info.Height <= 0 || (long)info.Width * info.Height > 16_000_000)
            throw new InvalidDataException("Image exceeds 16 megapixels.");
        using var bitmap = new SKBitmap(info.Width, info.Height, SKColorType.Rgba8888, SKAlphaType.Unpremul);
        if (codec.GetPixels(bitmap.Info, bitmap.GetPixels()) != SKCodecResult.Success)
            throw new InvalidDataException("Image decoding failed.");
        ct.ThrowIfCancellationRequested();
        using var pixmap = bitmap.PeekPixels();
        using var encoded = pixmap.Encode(new SKWebpEncoderOptions(SKWebpEncoderCompression.Lossless, 100))
            ?? throw new InvalidDataException("WebP encoding failed.");
        ct.ThrowIfCancellationRequested();
        var bytes = encoded.ToArray();
        var hash = System.Convert.ToHexStringLower(SHA256.HashData(bytes));
        Directory.CreateDirectory(outputDirectory);
        var path = Path.Combine(outputDirectory, hash + ".webp");
        if (!File.Exists(path)) File.WriteAllBytes(path, bytes);
        return new(hash, info.Width, info.Height, bytes.Length, codec.FrameCount > 1);
    }
}
