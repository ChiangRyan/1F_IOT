namespace SANJET.Core.Configuration
{
    public class LineAutoHotkeyOptions
    {
        public bool Enabled { get; set; }
        public string AutoHotkeyExecutablePath { get; set; } = @"C:\Program Files\AutoHotkey\v2\AutoHotkey64.exe";
        public string AutoHotkeyVersion { get; set; } = "v2";
        public string LineExecutablePath { get; set; } = string.Empty;
        public string LineProcessName { get; set; } = "LINE";
        public string[] TargetChatNames { get; set; } = [];
        public int OperationTimeoutSeconds { get; set; } = 15;
        public int SendDelayMilliseconds { get; set; } = 300;
        public bool RestoreClipboard { get; set; } = true;
    }
}
