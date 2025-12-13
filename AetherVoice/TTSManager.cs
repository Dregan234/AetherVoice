using System;
using System.IO;
using System.Speech.Synthesis;
using System.Threading.Tasks;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using Dalamud.Plugin.Services;

namespace AetherVoice;

public class TTSManager : IDisposable
{
    private readonly Configuration configuration;
    private readonly IPluginLog log;
    private SpeechSynthesizer? synthesizer;
    private IWavePlayer? waveOut;
    private WaveStream? audioStream;
    private readonly object audioLock = new object();

    public TTSManager(Configuration config, IPluginLog pluginLog)
    {
        configuration = config;
        log = pluginLog;

        // Always initialize TTS for fallback support
        try
        {
            synthesizer = new SpeechSynthesizer();
            synthesizer.Rate = configuration.TTSRate;
        }
        catch (Exception ex)
        {
            log.Error($"Failed to initialize TTS: {ex.Message}");
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
                else if (configuration.UseSoundFiles && string.IsNullOrEmpty(mechanicConfig.SoundFilePath) && !string.IsNullOrEmpty(mechanicConfig.TextPrompt))
                {
                    // Fallback to TTS if sound file mode is enabled but no file path is set
                    SpeakText(mechanicConfig.TextPrompt);
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

    public void PlayCustomTTS(string text)
    {
        if (!configuration.Enabled || string.IsNullOrEmpty(text))
            return;

        Task.Run(() =>
        {
            try
            {
                SpeakText(text);
            }
            catch (Exception ex)
            {
                log.Error($"Error playing custom TTS: {ex.Message}");
            }
        });
    }

    public void PlayCustomSound(string filePath)
    {
        if (!configuration.Enabled || string.IsNullOrEmpty(filePath))
            return;

        Task.Run(() =>
        {
            try
            {
                PlaySoundFile(filePath);
            }
            catch (Exception ex)
            {
                log.Error($"Error playing custom sound: {ex.Message}");
            }
        });
    }

    private void SpeakText(string text)
    {
        if (synthesizer == null)
            return;

        lock (audioLock)
        {
            try
            {
                // Generate TTS to a memory stream and play through NAudio for volume control
                var stream = new MemoryStream();
                synthesizer.SetOutputToWaveStream(stream);
                synthesizer.Rate = configuration.TTSRate;
                synthesizer.Speak(text);

                // Reset stream position and play through NAudio
                stream.Position = 0;

                StopAudioInternal();
                audioStream = new WaveFileReader(stream);

                // Use VolumeSampleProvider for independent volume control
                var volumeProvider = new VolumeSampleProvider(audioStream.ToSampleProvider())
                {
                    Volume = configuration.TTSVolume / 100f
                };

                waveOut = new WaveOutEvent();
                waveOut.Init(volumeProvider);
                waveOut.Play();

                // Reset synthesizer output to default
                synthesizer.SetOutputToDefaultAudioDevice();
            }
            catch (Exception ex)
            {
                log.Error($"TTS error: {ex.Message}");
            }
        }
    }

    private void PlaySoundFile(string filePath)
    {
        if (!File.Exists(filePath))
        {
            log.Warning($"Sound file not found: {filePath}");
            return;
        }

        lock (audioLock)
        {
            try
            {
                // Stop any currently playing audio
                StopAudioInternal();

                audioStream = new AudioFileReader(filePath);

                // Use VolumeSampleProvider for independent volume control
                var volumeProvider = new VolumeSampleProvider(audioStream.ToSampleProvider())
                {
                    Volume = configuration.SoundFileVolume / 100f
                };

                waveOut = new WaveOutEvent();
                waveOut.Init(volumeProvider);
                waveOut.Play();
            }
            catch (Exception ex)
            {
                log.Error($"Error playing sound file: {ex.Message}");
                StopAudioInternal();
            }
        }
    }

    private void StopAudioInternal()
    {
        try
        {
            waveOut?.Stop();
            waveOut?.Dispose();
            waveOut = null;

            audioStream?.Dispose();
            audioStream = null;
        }
        catch (Exception ex)
        {
            log.Error($"Error stopping audio: {ex.Message}");
        }
    }

    private void StopAudio()
    {
        lock (audioLock)
        {
            StopAudioInternal();
        }
    }

    public void Dispose()
    {
        StopAudio();
        lock (audioLock)
        {
            synthesizer?.Dispose();
            synthesizer = null;
        }
    }
}
