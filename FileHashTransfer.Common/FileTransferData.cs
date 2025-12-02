using System.Text.Json.Serialization;

namespace FileHashTransfer.Common
{
    public class FileTransferData
    {
        public string FileName { get; set; } = "";
        public long FileSize { get; set; }
        public string Hash { get; set; } = "";
        public byte[] Salt { get; set; } = Array.Empty<byte>();
        public byte[] FileData { get; set; } = Array.Empty<byte>();

        [JsonIgnore]
        public bool UseSalt { get; set; } = true;
    }
}