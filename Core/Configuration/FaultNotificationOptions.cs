namespace SANJET.Core.Configuration
{
    public class FaultNotificationOptions
    {
        public bool Enabled { get; set; } = true;
        public int CooldownMinutes { get; set; } = 30;
        public bool NotifyRecovery { get; set; } = true;
        public int BatchWindowSeconds { get; set; } = 5;
        public int MaxBatchSize { get; set; } = 50;
    }
}
