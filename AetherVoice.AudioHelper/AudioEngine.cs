using System;
using System.IO;
using System.Speech.Synthesis;
using System.Threading.Tasks;
using NAudio.CoreAudioApi;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace AetherVoice.AudioHelper
{
    public class AudioEngine : IDisposable
    {
        private SpeechSynthesizer synthesizer;
        private IWavePlayer waveOut;
        private WaveStream audioStream;
        private MMDevice activeDevice;
        private readonly object audioLock = new object();

        private string voice = "";
        private int rate = 0;
        private float ttsVolume = 1.0f;
        private float soundVolume = 1.0f;
        private string deviceId = "";

        public AudioEngine()
        {
            try
            {
                synthesizer = new SpeechSynthesizer();
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Failed to initialize TTS: {ex.Message}");
            }
        }

        public void Configure(CommandMessage msg)
        {
            voice = msg.Voice ?? "";
            rate = msg.Rate;
            ttsVolume = msg.TtsVolume / 100f;
            soundVolume = msg.SoundVolume / 100f;
            deviceId = msg.DeviceId ?? "";

            if (synthesizer != null)
            {
                synthesizer.Rate = rate;
                if (!string.IsNullOrEmpty(voice))
                {
                    try
                    {
                        synthesizer.SelectVoice(voice);
                    }
                    catch (Exception ex)
                    {
                        Console.Error.WriteLine($"Could not select voice '{voice}': {ex.Message}");
                    }
                }
            }
        }

        public void Speak(string text)
        {
            if (synthesizer == null || string.IsNullOrEmpty(text))
                return;

            Task.Run(() =>
            {
                lock (audioLock)
                {
                    try
                    {
                        if (!string.IsNullOrEmpty(voice))
                        {
                            try { synthesizer.SelectVoice(voice); }
                            catch { }
                        }

                        var stream = new MemoryStream();
                        synthesizer.SetOutputToWaveStream(stream);
                        synthesizer.Rate = rate;
                        synthesizer.Speak(text);

                        stream.Position = 0;

                        StopAudioInternal();
                        audioStream = new WaveFileReader(stream);

                        var volumeProvider = new VolumeSampleProvider(audioStream.ToSampleProvider())
                        {
                            Volume = ttsVolume
                        };

                        waveOut = CreateWavePlayer();
                        var targetFormat = GetDeviceMixFormat();
                        var finalProvider = EnsureCompatibleFormat(volumeProvider, targetFormat);
                        waveOut.Init(finalProvider);
                        waveOut.Play();

                        synthesizer.SetOutputToDefaultAudioDevice();
                    }
                    catch (Exception ex)
                    {
                        Console.Error.WriteLine($"TTS error: {ex.Message}");
                    }
                }
            });
        }

        public void PlayFile(string filePath)
        {
            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
                return;

            Task.Run(() =>
            {
                lock (audioLock)
                {
                    try
                    {
                        StopAudioInternal();
                        audioStream = new AudioFileReader(filePath);

                        var volumeProvider = new VolumeSampleProvider(audioStream.ToSampleProvider())
                        {
                            Volume = soundVolume
                        };

                        waveOut = CreateWavePlayer();
                        var targetFormat = GetDeviceMixFormat();
                        var finalProvider = EnsureCompatibleFormat(volumeProvider, targetFormat);
                        waveOut.Init(finalProvider);
                        waveOut.Play();
                    }
                    catch (Exception ex)
                    {
                        Console.Error.WriteLine($"Sound file error: {ex.Message}");
                        StopAudioInternal();
                    }
                }
            });
        }

        public void Stop()
        {
            lock (audioLock)
            {
                StopAudioInternal();
            }
        }

        private void StopAudioInternal()
        {
            try
            {
                if (waveOut != null)
                {
                    waveOut.Stop();
                    waveOut.Dispose();
                    waveOut = null;
                }

                if (activeDevice != null)
                {
                    activeDevice.Dispose();
                    activeDevice = null;
                }

                if (audioStream != null)
                {
                    audioStream.Dispose();
                    audioStream = null;
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error during StopAudioInternal: {ex.Message}");
            }
        }

        private IWavePlayer CreateWavePlayer()
        {
            if (string.IsNullOrEmpty(deviceId))
                return new WasapiOut(AudioClientShareMode.Shared, true, 100);

            try
            {
                var enumerator = new MMDeviceEnumerator();
                var device = enumerator.GetDevice(deviceId);
                enumerator.Dispose();
                activeDevice = device;
                return new WasapiOut(device, AudioClientShareMode.Shared, true, 100);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Audio device '{deviceId}' not found, falling back to default: {ex.Message}");
                return new WasapiOut(AudioClientShareMode.Shared, true, 100);
            }
        }

        private WaveFormat GetDeviceMixFormat()
        {
            using (var enumerator = new MMDeviceEnumerator())
            {
                MMDevice device;
                try
                {
                    device = string.IsNullOrEmpty(deviceId)
                        ? enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia)
                        : enumerator.GetDevice(deviceId);
                }
                catch
                {
                    device = enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
                }

                var audioClient = device.AudioClient;
                var format = audioClient.MixFormat;
                audioClient.Dispose();
                device.Dispose();
                return format;
            }
        }

        private static ISampleProvider EnsureCompatibleFormat(ISampleProvider source, WaveFormat targetFormat)
        {
            var result = source;

            if (result.WaveFormat.SampleRate != targetFormat.SampleRate)
                result = new WdlResamplingSampleProvider(result, targetFormat.SampleRate);

            if (result.WaveFormat.Channels == 1 && targetFormat.Channels >= 2)
                result = new MonoToStereoSampleProvider(result);

            return result;
        }

        public void Dispose()
        {
            Stop();
            lock (audioLock)
            {
                if (synthesizer != null)
                {
                    synthesizer.Dispose();
                    synthesizer = null;
                }
            }
        }
    }
}
