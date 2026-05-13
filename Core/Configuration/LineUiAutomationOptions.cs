namespace SANJET.Core.Configuration
{
    public class LineUiAutomationOptions
    {
        public bool Enabled { get; set; }
        public string LineExecutablePath { get; set; } = string.Empty;
        public string LineProcessName { get; set; } = "LINE";
        public string[] TargetChatNames { get; set; } = [];
        public int OperationTimeoutSeconds { get; set; } = 15;
        public int SendDelayMilliseconds { get; set; } = 300;
        public bool RestoreClipboard { get; set; } = true;
    }
}
