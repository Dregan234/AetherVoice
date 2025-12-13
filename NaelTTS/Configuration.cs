using Dalamud.Configuration;
using System;
using System.Collections.Generic;

namespace NaelTTS;

[Serializable]
public class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 1;
    public bool Enabled { get; set; } = true;

    // TTS Settings
    public bool UseTTS { get; set; } = true;
    public bool UseSoundFiles { get; set; } = false;
    public int TTSVolume { get; set; } = 100;
    public int TTSRate { get; set; } = 0; // -10 to 10

    // Custom text prompts for each mechanic
    public Dictionary<string, MechanicConfig> MechanicConfigs { get; set; } = new()
    {
        { "DynamoChariot", new MechanicConfig { TextPrompt = "in out", SoundFilePath = "" } },
        { "DynamoBeam", new MechanicConfig { TextPrompt = "in stack", SoundFilePath = "" } },
        { "BeamChariot", new MechanicConfig { TextPrompt = "stack out", SoundFilePath = "" } },
        { "BeamDynamo", new MechanicConfig { TextPrompt = "stack in", SoundFilePath = "" } },
        { "DiveChariot", new MechanicConfig { TextPrompt = "dive out", SoundFilePath = "" } },
        { "DiveDynamo", new MechanicConfig { TextPrompt = "dive in", SoundFilePath = "" } },
        { "MeteorStreamDive", new MechanicConfig { TextPrompt = "spread dive", SoundFilePath = "" } },
        { "DiveBeam", new MechanicConfig { TextPrompt = "dive stack", SoundFilePath = "" } },
        { "DiveDynamoMeteorStream", new MechanicConfig { TextPrompt = "dive in spread", SoundFilePath = "" } },
        { "DynamoDiveMeteorStream", new MechanicConfig { TextPrompt = "in dive spread", SoundFilePath = "" } },
        { "ChariotBeamDive", new MechanicConfig { TextPrompt = "out stack dive", SoundFilePath = "" } },
        { "ChariotDiveBeam", new MechanicConfig { TextPrompt = "out dive stack", SoundFilePath = "" } },
        { "DynamoDiveBeam", new MechanicConfig { TextPrompt = "in dive stack", SoundFilePath = "" } },
        { "DynamoChariotDive", new MechanicConfig { TextPrompt = "in out dive", SoundFilePath = "" } }
    };

    // The below exists just to make saving less cumbersome
    public void Save()
    {
        Plugin.PluginInterface.SavePluginConfig(this);
    }
}

[Serializable]
public class MechanicConfig
{
    public string TextPrompt { get; set; } = "";
    public string SoundFilePath { get; set; } = "";
}
