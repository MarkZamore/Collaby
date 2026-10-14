namespace Collaby.EventBus.Contracts;

public interface IKafkaTopicRegistry
{
    KafkaTopicContract GetRequiredTopic(string topicName);
}
