namespace GenshinDialogueHelper;

internal sealed class AutomaticDialogueGate
{
    private string? previous;
    private long lastSeen;
    private long lastPress;
    private long conversationUntil;
    private long armedInsertUntil;

    internal void Reset()
    {
        previous = null;
        lastSeen = 0;
        conversationUntil = 0;
        armedInsertUntil = 0;
    }

    // A deliberate F8 also permits starting on an already open item insert.
    // This short window does not bypass shape detection or frame confirmation.
    internal void Arm(long now) { Reset(); armedInsertUntil = now + 15000; }

    internal bool NeedsDialogueContext { get; private set; }

    internal bool Update(string? marker, long now, int interval)
    {
        NeedsDialogueContext = false;
        if (marker is null) { previous = null; lastSeen = 0; return false; }
        bool protectedMarker = marker == ContinueDiamondDetector.Marker || marker == DialogueChoiceDetector.Marker;
        bool hasContext = now < conversationUntil ||
            marker == ContinueDiamondDetector.Marker && now < armedInsertUntil;
        if (protectedMarker && !hasContext)
        {
            NeedsDialogueContext = true;
            previous = null;
            lastSeen = 0;
            return false;
        }
        bool immediate = marker == DialogueTextRules.AutoSpeechMarker ||
            marker == DialogueChoiceDetector.EllipsisMarker;
        if (immediate)
        {
            previous = marker;
            lastSeen = now;
            conversationUntil = now + 30000;
            if (now - lastPress < interval) return false;
            lastPress = now;
            return true;
        }
        bool confirmed = previous == marker && now - lastSeen <= 1500;
        previous = marker;
        lastSeen = now;
        if (confirmed)
        {
            conversationUntil = now + 30000;
            armedInsertUntil = 0;
        }
        if (!confirmed || now - lastPress < interval) return false;
        lastPress = now;
        return true;
    }
}
