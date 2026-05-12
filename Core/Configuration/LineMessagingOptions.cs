namespace SANJET.Core.Configuration
{
    public class LineMessagingOptions
    {
        public bool Enabled { get; set; }
        public string ChannelAccessToken { get; set; } = string.Empty;
        public string[] TargetIds { get; set; } = [];
        public int CooldownMinutes { get; set; } = 30;
        public bool NotifyRecovery { get; set; } = true;
    }
}
