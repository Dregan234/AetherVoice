using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using Dalamud.Game;
using Dalamud.Game.Command;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.IoC;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using FuzzySharp;
using AetherVoice.Windows;

namespace AetherVoice;

public sealed class Plugin : IDalamudPlugin
{
    [PluginService] internal static IDalamudPluginInterface PluginInterface { get; private set; } = null!;
    [PluginService] internal static ICommandManager CommandManager { get; private set; } = null!;
    [PluginService] internal static IClientState ClientState { get; private set; } = null!;
    [PluginService] internal static IChatGui ChatGui { get; private set; } = null!;
    [PluginService] internal static IPluginLog Log { get; private set; } = null!;

    private const string CommandName = "/aethervoice";

    public Configuration Configuration { get; init; }
    public readonly WindowSystem WindowSystem = new("AetherVoice");
    private ConfigWindow ConfigWindow { get; init; }

    private TTSManager? ttsManager;
    private readonly NaelQuotes naelQuotes;
    private Dictionary<string, string> naelQuotesDictionary = new();

    public Plugin()
    {
        Configuration = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();

        // Load Nael quotes from embedded JSON
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("AetherVoice.NaelQuotes.json");
        if (stream == null)
        {
            Log.Error("Failed to load NaelQuotes.json");
            naelQuotes = new NaelQuotes { Quotes = new List<Quote>() };
        }
        else
        {
            using var streamReader = new StreamReader(stream);
            var json = streamReader.ReadToEnd();
            naelQuotes = JsonSerializer.Deserialize<NaelQuotes>(json);
        }

        LoadQuotesDictionary();

        ttsManager = new TTSManager(Configuration, Log);

        ConfigWindow = new ConfigWindow(this);
        WindowSystem.AddWindow(ConfigWindow);

        CommandManager.AddHandler(CommandName, new CommandInfo(OnCommand)
        {
            HelpMessage = "Toggle TTS\n/aethervoice cfg → open configuration\n/aethervoice test → test TTS with random quote"
        });

        // Tell the UI system that we want our windows to be drawn through the window system
        PluginInterface.UiBuilder.Draw += WindowSystem.Draw;

        // This adds a button to the plugin installer entry of this plugin which allows
        // toggling the display status of the configuration ui
        PluginInterface.UiBuilder.OpenConfigUi += ToggleConfigUi;
        PluginInterface.UiBuilder.OpenMainUi += ToggleConfigUi;

        ChatGui.ChatMessage += OnChatMessage;

        Log.Information($"===AetherVoice loaded===");
    }

    private void OnCommand(string command, string args)
    {
        switch (args.ToLower())
        {
            case "cfg":
            case "config":
                ToggleConfigUi();
                break;
            case "test":
                TestRandomQuote();
                break;
            default:
                Configuration.Enabled = !Configuration.Enabled;
                Configuration.Save();
                var status = Configuration.Enabled ? "enabled" : "disabled";
                ChatGui.Print($"[AetherVoice] {status}");
                break;
        }
    }

    public void TestRandomQuote()
    {
        if (naelQuotesDictionary.Count == 0)
        {
            ChatGui.Print("[AetherVoice] No quotes loaded!");
            return;
        }

        var random = new Random();
        var quoteKey = naelQuotesDictionary.Keys.ElementAt(random.Next(naelQuotesDictionary.Count));
        var mechanic = naelQuotesDictionary[quoteKey];
        ChatGui.Print($"[AetherVoice] Testing: {mechanic} | Enabled: {Configuration.Enabled}, UseTTS: {Configuration.UseTTS}, UseSoundFiles: {Configuration.UseSoundFiles}");
        Log.Info($"Testing mechanic: {mechanic}");
        ttsManager?.PlayMechanic(mechanic);
    }

    private void OnChatMessage(XivChatType type, int timestamp, ref SeString sender, ref SeString message, ref bool handled)
    {
        if (!Configuration.Enabled)
            return;

        var fullText = message.TextValue;
        if (string.IsNullOrEmpty(fullText))
            return;

        // Check for Nael quotes first
        if (Configuration.EnableNaelQuotes)
        {
            var mechanic = IdentifyMechanic(fullText);
            if (!string.IsNullOrEmpty(mechanic))
            {
                Log.Info($"Detected Nael quote: {fullText}");
                ttsManager?.PlayMechanic(mechanic);
                return; // Don't check custom triggers if we matched a Nael quote
            }
        }

        // Check for custom triggers
        if (Configuration.EnableCustomTriggers)
        {
            CheckCustomTriggers(fullText);
        }
    }

