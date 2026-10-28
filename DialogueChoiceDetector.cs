using System.Drawing;

namespace GenshinDialogueHelper;

internal static class DialogueChoiceDetector
{
    internal const string Marker = "answer option";
    internal const string EllipsisMarker = "answer option with ellipsis";

    internal static bool IsVisible(Bitmap frame) => TryFindCenterY(frame, out _);

    private static bool TryFindCenterY(Bitmap frame, out int result)
    {
        // Dialogue answers are anchored to the right and use a large, almost
        // white circular speech-bubble icon. Checking this icon keeps ordinary
        // world interaction prompts (which also show the F key) from advancing.
        const int centreX = 1298;
        const int radius = 20;
        int bestScore = int.MinValue;
        result = 0;
        for (int centreY = 360; centreY <= 820; centreY += 4)
        {
            int bright = 0;
            int centre = 0;
            int corners = 0;
            for (int dy = -radius; dy <= radius; dy++)
            for (int dx = -radius; dx <= radius; dx++)
            {
                Color c = PixelAtReference(frame, centreX + dx, centreY + dy);
                int min = Math.Min(c.R, Math.Min(c.G, c.B));
                int max = Math.Max(c.R, Math.Max(c.G, c.B));
                if (min >= 220 && max - min <= 25)
                {
                    bright++;
                    if (Math.Abs(dx) <= 8 && Math.Abs(dy) <= 8) centre++;
                    if (Math.Abs(dx) >= 14 && Math.Abs(dy) >= 14) corners++;
                }
            }
            // A real answer icon has a bright middle and rounded, dark corners.
            // Large bright character models and menu panels fail this geometry.
            if (bright is >= 450 and <= 1000 && centre >= 180 && corners <= 30)
            {
                int score = centre * 4 + bright - corners * 5;
                if (score > bestScore)
                {
                    bestScore = score;
                    result = centreY;
                }
            }
        }
        return bestScore != int.MinValue;
    }

    internal static bool HasEllipsis(Bitmap frame)
    {
        // OCR intentionally ignores punctuation-only answers. Detect the three
        // compact white dots next to the answer icon instead. The strict size,
        // spacing and alignment keep ordinary menu text from matching.
        if (!TryFindCenterY(frame, out int centreY)) return false;
        for (int y = centreY + 4; y <= centreY + 10; y++)
        for (int x = 1320; x <= 1380; x++)
        for (int firstGap = 8; firstGap <= 14; firstGap++)
        for (int secondGap = 8; secondGap <= 14; secondGap++)
        {
            // The game uses proportional punctuation spacing. In combinations
            // such as "?.." the first gap is wider than the following ones,
            // so requiring the old fixed 9 px cadence misses a real answer.
            if (!BrightBlock(frame, x, y) || !BrightBlock(frame, x + firstGap, y) ||
                !BrightBlock(frame, x + firstGap + secondGap, y)) continue;
            return true;
        }
        return false;
    }

    private static bool BrightBlock(Bitmap frame, int left, int top)
    {
        int bright = 0;
        for (int y = 0; y < 4; y++)
        for (int x = 0; x < 4; x++)
        {
            Color c = PixelAtReference(frame, left + x, top + y);
            int min = Math.Min(c.R, Math.Min(c.G, c.B));
            int max = Math.Max(c.R, Math.Max(c.G, c.B));
            if (min >= 220 && max - min <= 25) bright++;
        }
        return bright >= 12;
    }

    private static Color PixelAtReference(Bitmap frame, int x, int y) => frame.GetPixel(
        Math.Clamp((int)Math.Round(x * frame.Width / 1920.0), 0, frame.Width - 1),
        Math.Clamp((int)Math.Round(y * frame.Height / 1080.0), 0, frame.Height - 1));
}
