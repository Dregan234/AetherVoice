using System;
using System.IO;
using System.IO.Pipes;
using System.Reflection;
using System.Text.Json;

namespace AetherVoice.AudioHelper
{
    static class Program
    {
        [STAThread]
        static void Main(string[] args)
        {
            var logPath = Path.Combine(
                Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location),
                "helper.log");

            try
            {
                File.AppendAllText(logPath, $"[{DateTime.Now}] Helper starting. Args: {string.Join(" ", args)}\n");

                if (args.Length == 0)
                {
                    File.AppendAllText(logPath, $"[{DateTime.Now}] No pipe name argument, exiting.\n");
                    return;
                }

                var pipeName = args[0];
                File.AppendAllText(logPath, $"[{DateTime.Now}] Creating pipe server: {pipeName}\n");

                using (var engine = new AudioEngine())
                using (var pipeServer = new NamedPipeServerStream(pipeName, PipeDirection.In))
                {
                    File.AppendAllText(logPath, $"[{DateTime.Now}] Waiting for connection...\n");
                    pipeServer.WaitForConnection();
                    File.AppendAllText(logPath, $"[{DateTime.Now}] Connected. Reading commands.\n");

                    var reader = new StreamReader(pipeServer);
                    string line;
                    while ((line = reader.ReadLine()) != null)
                    {
                        try
                        {
                            var msg = JsonSerializer.Deserialize<CommandMessage>(line);
                            if (msg == null || string.IsNullOrEmpty(msg.Cmd))
                                continue;

                            switch (msg.Cmd)
                            {
                                case "Configure":
                                    engine.Configure(msg);
                                    break;
                                case "Speak":
                                    engine.Speak(msg.Text);
                                    break;
                                case "PlayFile":
                                    engine.PlayFile(msg.Path);
                                    break;
                                case "Stop":
                                    engine.Stop();
                                    break;
                            }
                        }
                        catch (Exception ex)
                        {
                            File.AppendAllText(logPath, $"[{DateTime.Now}] Command error: {ex.Message}\n");
                        }
                    }

                    File.AppendAllText(logPath, $"[{DateTime.Now}] Pipe disconnected, shutting down.\n");
                }
            }
            catch (Exception ex)
            {
                File.AppendAllText(logPath, $"[{DateTime.Now}] FATAL: {ex}\n");
            }
        }
    }
}
