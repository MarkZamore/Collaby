#!/bin/sh
set -eu

registry_path="${1:-/workspace/platform/contracts/events/topic-registry.json}"
bootstrap_servers="${KAFKA_BOOTSTRAP_SERVERS:-events:9092}"
partitions="${KAFKA_TOPIC_PARTITIONS:-3}"
replication_factor="${KAFKA_TOPIC_REPLICATION_FACTOR:-1}"

if [ ! -f "$registry_path" ]; then
  echo "Kafka topic registry not found: $registry_path" >&2
  exit 1
fi

topic_list="$(perl -MJSON::PP -e '
use strict;
use warnings;

my $path = shift @ARGV;
open my $fh, q{<:raw}, $path or die "Cannot open $path: $!";
local $/;
my $json = JSON::PP::decode_json(<$fh>);
for my $topic (@{ $json->{topics} // [] }) {
    my $name = $topic->{name};
    my $dlq = $topic->{dlq};
    next unless defined $name && length $name;
    print "$name\n";
    print "$dlq\n" if defined $dlq && length $dlq;
}
' "$registry_path")"

if [ -z "$topic_list" ]; then
  echo "Kafka topic registry is empty: $registry_path" >&2
  exit 1
fi

echo "$topic_list" | while IFS= read -r topic_name; do
  [ -n "$topic_name" ] || continue
  echo "Ensuring Kafka topic '$topic_name' exists on $bootstrap_servers"
  /opt/bitnami/kafka/bin/kafka-topics.sh \
    --bootstrap-server "$bootstrap_servers" \
    --create \
    --if-not-exists \
    --topic "$topic_name" \
    --partitions "$partitions" \
    --replication-factor "$replication_factor"
done

echo "Kafka topic bootstrap completed from $registry_path"
