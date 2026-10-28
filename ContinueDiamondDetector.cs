using System.Drawing;

namespace GenshinDialogueHelper;

internal static class ContinueDiamondDetector
{
    internal const string Marker = "continue diamond";

    internal static bool IsVisible(Bitmap frame)
    {
        // Sample a small normalized region, then test the four diagonal edges,
        // empty centre and empty space outside the shape. Gold blobs do not pass.
        const int left = 930, top = 985, width = 62, height = 85;
        var gold = new bool[width, height];
        for (int y = 0; y < height; y++)
        for (int x = 0; x < width; x++)
        {
            Color c = frame.GetPixel((int)Math.Round((left + x) * frame.Width / 1920.0),
                (int)Math.Round((top + y) * frame.Height / 1080.0));
            gold[x, y] = c.R > 120 && c.R > c.G + 20 && c.G > c.B + 35;
        }
        bool Gold(int x, int y) => x >= 0 && y >= 0 && x < width && y < height && gold[x, y];
        bool NearGold(int x, int y) => Gold(x, y) || Gold(x - 1, y) || Gold(x + 1, y) ||
            Gold(x, y - 1) || Gold(x, y + 1);
        for (int cy = 1010 - top; cy <= 1048 - top; cy++)
        for (int cx = 955 - left; cx <= 966 - left; cx++)
        for (int radius = 10; radius <= 15; radius++)
        {
            int centre = 0;
            for (int dy = -3; dy <= 3; dy++)
            for (int dx = -3; dx <= 3; dx++)
                if (Gold(cx + dx, cy + dy)) centre++;
            if (centre > 3) continue;
            bool edges = true;
            foreach (int sx in new[] { -1, 1 })
            foreach (int sy in new[] { -1, 1 })
            {
                int hits = 0;
                for (int i = 0; i <= 6; i++)
                {
                    int dx = (int)Math.Round(radius * i / 6.0);
                    if (NearGold(cx + sx * dx, cy + sy * (radius - dx))) hits++;
                }
                if (hits < 6) edges = false;
            }
            if (!edges) continue;
            int outside = 0;
            foreach (int sx in new[] { -1, 1 })
            foreach (int sy in new[] { -1, 1 })
                if (NearGold(cx + sx * radius, cy + sy * radius)) outside++;
            if (outside == 0) return true;
        }
        return false;
    }
}
