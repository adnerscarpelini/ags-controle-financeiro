using System.Buffers.Binary;
using Ags.Finance.Modules.Accounts.Application;
using Ags.Finance.Modules.Accounts.Contracts;
using SkiaSharp;

namespace Ags.Finance.Modules.Accounts.Infrastructure;

internal sealed class LogoProcessor : ILogoProcessor
{
    public LogoPreviewResult CreatePreview(byte[] content, LogoCrop crop)
    {
        if (content is not { Length: > 0 and <= IBankLogoCatalog.MaxSourceBytes }) { return new(null, "Envie PNG, JPEG ou WebP de até 5 MB."); }
        if (!double.IsFinite(crop.Zoom) || !double.IsFinite(crop.Horizontal) || !double.IsFinite(crop.Vertical)
            || crop.Zoom is < 0.2 or > 5 || crop.Horizontal is < -1 or > 1 || crop.Vertical is < -1 or > 1) { return new(null, "Ajuste zoom e posição dentro dos limites do recorte."); }
        try
        {
            using var stream = new SKMemoryStream(content);
            using var codec = SKCodec.Create(stream);
            if (codec is null || codec.EncodedFormat is not (SKEncodedImageFormat.Png or SKEncodedImageFormat.Jpeg or SKEncodedImageFormat.Webp))
            {
                return new(null, "O conteúdo não é uma imagem PNG, JPEG ou WebP válida.");
            }
            if (!HasCompleteEnvelope(content, codec.EncodedFormat)) { return new(null, "A imagem está corrompida ou incompleta."); }
            var info = codec.Info;
            if (info.Width is < 1 or > 4096 || info.Height is < 1 or > 4096) { return new(null, "A imagem deve ter até 4096 × 4096 pixels."); }
            using var colors = SKColorSpace.CreateSrgb();
            using var source = new SKBitmap(new SKImageInfo(info.Width, info.Height, SKColorType.Rgba8888, SKAlphaType.Premul, colors));
            if (codec.GetPixels(source.Info, source.GetPixels()) != SKCodecResult.Success) { return new(null, "A imagem está corrompida ou incompleta."); }
            using var target = new SKBitmap(new SKImageInfo(256, 256, SKColorType.Rgba8888, SKAlphaType.Premul, colors));
            using var canvas = new SKCanvas(target);
            canvas.Clear(SKColors.Transparent);
            using var path = new SKPathBuilder();
            path.AddCircle(128, 128, 128);
            using var circle = path.Detach();
            canvas.ClipPath(circle, SKClipOperation.Intersect, true);
            var scale = Math.Max(256d / info.Width, 256d / info.Height) * crop.Zoom;
            var width = info.Width * scale;
            var height = info.Height * scale;
            var left = -(width - 256) * (crop.Horizontal + 1) / 2;
            var top = -(height - 256) * (crop.Vertical + 1) / 2;
            using var image = SKImage.FromBitmap(source);
            using var paint = new SKPaint { IsAntialias = true };
            canvas.DrawImage(image, new SKRect((float)left, (float)top, (float)(left + width), (float)(top + height)), new SKSamplingOptions(SKFilterMode.Linear), paint);
            canvas.Flush();
            using var result = SKImage.FromBitmap(target);
            using var encoded = result.Encode(SKEncodedImageFormat.Png, 100);
            return encoded is null ? new(null, "Não foi possível gerar o recorte.") : new(encoded.ToArray(), null);
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            return new(null, "Não foi possível decodificar esta imagem.");
        }
    }

    private static bool HasCompleteEnvelope(byte[] content, SKEncodedImageFormat format) => format switch
    {
        SKEncodedImageFormat.Png => content.Length >= 12 && content.AsSpan(content.Length - 12).SequenceEqual(new byte[] { 0, 0, 0, 0, 73, 69, 78, 68, 174, 66, 96, 130 }),
        SKEncodedImageFormat.Jpeg => content.Length >= 2 && content[^2] == 255 && content[^1] == 217,
        SKEncodedImageFormat.Webp => content.Length >= 12 && (ulong)BinaryPrimitives.ReadUInt32LittleEndian(content.AsSpan(4, 4)) + 8 == (ulong)content.Length,
        _ => false
    };
}
