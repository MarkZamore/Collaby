using System.Text.Json;

namespace Collaby.EventBus.Contracts;

public sealed record KafkaTopicContract(
    string Name,
    string SchemaFile,
    string DlqTopic,
    JsonElement SchemaDocument,
    IReadOnlyList<string> RequiredHeaders)
{
    public string SchemaVersion =>
        Name.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).LastOrDefault()
        ?? "v1";
}
