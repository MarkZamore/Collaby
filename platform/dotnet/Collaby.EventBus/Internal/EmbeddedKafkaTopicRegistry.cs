using System.Reflection;
using System.Text.Json;
using Collaby.EventBus.Contracts;

namespace Collaby.EventBus.Internal;

internal sealed class EmbeddedKafkaTopicRegistry : IKafkaTopicRegistry
{
    private readonly Lazy<IReadOnlyDictionary<string, KafkaTopicContract>> _contracts;

    public EmbeddedKafkaTopicRegistry()
    {
        _contracts = new Lazy<IReadOnlyDictionary<string, KafkaTopicContract>>(LoadContracts, LazyThreadSafetyMode.ExecutionAndPublication);
    }

    public KafkaTopicContract GetRequiredTopic(string topicName)
    {
        if (_contracts.Value.TryGetValue(topicName, out var contract))
        {
            return contract;
        }

        throw new InvalidOperationException($"Kafka topic '{topicName}' is not declared in embedded topic-registry.json.");
    }

    private static IReadOnlyDictionary<string, KafkaTopicContract> LoadContracts()
    {
        var assembly = typeof(EmbeddedKafkaTopicRegistry).Assembly;
        using var registryStream = assembly.GetManifestResourceStream("Collaby.EventBus.EventContracts.topic-registry.json")
            ?? throw new InvalidOperationException("Embedded Kafka topic registry is missing.");
        using var registryDocument = JsonDocument.Parse(registryStream);

        var requiredHeaders = registryDocument.RootElement
            .GetProperty("requiredHeaders")
            .EnumerateArray()
            .Select(entry => entry.GetString())
            .Where(static value => !string.IsNullOrWhiteSpace(value))
            .Select(static value => value!)
            .ToArray();

        var result = new Dictionary<string, KafkaTopicContract>(StringComparer.Ordinal);
        foreach (var topic in registryDocument.RootElement.GetProperty("topics").EnumerateArray())
        {
            var topicName = topic.GetProperty("name").GetString()
                ?? throw new InvalidOperationException("Kafka topic name must not be null.");
            var schemaFile = topic.GetProperty("schema").GetString()
                ?? throw new InvalidOperationException($"Schema file must not be null for topic {topicName}.");
            var dlqTopic = topic.GetProperty("dlq").GetString()
                ?? throw new InvalidOperationException($"DLQ topic must not be null for topic {topicName}.");

            using var schemaStream = assembly.GetManifestResourceStream($"Collaby.EventBus.EventContracts.{schemaFile}")
                ?? throw new InvalidOperationException($"Embedded schema resource '{schemaFile}' is missing.");
            using var schemaDocument = JsonDocument.Parse(schemaStream);

            result[topicName] = new KafkaTopicContract(
                topicName,
                schemaFile,
                dlqTopic,
                schemaDocument.RootElement.Clone(),
                requiredHeaders);
        }

        return result;
    }
}
