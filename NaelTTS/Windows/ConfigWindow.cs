using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;

namespace NaelTTS.Windows;

public class ConfigWindow : Window, IDisposable
{
    private readonly Configuration configuration;
    private readonly Plugin plugin;

    private bool useTTS;
    private bool useSoundFiles;
    private int ttsVolume;
    private int ttsRate;

    private string[] mechanicKeys;
    private string[] mechanicTextPrompts;
    private string[] mechanicSoundPaths;

    public ConfigWindow(Plugin naelPlugin)
        : base("Nael TTS Configuration###NaelTTSConfig", ImGuiWindowFlags.None)
    {
        configuration = naelPlugin.Configuration;
        plugin = naelPlugin;

        Size = new Vector2(600, 700);
        SizeCondition = ImGuiCond.FirstUseEver;

        LoadConfiguration();
    }

    public void Dispose() { }

    private void LoadConfiguration()
    {
        useTTS = configuration.UseTTS;
        useSoundFiles = configuration.UseSoundFiles;
        ttsVolume = configuration.TTSVolume;
        ttsRate = configuration.TTSRate;

        mechanicKeys = new string[configuration.MechanicConfigs.Count];
        mechanicTextPrompts = new string[configuration.MechanicConfigs.Count];
        mechanicSoundPaths = new string[configuration.MechanicConfigs.Count];

        int index = 0;
        foreach (var kvp in configuration.MechanicConfigs)
        {
            mechanicKeys[index] = kvp.Key;
            mechanicTextPrompts[index] = kvp.Value.TextPrompt;
            mechanicSoundPaths[index] = kvp.Value.SoundFilePath;
            index++;
        }
    }

    public override void Draw()
    {
        ImGui.Text("Nael TTS Configuration");
        ImGui.Separator();

        // Audio Source Selection
        ImGui.Text("Audio Source:");
        if (ImGui.Checkbox("Use Text-to-Speech", ref useTTS))
        {
            if (useTTS) useSoundFiles = false;
        }

        if (ImGui.Checkbox("Use Sound Files", ref useSoundFiles))
        {
            if (useSoundFiles) useTTS = false;
        }

        ImGui.Separator();

        // TTS Settings
        if (useTTS)
        {
            ImGui.Text("TTS Settings:");
            ImGui.SliderInt("Volume", ref ttsVolume, 0, 100);
            ImGui.SliderInt("Speed", ref ttsRate, -10, 10);
            ImGui.Text("(Negative = slower, Positive = faster)");
            ImGui.Separator();
        }

        // Mechanic Configurations
        ImGui.Text("Mechanic Callouts:");
        ImGui.BeginChild("MechanicsList", new Vector2(0, 400));

        for (int i = 0; i < mechanicKeys.Length; i++)
        {
            ImGui.PushID(i);
            ImGui.Text(GetMechanicDisplayName(mechanicKeys[i]));

            if (useTTS)
            {
                ImGui.SetNextItemWidth(400);
                ImGui.InputText("TTS Text", ref mechanicTextPrompts[i], 100);
            }
            else if (useSoundFiles)
            {
                ImGui.SetNextItemWidth(400);
                ImGui.InputText("Sound File Path", ref mechanicSoundPaths[i], 500);
                ImGui.SameLine();
                if (ImGui.Button("Browse"))
                {
                    // File dialog would go here - for now user needs to paste path
                    ImGui.OpenPopup("FilePathHelp");
                }

                if (ImGui.BeginPopup("FilePathHelp"))
                {
                    ImGui.Text("Paste the full path to your sound file");
                    ImGui.Text("Supported formats: .wav, .mp3, .ogg");
                    ImGui.EndPopup();
                }
            }

            ImGui.Separator();
            ImGui.PopID();
        }

        ImGui.EndChild();

        ImGui.Separator();

        // Action Buttons
        if (ImGui.Button("Test Random"))
        {
            plugin.TestRandomQuote();
        }

        ImGui.SameLine();

        if (ImGui.Button("Save"))
        {
            SaveConfiguration();
            plugin.ReloadTTSManager();
        }

        ImGui.SameLine();

        if (ImGui.Button("Save and Close"))
        {
            SaveConfiguration();
            plugin.ReloadTTSManager();
            IsOpen = false;
        }
    }

    private void SaveConfiguration()
    {
        configuration.UseTTS = useTTS;
        configuration.UseSoundFiles = useSoundFiles;
        configuration.TTSVolume = ttsVolume;
        configuration.TTSRate = ttsRate;

        for (int i = 0; i < mechanicKeys.Length; i++)
        {
            if (configuration.MechanicConfigs.ContainsKey(mechanicKeys[i]))
            {
                configuration.MechanicConfigs[mechanicKeys[i]].TextPrompt = mechanicTextPrompts[i];
                configuration.MechanicConfigs[mechanicKeys[i]].SoundFilePath = mechanicSoundPaths[i];
            }
        }

        configuration.Save();
    }

    private string GetMechanicDisplayName(string key)
    {
        return key switch
        {
            "DynamoChariot" => "Dynamo → Chariot (In → Out)",
            "DynamoBeam" => "Dynamo → Beam (In → Stack)",
            "BeamChariot" => "Beam → Chariot (Stack → Out)",
            "BeamDynamo" => "Beam → Dynamo (Stack → In)",
            "DiveChariot" => "Dive → Chariot (Dive → Out)",
            "DiveDynamo" => "Dive → Dynamo (Dive → In)",
            "MeteorStreamDive" => "Meteor Stream → Dive (Spread → Dive)",
            "DiveBeam" => "Dive → Beam (Dive → Stack)",
            "DiveDynamoMeteorStream" => "Dive → Dynamo → Meteor (Dive → In → Spread)",
            "DynamoDiveMeteorStream" => "Dynamo → Dive → Meteor (In → Dive → Spread)",
            "ChariotBeamDive" => "Chariot → Beam → Dive (Out → Stack → Dive)",
            "ChariotDiveBeam" => "Chariot → Dive → Beam (Out → Dive → Stack)",
            "DynamoDiveBeam" => "Dynamo → Dive → Beam (In → Dive → Stack)",
            "DynamoChariotDive" => "Dynamo → Chariot → Dive (In → Out → Dive)",
            _ => key
        };
    }
}
