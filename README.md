# Nael TTS

**Repository**: [https://github.com/Dregan234/ffxiv-naeltts](https://github.com/Dregan234/ffxiv-naeltts)

A FFXIV Dalamud plugin that provides Text-to-Speech or custom sound file callouts for Nael deus Darnus mechanics in The Unending Coil of Bahamut (Ultimate).

## Features

- **Text-to-Speech Callouts**: Uses Windows TTS to call out mechanics with customizable text prompts
- **Custom Sound Files**: Alternative to TTS - use your own sound files for each mechanic
- **Fully Customizable**: Configure each mechanic's callout text or sound file
- **Multi-language Support**: Detects Nael quotes in EN, DE, FR, JP, and CN clients
- **Simple Commands**: Easy toggle and configuration access

## Installation

### From Custom Repository

1. In FFXIV, type `/xlsettings` to open Dalamud Settings
2. Go to the **Experimental** tab
3. Under **Custom Plugin Repositories**, paste this URL:
   ```
   https://raw.githubusercontent.com/Dregan234/ffxiv-naeltts/refs/heads/main/repo.json
   ```
4. Click the **+** button and **Save**
5. Type `/xlplugins` to open the Plugin Installer
6. Search for "Nael TTS" and click **Install**

### Prerequisites

- XIVLauncher, FINAL FANTASY XIV, and Dalamud have all been installed and the game has been run with Dalamud at least once.
- XIVLauncher is installed to its default directories and configurations.
  - If a custom path is required for Dalamud's dev directory, it must be set with the `DALAMUD_HOME` environment variable.
- A .NET 8 SDK has been installed and configured.

### Dev Plugin Installation

1. Open up `NaelTTS.sln` in your C# editor of choice (Visual Studio 2022 or JetBrains Rider).
2. Build the solution. By default, this will build a `Debug` build.
3. The resulting plugin DLL can be found at `NaelTTS/bin/x64/Debug/NaelTTS.dll`.
4. Launch the game and use `/xlsettings` in chat to open Dalamud settings.
5. In `Experimental`, add the full path to `NaelTTS.dll` to the list of Dev Plugin Locations.
6. Use `/xlplugins` to open the Plugin Installer, go to `Dev Tools > Installed Dev Plugins`, and enable NaelTTS.

## Usage

### Commands

- `/naeltts` - Toggle the plugin on/off
- `/naeltts cfg` or `/naeltts config` - Open configuration window
- `/naeltts test` - Test a random mechanic callout

### Configuration

Open the configuration with `/naeltts cfg` to customize:

1. **Audio Source**
   - Choose between Text-to-Speech or Sound Files

2. **TTS Settings** (if using TTS)
   - Volume: 0-100
   - Speed: -10 (slow) to +10 (fast)

3. **Mechanic Callouts**
   - Each mechanic has customizable text or sound file path
   - Default TTS prompts provided (e.g., "in out", "dive stack")

### Supported Mechanics

The plugin detects all 14 Nael quote mechanics:
- Dynamo → Chariot (In → Out)
- Dynamo → Beam (In → Stack)
- Beam → Chariot (Stack → Out)
- Beam → Dynamo (Stack → In)
- Dive → Chariot
- Dive → Dynamo
- Meteor Stream → Dive
- Dive → Beam
- Dive → Dynamo → Meteor Stream (triple)
- Dynamo → Dive → Meteor Stream (triple)
- Chariot → Beam → Dive (triple)
- Chariot → Dive → Beam (triple)
- Dynamo → Dive → Beam (triple)
- Dynamo → Chariot → Dive (triple)

## Project Structure

```
NaelTTS/
├── Plugin.cs               # Main plugin class
├── Configuration.cs        # Configuration storage
├── TTSManager.cs          # Handles TTS and sound playback
├── NaelQuotes.cs          # Data structures for quotes
├── NaelQuotes.json        # Embedded quote database
└── Windows/
    └── ConfigWindow.cs    # ImGui configuration window
```

## Using Sound Files

1. Set "Use Sound Files" in configuration
2. For each mechanic, specify the full path to your sound file
3. Supported formats: `.wav`, `.mp3`, `.ogg`

Example path:
```
C:\FFXIV\Sounds\in_out.wav
```

## Default TTS Prompts

The plugin comes with sensible defaults:
- "in out" for Dynamo → Chariot
- "stack in" for Beam → Dynamo
- "dive stack" for Dive → Beam
- etc.

Feel free to customize these to match your static's callouts!

## Architecture

This plugin follows the official [Dalamud SamplePlugin](https://github.com/goatcorp/SamplePlugin) structure:

- **WindowSystem**: Uses Dalamud's WindowSystem for UI management
- **Service Injection**: Proper `[PluginService]` attribute usage for Dalamud services
- **File-scoped namespaces**: Modern C# 10+ namespace syntax
- **Separation of Concerns**: Windows in separate folder, clear responsibility boundaries

### Key Components

1. **Quote Detection**: Monitors NPC dialogue chat messages for Nael quotes
2. **Fuzzy Matching**: Uses FuzzySharp library to match quotes (85% similarity threshold)
3. **Mechanic Mapping**: Maps detected quotes to mechanic keys
4. **Playback**: Either speaks TTS text or plays sound file based on configuration

## Credits

- Plugin structure based on [goatcorp/SamplePlugin](https://github.com/goatcorp/SamplePlugin)
- Mechanic detection inspired by [Eisenhuth/dalamud-nael](https://github.com/Eisenhuth/dalamud-nael)
- Uses [FuzzySharp](https://github.com/JakeBayer/FuzzySharp) for quote matching
- Uses [NAudio](https://github.com/naudio/NAudio) for sound file playback
- Uses Windows Speech Synthesis for TTS

## License

Feel free to modify and distribute!