    private void CheckCustomTriggers(string text)
    {
        foreach (var trigger in Configuration.CustomTriggers)
        {
            if (!trigger.Enabled)
                continue;

            bool matched = trigger.UseExactMatch
                ? text.Equals(trigger.TriggerText, StringComparison.OrdinalIgnoreCase)
                : text.Contains(trigger.TriggerText, StringComparison.OrdinalIgnoreCase);

            if (matched)
            {
                Log.Info($"Custom trigger matched: {trigger.TriggerText}");

                // Prioritize sound file if path is provided, otherwise use TTS
                if (!string.IsNullOrEmpty(trigger.SoundFilePath))
                {
                    ttsManager?.PlayCustomSound(trigger.SoundFilePath);
                }
                else if (!string.IsNullOrEmpty(trigger.ResponseText))
                {
                    ttsManager?.PlayCustomTTS(trigger.ResponseText);
                }

                break; // Only trigger once per message
            }
        }
    }

    private static bool CheckForNael(string name)
    {
        var names = new[]
        {
            "Nael deus Darnus", // EN/DE/FR
            "ネール・デウス・ダーナス", // JA
            "奈尔·神·达纳斯" // CN
        };

        return names.Contains(name);
    }

    private string IdentifyMechanic(string input)
    {
        var match = Process.ExtractOne(input, naelQuotesDictionary.Keys, s => s);
        return match is { Score: >= 95 } ? naelQuotesDictionary[match.Value] : string.Empty;
    }

    private string GetQuote(int id)
    {
        var quote = naelQuotes.Quotes.Find(q => q.ID == id);
        var quoteText = ClientState.ClientLanguage switch
        {
            ClientLanguage.English => quote.Text.Text_en,
            ClientLanguage.French => quote.Text.Text_fr,
            ClientLanguage.German => quote.Text.Text_de,
            ClientLanguage.Japanese => quote.Text.Text_ja,
            _ => quote.Text.Text_chs
        };
        quoteText = quoteText.Replace("\n\n", "\n");

        return quoteText;
    }

    private void LoadQuotesDictionary()
    {
        naelQuotesDictionary = new Dictionary<string, string>
        {
            { GetQuote(6492), "DiveDynamo" },        // From on high I descend, the hallowed moon to call! (Dive & In)
            { GetQuote(6493), "DiveChariot" },       // From on high I descend, the iron path to walk! (Dive & Out)
            { GetQuote(6494), "BeamDynamo" },        // Take fire, O hallowed moon! (Stack & In)
            { GetQuote(6495), "BeamChariot" },       // Blazing path, lead me to iron rule! (Stack & Out)
            { GetQuote(6496), "DynamoBeam" },        // O hallowed moon, take fire and scorch my foes! (In & Stack)
            { GetQuote(6497), "DynamoChariot" },     // O hallowed moon, shine you the iron path! (In & Out)
            { GetQuote(6500), "ChariotDiveBeam" },   // Unbending iron, descend with fiery edge! (Out & Dive & Stack)
            { GetQuote(6501), "DiveBeam" },          // Fleeting light! 'Neath the red moon, scorch you the earth! (Dive & Stack)
            { GetQuote(6502), "MeteorStreamDive" },  // Fleeting light! Amid a rain of stars, exalt you the red moon! (Spread & Dive)
            { GetQuote(6503), "DynamoDiveMeteorStream" },  // From hallowed moon I descend, a rain of stars to bring! (In & Dive & Spread)
            { GetQuote(6504), "DiveDynamoMeteorStream" },  // From on high I descend, the moon and stars to bring! (Dive & In & Spread)
            { GetQuote(6506), "DynamoChariotDive" }, // From hallowed moon I bare iron, in my descent to wield! (In & Out & Dive)
            { GetQuote(6507), "DynamoDiveBeam" },     // From hallowed moon I descend, upon burning earth to tread! (In & Dive & Stack)
            { GetQuote(6508), "ChariotBeamDive" }     // Unbending iron, take fire and descend! (Out & Stack & Dive)
        };
    }

    public void ReloadTTSManager()
    {
        ttsManager?.Dispose();
        ttsManager = new TTSManager(Configuration, Log);
    }

    public void ToggleConfigUi() => ConfigWindow.Toggle();

    public void Dispose()
    {
        // Unregister all actions to not leak anything during disposal of plugin
        PluginInterface.UiBuilder.Draw -= WindowSystem.Draw;
        PluginInterface.UiBuilder.OpenConfigUi -= ToggleConfigUi;
        PluginInterface.UiBuilder.OpenMainUi -= ToggleConfigUi;

        WindowSystem.RemoveAllWindows();
        ConfigWindow.Dispose();

        ttsManager?.Dispose();
        ChatGui.ChatMessage -= OnChatMessage;
        CommandManager.RemoveHandler(CommandName);
    }
}
