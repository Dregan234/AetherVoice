using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Runtime.InteropServices;
using System.Speech.Synthesis;
using System.Text.Json;
using System.Threading.Tasks;
using NAudio.CoreAudioApi;
using Dalamud.Plugin.Services;

namespace AetherVoice;

public class TTSManager : IDisposable
{
    private readonly Configuration configuration;
    private readonly IPluginLog log;
    private readonly string helperExePath;
    private int helperPid;
    private NamedPipeClientStream? pipeStream;
    private StreamWriter? pipeWriter;
    private volatile bool helperReady;
    private readonly object sendLock = new object();

    public static bool IsTTSAvailable => RuntimeInformation.IsOSPlatform(OSPlatform.Windows);

    #region P/Invoke

    [StructLayout(LayoutKind.Sequential)]
    private struct STARTUPINFO
    {
        public int cb;
        public IntPtr lpReserved;
        public IntPtr lpDesktop;
        public IntPtr lpTitle;
        public int dwX, dwY, dwXSize, dwYSize;
        public int dwXCountChars, dwYCountChars, dwFillAttribute;
        public int dwFlags;
        public short wShowWindow;
        public short cbReserved2;
        public IntPtr lpReserved2;
        public IntPtr hStdInput, hStdOutput, hStdError;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct STARTUPINFOEX
    {
        public STARTUPINFO StartupInfo;
        public IntPtr lpAttributeList;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct PROCESS_INFORMATION
    {
        public IntPtr hProcess;
        public IntPtr hThread;
        public int dwProcessId;
        public int dwThreadId;
    }

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool CreateProcess(
        string? lpApplicationName, string lpCommandLine,
        IntPtr lpProcessAttributes, IntPtr lpThreadAttributes,
        bool bInheritHandles, uint dwCreationFlags,
        IntPtr lpEnvironment, string? lpCurrentDirectory,
        ref STARTUPINFOEX lpStartupInfo, out PROCESS_INFORMATION lpProcessInformation);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool InitializeProcThreadAttributeList(
        IntPtr lpAttributeList, int dwAttributeCount, int dwFlags, ref IntPtr lpSize);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool UpdateProcThreadAttribute(
        IntPtr lpAttributeList, uint dwFlags, IntPtr Attribute,
        ref IntPtr lpValue, IntPtr cbSize, IntPtr lpPreviousValue, IntPtr lpReturnSize);

    [DllImport("kernel32.dll")]
    private static extern void DeleteProcThreadAttributeList(IntPtr lpAttributeList);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr OpenProcess(uint dwDesiredAccess, bool bInheritHandle, int dwProcessId);

    [DllImport("kernel32.dll")]
    private static extern bool CloseHandle(IntPtr hObject);

    private const uint EXTENDED_STARTUPINFO_PRESENT = 0x00080000;
    private const uint CREATE_NO_WINDOW = 0x08000000;
    private const uint PROCESS_CREATE_PROCESS = 0x0080;
    private static readonly IntPtr PROC_THREAD_ATTRIBUTE_PARENT_PROCESS = (IntPtr)0x00020000;

    #endregion

    public TTSManager(Configuration config, IPluginLog pluginLog, string helperPath)
    {
        configuration = config;
        log = pluginLog;
        helperExePath = helperPath;

        if (!IsTTSAvailable)
        {
            log.Warning("TTS is not available on this platform. Only sound files will work.");
            return;
        }

        Task.Run(() =>
        {
            try
            {
                LaunchHelper();
                SendConfigureDirect();
            }
            catch (Exception ex)
            {
                log.Error($"Failed to launch audio helper: {ex.Message}");
            }
        });
    }

    public static List<string> GetAvailableVoices()
    {
        if (!IsTTSAvailable)
            return new List<string>();

        try
        {
            using var synth = new SpeechSynthesizer();
            return synth.GetInstalledVoices()
                .Where(v => v.Enabled)
                .Select(v => v.VoiceInfo.Name)
                .ToList();
        }
        catch
        {
            return new List<string>();
        }
    }

    public static List<(string Id, string FriendlyName)> GetAvailableDevices()
    {
        try
        {
            using var enumerator = new MMDeviceEnumerator();
            var devices = new List<(string Id, string FriendlyName)>();
            foreach (var device in enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active))
            {
                devices.Add((device.ID, device.FriendlyName));
                device.Dispose();
            }
            return devices;
        }
        catch
        {
            return new List<(string, string)>();
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
                    SendCommand(new { Cmd = "PlayFile", Path = mechanicConfig.SoundFilePath });
                }
                else if (configuration.UseSoundFiles && string.IsNullOrEmpty(mechanicConfig.SoundFilePath) && !string.IsNullOrEmpty(mechanicConfig.TextPrompt))
                {
                    SendCommand(new { Cmd = "Speak", Text = mechanicConfig.TextPrompt });
                }
                else if (configuration.UseTTS && !string.IsNullOrEmpty(mechanicConfig.TextPrompt))
                {
                    SendCommand(new { Cmd = "Speak", Text = mechanicConfig.TextPrompt });
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
                SendCommand(new { Cmd = "Speak", Text = text });
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
                SendCommand(new { Cmd = "PlayFile", Path = filePath });
            }
            catch (Exception ex)
            {
                log.Error($"Error playing custom sound: {ex.Message}");
            }
        });
    }

    private void LaunchHelper()
    {
        if (!File.Exists(helperExePath))
        {
            log.Error($"Audio helper not found at: {helperExePath}");
            return;
        }

        helperReady = false;
        var helperDir = Path.GetDirectoryName(helperExePath)!;
        var pipeName = $"AetherVoice_{Guid.NewGuid():N}";
        var commandLine = $"\"{helperExePath}\" {pipeName}";

        if (!LaunchWithReparent(commandLine, helperDir))
        {
            log.Warning("Reparented launch failed, falling back to direct CreateProcess.");
            LaunchDirect(commandLine, helperDir);
        }

        try
        {
            pipeStream = new NamedPipeClientStream(".", pipeName, PipeDirection.Out);
            pipeStream.Connect(5000);
            pipeWriter = new StreamWriter(pipeStream) { AutoFlush = true };
            helperReady = true;
            log.Info($"Connected to audio helper (PID {helperPid}) via named pipe.");
        }
        catch (Exception ex)
        {
            log.Error($"Failed to connect to audio helper pipe: {ex.Message}");
            KillHelper();
        }
    }

    private bool LaunchWithReparent(string commandLine, string workingDir)
    {
        var explorerProcesses = Process.GetProcessesByName("explorer");
        if (explorerProcesses.Length == 0)
        {
            foreach (var p in explorerProcesses) p.Dispose();
            return false;
        }

        var explorerHandle = OpenProcess(PROCESS_CREATE_PROCESS, false, explorerProcesses[0].Id);
        foreach (var p in explorerProcesses) p.Dispose();

        if (explorerHandle == IntPtr.Zero)
        {
            log.Warning($"Could not open explorer.exe handle: Win32 error {Marshal.GetLastWin32Error()}");
            return false;
        }

        var lpSize = IntPtr.Zero;
        InitializeProcThreadAttributeList(IntPtr.Zero, 1, 0, ref lpSize);
        var lpAttributeList = Marshal.AllocHGlobal(lpSize);

        try
        {
            if (!InitializeProcThreadAttributeList(lpAttributeList, 1, 0, ref lpSize))
            {
                log.Warning($"InitializeProcThreadAttributeList failed: Win32 error {Marshal.GetLastWin32Error()}");
                return false;
            }

            if (!UpdateProcThreadAttribute(lpAttributeList, 0, PROC_THREAD_ATTRIBUTE_PARENT_PROCESS,
                ref explorerHandle, (IntPtr)IntPtr.Size, IntPtr.Zero, IntPtr.Zero))
            {
                log.Warning($"UpdateProcThreadAttribute failed: Win32 error {Marshal.GetLastWin32Error()}");
                DeleteProcThreadAttributeList(lpAttributeList);
                return false;
            }

            var si = new STARTUPINFOEX();
            si.StartupInfo.cb = Marshal.SizeOf(si);
            si.lpAttributeList = lpAttributeList;

            if (!CreateProcess(null, commandLine, IntPtr.Zero, IntPtr.Zero, false,
                CREATE_NO_WINDOW | EXTENDED_STARTUPINFO_PRESENT,
                IntPtr.Zero, workingDir, ref si, out var pi))
            {
                log.Warning($"CreateProcess with reparent failed: Win32 error {Marshal.GetLastWin32Error()}");
                DeleteProcThreadAttributeList(lpAttributeList);
                return false;
            }

            helperPid = pi.dwProcessId;
            CloseHandle(pi.hProcess);
            CloseHandle(pi.hThread);
            DeleteProcThreadAttributeList(lpAttributeList);

            log.Info($"Audio helper launched with PID {helperPid} (parent: explorer.exe).");
            return true;
        }
        finally
        {
            Marshal.FreeHGlobal(lpAttributeList);
            CloseHandle(explorerHandle);
        }
    }

    private void LaunchDirect(string commandLine, string workingDir)
    {
        var si = new STARTUPINFOEX();
        si.StartupInfo.cb = Marshal.SizeOf(si.StartupInfo);

        if (!CreateProcess(null, commandLine, IntPtr.Zero, IntPtr.Zero, false,
            CREATE_NO_WINDOW, IntPtr.Zero, workingDir, ref si, out var pi))
        {
            log.Error($"CreateProcess failed: Win32 error {Marshal.GetLastWin32Error()}");
            return;
        }

        helperPid = pi.dwProcessId;
        CloseHandle(pi.hProcess);
        CloseHandle(pi.hThread);
        log.Info($"Audio helper launched with PID {helperPid} (direct, not reparented).");
    }

    private void SendConfigureDirect()
    {
        if (pipeWriter == null)
            return;

        try
        {
            var json = JsonSerializer.Serialize(new
            {
                Cmd = "Configure",
                Voice = configuration.TTSVoice ?? "",
                Rate = configuration.TTSRate,
                TtsVolume = configuration.TTSVolume,
                SoundVolume = configuration.SoundFileVolume,
                DeviceId = configuration.AudioOutputDeviceId ?? ""
            });
            pipeWriter.WriteLine(json);
        }
        catch (Exception ex)
        {
            log.Warning($"Failed to send configure to audio helper: {ex.Message}");
        }
    }

    private void SendCommand(object command)
    {
        lock (sendLock)
        {
            try
            {
                if (!helperReady)
                {
                    EnsureHelperRunning();
                    if (!helperReady)
                        return;
                }

                if (pipeWriter == null)
                    return;

                var json = JsonSerializer.Serialize(command);
                pipeWriter.WriteLine(json);
            }
            catch (Exception ex)
            {
                log.Warning($"Failed to send command to audio helper: {ex.Message}");
                helperReady = false;
                KillHelper();
            }
        }
    }

    private void EnsureHelperRunning()
    {
        if (!IsHelperAlive())
        {
            log.Info("Audio helper not running, relaunching...");
            helperReady = false;
            LaunchHelper();
            SendConfigureDirect();
        }
    }

    private bool IsHelperAlive()
    {
        if (helperPid == 0)
            return false;

        try
        {
            var proc = Process.GetProcessById(helperPid);
            proc.Dispose();
            return true;
        }
        catch
        {
            return false;
        }
    }

    private void KillHelper()
    {
        try
        {
            pipeWriter?.Close();
            pipeWriter = null;

            pipeStream?.Close();
            pipeStream = null;

            if (helperPid != 0)
            {
                try
                {
                    var proc = Process.GetProcessById(helperPid);
                    proc.Kill();
                    proc.Dispose();
                }
                catch { }

                helperPid = 0;
            }
        }
        catch (Exception ex)
        {
            log.Error($"Error killing audio helper: {ex.Message}");
        }
    }

    public void Dispose()
    {
        try
        {
            if (pipeWriter != null)
            {
                var json = JsonSerializer.Serialize(new { Cmd = "Stop" });
                pipeWriter.WriteLine(json);
            }
        }
        catch { }

        try
        {
            pipeWriter?.Close();
            pipeWriter = null;

            pipeStream?.Close();
            pipeStream = null;

            if (helperPid != 0)
            {
                try
                {
                    var proc = Process.GetProcessById(helperPid);
                    if (!proc.WaitForExit(2000))
                        proc.Kill();
                    proc.Dispose();
                }
                catch { }

                helperPid = 0;
            }
        }
        catch (Exception ex)
        {
            log.Error($"Error disposing audio helper: {ex.Message}");
        }
    }
}
