using System;
using System.IO;
using System.Text.Json;

namespace SANJET.Core.Services
{
    public sealed class RtspStreamSettings
    {
        private static readonly string ConfigDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "SANJET");

        private static readonly string ConfigFilePath = Path.Combine(ConfigDirectory, "rtsp_settings.json");

        public string IpAddress { get; set; } = "192.168.98.190";
        public string Username { get; set; } = "SANJET";
        public string Password { get; set; } = "Sanjet25653819";
        public int Port { get; set; } = 554;
        public string StreamPath { get; set; } = "stream1";
        public string FullRtspUrl { get; set; } = string.Empty;

        public static RtspStreamSettings Load()
        {
            try
            {
                if (!File.Exists(ConfigFilePath))
                {
                    return new RtspStreamSettings();
                }

                string json = File.ReadAllText(ConfigFilePath);
                var settings = JsonSerializer.Deserialize<RtspStreamSettings>(json);
                return settings ?? new RtspStreamSettings();
            }
            catch
            {
                return new RtspStreamSettings();
            }
        }

        public void Save()
        {
            Directory.CreateDirectory(ConfigDirectory);
            string json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(ConfigFilePath, json);
        }

        public string BuildRtspUrl()
        {
            string directUrl = (FullRtspUrl ?? string.Empty).Trim();
            if (directUrl.StartsWith("rtsp://", StringComparison.OrdinalIgnoreCase))
            {
                return directUrl;
            }

            string user = Uri.EscapeDataString((Username ?? string.Empty).Trim());
            string pass = Uri.EscapeDataString((Password ?? string.Empty).Trim());
            string host = (IpAddress ?? string.Empty).Trim();
            string streamPath = (StreamPath ?? string.Empty).Trim();
            int port = Port <= 0 ? 554 : Port;

            if (string.IsNullOrWhiteSpace(streamPath))
            {
                streamPath = "stream1";
            }

            if (!streamPath.StartsWith("/"))
            {
                streamPath = "/" + streamPath;
            }

            if (streamPath == "/")
            {
                streamPath = string.Empty;
            }

            string authPart = string.IsNullOrWhiteSpace(user)
                ? string.Empty
                : $"{user}:{pass}@";

            return $"rtsp://{authPart}{host}:{port}{streamPath}";
        }
    }
}
