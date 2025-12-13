using System;
using System.Numerics;
using System.Windows.Forms;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;

namespace AetherVoice.Windows;

public class ConfigWindow : Window, IDisposable
{
    private readonly Configuration configuration;
    private readonly Plugin plugin;

    private bool useTTS;
    private bool useSoundFiles;
    private int ttsVolume;
    private int ttsRate;
    private int soundFileVolume;

    private string[] mechanicKeys = Array.Empty<string>();
    private string[] mechanicTextPrompts = Array.Empty<string>();
    private string[] mechanicSoundPaths = Array.Empty<string>();

    private string newTriggerText = "";
    private string newResponseText = "";
    private string newSoundPath = "";
    private bool newUseExactMatch = false;

    public ConfigWindow(Plugin naelPlugin)
        : base("AetherVoice Configuration###AetherVoiceConfig", ImGuiWindowFlags.None)
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
        soundFileVolume = configuration.SoundFileVolume;

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
        ImGui.Text("AetherVoice Configuration");
        ImGui.Separator();

        // Tab bar
        if (ImGui.BeginTabBar("ConfigTabs"))
        {
            if (ImGui.BeginTabItem("General"))
            {
                DrawGeneralTab();
                ImGui.EndTabItem();
            }

            if (ImGui.BeginTabItem("Nael Mechanics"))
            {
                DrawMechanicsTab();
                ImGui.EndTabItem();
            }

            if (ImGui.BeginTabItem("Custom TTS"))
            {
                DrawCustomTTSTab();
                ImGui.EndTabItem();
            }

            ImGui.EndTabBar();
        }

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

    private void DrawGeneralTab()
    {
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
        }

        // Sound File Settings
        if (useSoundFiles)
        {
            ImGui.Text("Sound File Settings:");
            ImGui.SliderInt("Volume", ref soundFileVolume, 0, 100);
        }
    }

    private void DrawMechanicsTab()
    {
        // Mechanic Configurations
        ImGui.Text("Mechanic Callouts:");
        ImGui.BeginChild("MechanicsList", new Vector2(0, 500));

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
                    var dialog = new OpenFileDialog
                    {
                        Filter = "Audio Files (*.wav;*.mp3;*.ogg)|*.wav;*.mp3;*.ogg|All Files (*.*)|*.*",
                        Title = "Select Sound File",
                        CheckFileExists = true
                    };

                    if (dialog.ShowDialog() == DialogResult.OK)
                    {
                        mechanicSoundPaths[i] = dialog.FileName;
                    }
                }
            }

            ImGui.Separator();
            ImGui.PopID();
        }

        ImGui.EndChild();
    }

    private void DrawCustomTTSTab()
    {
        ImGui.Text("Add custom chat triggers to play TTS or sound files.");
        ImGui.Separator();

        // Add new trigger section
        ImGui.Text("Add New Trigger:");
        ImGui.SetNextItemWidth(400);
        ImGui.InputTextWithHint("##triggerText", "Text to match in chat", ref newTriggerText, 200);

        ImGui.SetNextItemWidth(400);
        ImGui.InputTextWithHint("##responseText", "TTS response text", ref newResponseText, 200);

        ImGui.SetNextItemWidth(400);
        ImGui.InputTextWithHint("##soundPath", "Or sound file path (optional)", ref newSoundPath, 500);
        ImGui.SameLine();
        if (ImGui.Button("Browse##new"))
        {
            var dialog = new OpenFileDialog
            {
                Filter = "Audio Files (*.wav;*.mp3;*.ogg)|*.wav;*.mp3;*.ogg|All Files (*.*)|*.*",
                Title = "Select Sound File",
                CheckFileExists = true
            };

            if (dialog.ShowDialog() == DialogResult.OK)
            {
                newSoundPath = dialog.FileName;
            }
        }

        ImGui.Checkbox("Exact match (case-insensitive)", ref newUseExactMatch);
        ImGui.SameLine();
        if (ImGui.Button("Add Trigger"))
        {
            if (!string.IsNullOrEmpty(newTriggerText) && (!string.IsNullOrEmpty(newResponseText) || !string.IsNullOrEmpty(newSoundPath)))
            {
                configuration.CustomTriggers.Add(new CustomTrigger
                {
                    TriggerText = newTriggerText,
                    ResponseText = newResponseText,
                    SoundFilePath = newSoundPath,
                    UseExactMatch = newUseExactMatch,
                    Enabled = true
                });

                newTriggerText = "";
                newResponseText = "";
                newSoundPath = "";
                newUseExactMatch = false;
            }
        }

        ImGui.Separator();
        ImGui.Text("Existing Triggers:");
        ImGui.BeginChild("CustomTriggersList", new Vector2(0, 400));

        for (int i = 0; i < configuration.CustomTriggers.Count; i++)
        {
            var trigger = configuration.CustomTriggers[i];
            ImGui.PushID(i);

            bool enabled = trigger.Enabled;
            if (ImGui.Checkbox("##enabled", ref enabled))
            {
                trigger.Enabled = enabled;
            }
            ImGui.SameLine();

            ImGui.Text($"Trigger: {trigger.TriggerText}");

            ImGui.SetNextItemWidth(400);
            string triggerText = trigger.TriggerText;
            if (ImGui.InputText("Match Text", ref triggerText, 200))
            {
                trigger.TriggerText = triggerText;
            }

            ImGui.SetNextItemWidth(400);
            string responseText = trigger.ResponseText;
            if (ImGui.InputText("TTS Response", ref responseText, 200))
            {
                trigger.ResponseText = responseText;
            }

            ImGui.SetNextItemWidth(400);
            string soundPath = trigger.SoundFilePath;
            if (ImGui.InputText("Sound File", ref soundPath, 500))
            {
                trigger.SoundFilePath = soundPath;
            }
            ImGui.SameLine();
            if (ImGui.Button($"Browse##{i}"))
            {
                var dialog = new OpenFileDialog
                {
                    Filter = "Audio Files (*.wav;*.mp3;*.ogg)|*.wav;*.mp3;*.ogg|All Files (*.*)|*.*",
                    Title = "Select Sound File",
                    CheckFileExists = true
                };

                if (dialog.ShowDialog() == DialogResult.OK)
                {
                    trigger.SoundFilePath = dialog.FileName;
                }
            }

            bool exactMatch = trigger.UseExactMatch;
            if (ImGui.Checkbox("Exact Match", ref exactMatch))
            {
                trigger.UseExactMatch = exactMatch;
            }

            ImGui.SameLine();
            if (ImGui.Button($"Delete##{i}"))
            {
                configuration.CustomTriggers.RemoveAt(i);
                ImGui.PopID();
                break;
            }

            ImGui.Separator();
            ImGui.PopID();
        }

        ImGui.EndChild();
    }

    private void SaveConfiguration()
    {
        configuration.UseTTS = useTTS;
        configuration.UseSoundFiles = useSoundFiles;
        configuration.TTSVolume = ttsVolume;
        configuration.TTSRate = ttsRate;
        configuration.SoundFileVolume = soundFileVolume;

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
