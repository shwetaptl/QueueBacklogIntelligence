namespace QueueBacklogIntelligence.Configuration
{
    public class CollectorSettings
    {
        public string ServiceBusConnectionString { get; set; } = string.Empty;
        public string StorageConnectionString { get; set; } = "UseDevelopmentStorage=true";
    }
}