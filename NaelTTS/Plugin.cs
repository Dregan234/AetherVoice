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
using NaelTTS.Windows;

namespace NaelTTS;

public sealed class Plugin : IDalamudPlugin
{
    [PluginService] internal static IDalamudPluginInterface PluginInterface { get; private set; } = null!;
    [PluginService] internal static ICommandManager CommandManager { get; private set; } = null!;
    [PluginService] internal static IClientState ClientState { get; private set; } = null!;
    [PluginService] internal static IChatGui ChatGui { get; private set; } = null!;
    [PluginService] internal static IPluginLog Log { get; private set; } = null!;

    private const string CommandName = "/naeltts";

    public Configuration Configuration { get; init; }
    public readonly WindowSystem WindowSystem = new("NaelTTS");
    private ConfigWindow ConfigWindow { get; init; }

    private TTSManager? ttsManager;
    private readonly NaelQuotes naelQuotes;
    private Dictionary<string, string> naelQuotesDictionary = new();

    public Plugin()
    {
        Configuration = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();

        // Load Nael quotes from embedded JSON
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("NaelTTS.NaelQuotes.json");
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
            HelpMessage = "Toggle TTS\n/naeltts cfg → open configuration\n/naeltts test → test TTS with random quote"
        });

        // Tell the UI system that we want our windows to be drawn through the window system
        PluginInterface.UiBuilder.Draw += WindowSystem.Draw;

        // This adds a button to the plugin installer entry of this plugin which allows
        // toggling the display status of the configuration ui
        PluginInterface.UiBuilder.OpenConfigUi += ToggleConfigUi;
        PluginInterface.UiBuilder.OpenMainUi += ToggleConfigUi;

        ChatGui.ChatMessage += OnChatMessage;

        Log.Information($"===Nael TTS loaded===");
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
                ChatGui.Print($"[Nael TTS] {status}");
                break;
        }
    }

    public void TestRandomQuote()
    {
        if (naelQuotesDictionary.Count == 0)
        {
            ChatGui.Print("[Nael TTS] No quotes loaded!");
            return;
        }

        var random = new Random();
        var quoteKey = naelQuotesDictionary.Keys.ElementAt(random.Next(naelQuotesDictionary.Count));
        var mechanic = naelQuotesDictionary[quoteKey];
        ChatGui.Print($"[Nael TTS] Testing: {mechanic} | Enabled: {Configuration.Enabled}, UseTTS: {Configuration.UseTTS}, UseSoundFiles: {Configuration.UseSoundFiles}");
        Log.Info($"Testing mechanic: {mechanic}");
        ttsManager?.PlayMechanic(mechanic);
    }

    private void OnChatMessage(XivChatType type, int timestamp, ref SeString sender, ref SeString message, ref bool handled)
    {
        if (!Configuration.Enabled)
            return;

        // Check all message types, not just NPC dialogue
        foreach (var payload in message.Payloads)
        {
            if (payload is TextPayload { Text: not null } textPayload)
            {
                var mechanic = IdentifyMechanic(textPayload.Text);
                if (!string.IsNullOrEmpty(mechanic))
                {
                    Log.Info($"Detected Nael quote from any source: {textPayload.Text}");
                    ttsManager?.PlayMechanic(mechanic);
                }
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
        return match is { Score: >= 85 } ? naelQuotesDictionary[match.Value] : string.Empty;
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
            { GetQuote(6492), "DynamoChariot" },
            { GetQuote(6493), "DynamoBeam" },
            { GetQuote(6494), "BeamChariot" },
            { GetQuote(6495), "BeamDynamo" },
            { GetQuote(6496), "DiveChariot" },
            { GetQuote(6497), "DiveDynamo" },
            { GetQuote(6500), "MeteorStreamDive" },
            { GetQuote(6501), "DiveBeam" },
            { GetQuote(6502), "DiveDynamoMeteorStream" },
            { GetQuote(6503), "DynamoDiveMeteorStream" },
            { GetQuote(6504), "ChariotBeamDive" },
            { GetQuote(6505), "ChariotDiveBeam" },
            { GetQuote(6506), "DynamoDiveBeam" },
            { GetQuote(6507), "DynamoChariotDive" }
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
