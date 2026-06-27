# AetherVoice Plugin Structure

## Project Files

```
AetherVoice/
├── AetherVoice.csproj      # Project file with dependencies
├── AetherVoice.json        # Plugin manifest for Dalamud
├── Plugin.cs               # Main plugin class
├── Configuration.cs        # Configuration storage
├── TTSManager.cs           # Handles TTS and sound playback
├── Windows/
│   └── ConfigWindow.cs     # ImGui configuration window
├── NaelQuotes.cs           # Data structures for quotes
└── NaelQuotes.json         # Embedded quote database
```

## Key Dependencies

- **Dalamud**: FFXIV plugin framework
- **FuzzySharp**: Fuzzy string matching for quote detection
- **NAudio**: Audio playback for sound files
- **System.Speech**: Windows TTS

## How It Works

1. **Quote Detection**: Monitors NPC dialogue chat messages
2. **Fuzzy Matching**: Uses FuzzySharp to match Nael quotes (85% threshold)
3. **Mechanic Mapping**: Maps quotes to mechanic keys
4. **Playback**: Either speaks TTS text or plays sound file based on configuration

## Customization Points

### TTS Prompts
Edit in configuration UI or directly in `Configuration.cs` default values.

### Sound Files
Specify full paths in configuration UI. Supports WAV, MP3, OGG.

### Quote Matching
Adjust fuzzy match threshold in `Plugin.cs` line with `Score: >= 85`.

### Adding New Mechanics
1. Add entry to `MechanicConfigs` in `Configuration.cs`
2. Add quote-to-mechanic mapping in `LoadQuotesDictionary()`
3. Add display name in `PluginUI.GetMechanicDisplayName()`

## Testing

Use `/aethervoice test` to test random mechanic without being in UCOB.
