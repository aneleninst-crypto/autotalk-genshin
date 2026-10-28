# Genshin Dialogue Helper

A small Windows utility that automatically advances Genshin Impact dialogues. It watches the game window with the built-in Windows OCR and presses the interaction key (or clicks, for story inserts) when it recognizes that a dialogue is on screen.

> **Disclaimer:** this is third-party automation. It does not read or modify game memory or game files, but that does not guarantee it is permitted by the game's terms of service. Use it at your own risk.

## Features

- Detects regular dialogue lines, answer options (including ellipsis-only answers), "Click to continue" prompts and story inserts with the golden continue diamond.
- Works only while `GenshinImpact.exe` or `YuanShen.exe` is the active window.
- Pauses automatically while you move (WASD), jump (Space) or open menus (Esc, M).
- Runs fully locally: frames are processed in memory and are never sent over the network.

## Usage

1. Build the project (see [Build](#build)) and run `GenshinDialogueHelper.exe`. The app requests administrator rights so that it can send input to the game.
2. Press **F8** once, or click **Start**. The helper then repeats the interaction key whenever it recognizes a dialogue; you do not need to hold anything down.
3. **F9** pauses. Pressing F8 again also turns the helper off.
4. The default interaction key is `F`. If you rebound it in the game, enter its letter (A–Z) in the **Key** field. The **Interval, ms** field sets the minimum delay between presses.

The helper starts paused. Key presses are sent only while the game window is focused, and nothing is sent after you switch away from the game.

## How it works

Recognition uses the local Windows OCR engine (Russian first, falling back to your profile languages) on regions that are scaled relative to a 1920×1080 interface.

- **Regular dialogue** — the "Auto" button label in the dialogue panel plus speech text at the bottom of the screen. This combination is considered reliable and triggers on a single completed OCR pass.
- **Ellipsis-only line** — the "Auto" label plus three dots detected by shape in the text line.
- **Answer option** — speech text, option text and the round speech-bubble icon. It is allowed to fire only after a dialogue line has already been confirmed, so similar-looking elements in ordinary menus do not trigger it. An ellipsis-only answer is matched by the exact shape and position of three dots next to a geometrically confirmed answer icon.
- **"Click to continue" prompt** — recognized explicitly (Russian and English text).
- **Story inserts without text** — detected by the shape of the golden continue diamond (four diagonal edges, an empty centre and empty space around it). They are accepted for 30 seconds after a confirmed text dialogue. If the helper is started while an insert is already open, one F8 press grants 15 seconds to recognize it. For an insert the helper clicks the centre of the game window (at most once per second); for dialogue lines and answers it presses the selected key. If another window covers the centre, no click is sent.

Pausing, losing focus or pressing a movement key resets this context. Ambiguous signals require two consecutive matches, and a missing signal blocks input immediately. F9 also cancels an OCR pass that is still running.

The Russian dialogue panel labels (such as "Авто" and "Нажмите, чтобы продолжить") are matched in the OCR text, so a Windows OCR language pack must be installed. If OCR is unavailable, the app reports an error and presses nothing.

Limitations: a single OCR pass takes time, so the effective interval is never shorter than the recognition time; unusual dialogues without the labels above and without the diamond may be skipped; the helper cannot speed up mandatory cutscenes and animations.

## Build

Requirements: Windows 10/11, [.NET 10 SDK](https://dotnet.microsoft.com/download).

```powershell
dotnet publish .\GenshinDialogueHelper.csproj -c Release -o .\dist
```

The executable is written to `dist\GenshinDialogueHelper.exe`. Events are logged to `helper.log` next to the executable.

## Project layout

| File | Purpose |
| --- | --- |
| `Program.cs` | Window UI, hotkeys, screen capture and input sending |
| `TextDialogueDetector.cs` | Screen regions, image preparation and Windows OCR |
| `DialogueTextRules.cs` | Rules that turn OCR text into a dialogue decision |
| `AutomaticDialogueGate.cs` | Confirmation, context and rate limiting before any input |
| `DialogueChoiceDetector.cs` | Answer-icon and answer-ellipsis detection |
| `DialogueEllipsisDetector.cs` | Detection of ellipsis-only dialogue lines |
| `ContinueDiamondDetector.cs` | Detection of the golden continue diamond |
| `diagnose.py` | Read-only diagnostics of the foreground window and dialogue pixels |
| `Assets/` | Application icon and the prompt used to generate it |
