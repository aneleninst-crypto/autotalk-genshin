using System.Drawing;
using System.Drawing.Imaging;
using Windows.Globalization;
using Windows.Graphics.Imaging;
using Windows.Media.Ocr;
using Windows.Storage.Streams;

namespace GenshinDialogueHelper;

internal sealed class TextDialogueDetector
{
    private readonly OcrEngine engine = OcrEngine.TryCreateFromLanguage(new Language("ru-RU"))
        ?? OcrEngine.TryCreateFromUserProfileLanguages()
        ?? throw new InvalidOperationException("Windows OCR is unavailable. A Windows OCR language pack is required.");

    internal async Task<(string? Marker, string Controls, string Subtitles, string Choices)> DetectAsync(Bitmap frame)
    {
        string controls = await ReadAsync(frame, new Rectangle(98, 30, 82, 36), 3);
        if (!DialogueTextRules.HasAutoControl(controls))
            controls = await ReadAsync(frame, new Rectangle(98, 30, 82, 36), 3, true);
        string subtitles = await ReadAsync(frame, new Rectangle(320, 825, 1280, 235));
        bool choicePromptVisible = DialogueChoiceDetector.IsVisible(frame);
        string choices = choicePromptVisible
            ? await ReadAsync(frame, new Rectangle(1320, 360, 570, 470), 2, true)
            : "";
        bool ellipsisVisible = DialogueEllipsisDetector.IsVisible(frame);
        bool choiceEllipsisVisible = choicePromptVisible && DialogueChoiceDetector.HasEllipsis(frame);
        string? marker = DialogueTextRules.Match(controls, subtitles, choices, choicePromptVisible,
            ellipsisVisible, choiceEllipsisVisible);
        if (marker is null && ContinueDiamondDetector.IsVisible(frame))
            marker = ContinueDiamondDetector.Marker;
        return (marker, controls, subtitles, choices);
    }

    private async Task<string> ReadAsync(Bitmap frame, Rectangle reference, int scale = 1, bool isolateText = false)
    {
        var source = new Rectangle(
            (int)Math.Round(reference.X * frame.Width / 1920.0),
            (int)Math.Round(reference.Y * frame.Height / 1080.0),
            (int)Math.Round(reference.Width * frame.Width / 1920.0),
            (int)Math.Round(reference.Height * frame.Height / 1080.0));
        using var crop = new Bitmap(reference.Width * scale, reference.Height * scale);
        using (var graphics = Graphics.FromImage(crop))
            graphics.DrawImage(frame, new Rectangle(Point.Empty, crop.Size), source, GraphicsUnit.Pixel);
        if (isolateText)
        {
            // OCR needs the letter shapes, not the changing scenery behind them.
            // Keep bright neutral text and render it as black ink on white.
            for (int y = 0; y < crop.Height; y++)
            for (int x = 0; x < crop.Width; x++)
            {
                Color c = crop.GetPixel(x, y);
                int min = Math.Min(c.R, Math.Min(c.G, c.B));
                int max = Math.Max(c.R, Math.Max(c.G, c.B));
                crop.SetPixel(x, y, min >= 220 && max - min <= 30 ? Color.Black : Color.White);
            }
        }
        using var encoded = new MemoryStream();
        crop.Save(encoded, ImageFormat.Png);
        using var stream = new InMemoryRandomAccessStream();
        using (var writer = new DataWriter(stream.GetOutputStreamAt(0)))
        {
            writer.WriteBytes(encoded.ToArray());
            await writer.StoreAsync();
        }
        stream.Seek(0);
        var decoder = await BitmapDecoder.CreateAsync(stream);
        using var bitmap = await decoder.GetSoftwareBitmapAsync(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied);
        return (await engine.RecognizeAsync(bitmap)).Text;
    }
}
