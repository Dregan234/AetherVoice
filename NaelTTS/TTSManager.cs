using System;
using System.IO;
using System.Speech.Synthesis;
using System.Threading.Tasks;
using NAudio.Wave;
using Dalamud.Plugin.Services;

namespace NaelTTS;

public class TTSManager : IDisposable
{
    private readonly Configuration configuration;
    private readonly IPluginLog log;
    private SpeechSynthesizer? synthesizer;
    private WaveOutEvent? waveOut;
    private AudioFileReader? audioFile;

    public TTSManager(Configuration config, IPluginLog pluginLog)
    {
        configuration = config;
        log = pluginLog;

        if (configuration.UseTTS)
        {
            try
            {
                synthesizer = new SpeechSynthesizer();
                synthesizer.Volume = configuration.TTSVolume;
                synthesizer.Rate = configuration.TTSRate;
            }
            catch (Exception ex)
            {
                log.Error($"Failed to initialize TTS: {ex.Message}");
            }
        }
    }

    public void PlayMechanic(string mechanicKey)
    {
        if (!configuration.Enabled)
            return;

        if (!configuration.MechanicConfigs.TryGetValue(mechanicKey, out var mechanicConfig))
        {
            log.Warning($"No configuration found for mechanic: {mechanicKey}");
            return;
        }

        Task.Run(() =>
        {
            try
            {
                if (configuration.UseSoundFiles && !string.IsNullOrEmpty(mechanicConfig.SoundFilePath))
                {
                    PlaySoundFile(mechanicConfig.SoundFilePath);
                }
                else if (configuration.UseTTS && !string.IsNullOrEmpty(mechanicConfig.TextPrompt))
                {
                    SpeakText(mechanicConfig.TextPrompt);
                }
            }
            catch (Exception ex)
            {
                log.Error($"Error playing mechanic {mechanicKey}: {ex.Message}");
            }
        });
    }

    private void SpeakText(string text)
    {
        if (synthesizer == null)
            return;

        try
        {
            synthesizer.Volume = configuration.TTSVolume;
            synthesizer.Rate = configuration.TTSRate;
            synthesizer.SpeakAsync(text);
        }
        catch (Exception ex)
        {
            log.Error($"TTS error: {ex.Message}");
        }
    }

    private void PlaySoundFile(string filePath)
    {
        if (!File.Exists(filePath))
        {
            log.Warning($"Sound file not found: {filePath}");
            return;
        }

        try
        {
            // Stop any currently playing audio
            StopAudio();

            audioFile = new AudioFileReader(filePath);
            waveOut = new WaveOutEvent();
            waveOut.Init(audioFile);
            waveOut.Volume = configuration.TTSVolume / 100f;
            waveOut.PlaybackStopped += OnPlaybackStopped;
            waveOut.Play();
        }
        catch (Exception ex)
        {
            log.Error($"Error playing sound file: {ex.Message}");
            StopAudio();
        }
    }

    private void OnPlaybackStopped(object? sender, StoppedEventArgs e)
    {
        StopAudio();
    }

    private void StopAudio()
    {
        try
        {
            waveOut?.Stop();
            waveOut?.Dispose();
            waveOut = null;

            audioFile?.Dispose();
            audioFile = null;
        }
        catch (Exception ex)
        {
            log.Error($"Error stopping audio: {ex.Message}");
        }
    }

    public void Dispose()
    {
        StopAudio();
        synthesizer?.Dispose();
    }
}
