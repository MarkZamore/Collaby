using Collaby.EventBus.Abstractions;
using Collaby.EventBus.Contracts;
using Collaby.EventBus.Internal;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Collaby.EventBus;

public static class CollabyEventBusServiceCollectionExtensions
{
    public static IServiceCollection AddCollabyEventBus(
        this IServiceCollection services,
        IConfiguration configuration,
        string serviceName)
    {
        services.AddOptions<CollabyKafkaOptions>()
            .Bind(configuration.GetSection(CollabyKafkaOptions.SectionName))
            .PostConfigure(options =>
            {
                if (string.IsNullOrWhiteSpace(options.BootstrapServers))
                {
                    options.BootstrapServers = "events:9092";
                }
            })
            .Validate(options => !string.IsNullOrWhiteSpace(options.BootstrapServers), "Kafka:BootstrapServers must be configured.")
            .ValidateOnStart();

        services.TryAddSingleton(new CollabyKafkaServiceIdentity(serviceName));
        services.TryAddSingleton<IKafkaTopicRegistry, EmbeddedKafkaTopicRegistry>();
        services.TryAddSingleton<KafkaSchemaValidator>();
        services.TryAddSingleton<KafkaProducerAccessor>();
        services.TryAddSingleton<IEventPublisher, KafkaEventPublisher>();

        return services;
    }
}
