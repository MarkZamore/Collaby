using Confluent.Kafka;
using Microsoft.Extensions.Options;

namespace Collaby.EventBus.Internal;

public sealed class KafkaProducerAccessor : IDisposable
{
    private readonly Lazy<IProducer<string, string>> _producer;

    public KafkaProducerAccessor(IOptions<CollabyKafkaOptions> options, CollabyKafkaServiceIdentity serviceIdentity)
    {
        _producer = new Lazy<IProducer<string, string>>(
            () => new ProducerBuilder<string, string>(new ProducerConfig
            {
                BootstrapServers = options.Value.BootstrapServers,
                ClientId = $"{serviceIdentity.ServiceName}-producer"
            }).Build(),
            LazyThreadSafetyMode.ExecutionAndPublication);
    }

    public IProducer<string, string> Producer => _producer.Value;

    public void Dispose()
    {
        if (!_producer.IsValueCreated)
        {
            return;
        }

        _producer.Value.Flush(TimeSpan.FromSeconds(5));
        _producer.Value.Dispose();
    }
}
