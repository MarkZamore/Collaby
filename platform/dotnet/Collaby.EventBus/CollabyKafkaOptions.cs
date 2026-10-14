namespace Collaby.EventBus;

public sealed class CollabyKafkaOptions
{
    public const string SectionName = "Kafka";

    public string BootstrapServers { get; set; } = "events:9092";
}
