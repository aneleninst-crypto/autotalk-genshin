using System.Drawing;

namespace GenshinDialogueHelper;

internal static class DialogueEllipsisDetector
{
    internal const string Marker = "Auto + ellipsis";

    internal static bool IsVisible(Bitmap frame)
    {
        // A punctuation-only dialogue line is centred at the same position on
        // every supported resolution. OCR commonly returns only the speaker's
        // name, so verify the three small neutral-white dots directly.
        for (int centreY = 932; centreY <= 942; centreY++)
        for (int centreX = 956; centreX <= 966; centreX++)
        {
            int total = 0;
            bool allDots = true;
            foreach (int offset in new[] { -11, 0, 11 })
            {
                int dot = 0;
                for (int dy = -4; dy <= 4; dy++)
                for (int dx = -4; dx <= 4; dx++)
                    if (IsBrightNeutral(PixelAtReference(frame, centreX + offset + dx, centreY + dy))) dot++;
                if (dot < 8) allDots = false;
                total += dot;
            }
            if (allDots && total <= 100) return true;
        }
        return false;
    }

    private static bool IsBrightNeutral(Color c)
    {
        int min = Math.Min(c.R, Math.Min(c.G, c.B));
        int max = Math.Max(c.R, Math.Max(c.G, c.B));
        return min >= 210 && max - min <= 35;
    }

    private static Color PixelAtReference(Bitmap frame, int x, int y) => frame.GetPixel(
        Math.Clamp((int)Math.Round(x * frame.Width / 1920.0), 0, frame.Width - 1),
        Math.Clamp((int)Math.Round(y * frame.Height / 1080.0), 0, frame.Height - 1));
}
