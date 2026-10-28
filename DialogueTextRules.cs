using System.Text.RegularExpressions;

namespace GenshinDialogueHelper;

internal static class DialogueTextRules
{
    internal const string AutoSpeechMarker = "Auto + speech";

    // "авто" is the Russian client's label for the "Auto" dialogue button.
    internal static bool HasAutoControl(string text) =>
        Regex.IsMatch(text, @"(?<!\p{L})(?:авто|auto)(?!\p{L})", RegexOptions.IgnoreCase);

    internal static string? Match(string controls, string subtitles, string choices = "",
        bool choicePromptVisible = false, bool ellipsisVisible = false,
        bool choiceEllipsisVisible = false)
    {
        string lower = subtitles.ToLowerInvariant().Replace('ё', 'е');
        // The first pattern is the Russian client's "Click to continue" prompt.
        if (Regex.IsMatch(lower, @"нажмите\s*[,，]?\s*(?:чтобы\s+)?продолжить") ||
            Regex.IsMatch(lower, @"(?:click|press)\s+(?:to\s+)?continue"))
            return "continue prompt";

        bool auto = HasAutoControl(controls);
        bool speech = subtitles.Count(char.IsLetter) >= 8;
        if (auto && speech) return AutoSpeechMarker;
        if (auto && ellipsisVisible) return DialogueEllipsisDetector.Marker;
        if (speech && choicePromptVisible && choiceEllipsisVisible)
            return DialogueChoiceDetector.EllipsisMarker;
        bool choice = choicePromptVisible && choices.Count(char.IsLetter) >= 2;
        return speech && choice ? DialogueChoiceDetector.Marker : null;
    }
}
