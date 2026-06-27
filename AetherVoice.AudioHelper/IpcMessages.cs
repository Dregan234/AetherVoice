using System.Text.Json.Serialization;

namespace AetherVoice.AudioHelper
{
    public class CommandMessage
    {
        [JsonPropertyName("Cmd")]
        public string Cmd { get; set; }

        [JsonPropertyName("Voice")]
        public string Voice { get; set; }

        [JsonPropertyName("Rate")]
        public int Rate { get; set; }

        [JsonPropertyName("TtsVolume")]
        public int TtsVolume { get; set; }

        [JsonPropertyName("SoundVolume")]
        public int SoundVolume { get; set; }

        [JsonPropertyName("DeviceId")]
        public string DeviceId { get; set; }

        [JsonPropertyName("Text")]
        public string Text { get; set; }

        [JsonPropertyName("Path")]
        public string Path { get; set; }
    }

    public class ResponseMessage
    {
        [JsonPropertyName("Status")]
        public string Status { get; set; }

        [JsonPropertyName("Message")]
        public string Message { get; set; }
    }
}
