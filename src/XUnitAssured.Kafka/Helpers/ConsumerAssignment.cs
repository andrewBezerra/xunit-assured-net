using System;
using System.Collections.Generic;
using System.Linq;

using Confluent.Kafka;

namespace XUnitAssured.Kafka.Helpers;

/// <summary>
/// Assigns a consumer directly to every partition of a topic, bypassing the
/// consumer-group join that <c>Subscribe</c> performs.
/// </summary>
/// <remarks>
/// <para>
/// Joining a consumer group costs a coordinator lookup, a JoinGroup/SyncGroup
/// exchange and — on a default broker — a <c>group.initial.rebalance.delay.ms</c>
/// wait of three seconds, all before the first message can be read. In a test
/// each consume step is a fresh consumer, so that price is paid on every step.
/// </para>
/// <para>
/// Manual assignment removes the join while keeping the observable behaviour of
/// <c>Subscribe</c>: a partition with an offset committed under the group resumes
/// from it, and one without starts where <c>AutoOffsetReset</c> says. Committed
/// offsets are looked up without joining, so no rebalance is triggered.
/// </para>
/// </remarks>
internal static class ConsumerAssignment
{
	/// <summary>
	/// Configuration keys that only make sense on a consumer. They are stripped when
	/// the consumer configuration is reused to build the metadata client, so
	/// librdkafka does not log a configuration warning for each of them.
	/// </summary>
	private static readonly HashSet<string> ConsumerOnlyKeys = new(StringComparer.OrdinalIgnoreCase)
	{
		"group.id",
		"group.instance.id",
		"auto.offset.reset",
		"enable.auto.commit",
		"enable.auto.offset.store",
		"auto.commit.interval.ms",
		"session.timeout.ms",
		"heartbeat.interval.ms",
		"max.poll.interval.ms",
		"partition.assignment.strategy",
		"fetch.wait.max.ms",
		"fetch.min.bytes",
		"fetch.max.bytes",
		"fetch.message.max.bytes",
		"max.partition.fetch.bytes",
		"queued.min.messages",
		"queued.max.messages.kbytes",
		"enable.partition.eof",
		"check.crcs",
		"isolation.level",
		"allow.auto.create.topics",
		"coordinator.query.interval.ms",
		"fetch.error.backoff.ms",
		"fetch.queue.backoff.ms",
	};

	/// <summary>
	/// Assigns <paramref name="consumer"/> to all partitions of <paramref name="topic"/>
	/// at the offsets <c>Subscribe</c> would have started from.
	/// </summary>
	/// <param name="consumer">The consumer to assign. Must not be subscribed.</param>
	/// <param name="config">The configuration the consumer was built from.</param>
	/// <param name="topic">Topic whose partitions are assigned.</param>
	/// <param name="metadataTimeout">How long to wait for partition metadata.</param>
	/// <returns>The assignment that was applied, one entry per partition.</returns>
	/// <exception cref="InvalidOperationException">Thrown when the topic has no partitions, typically because it does not exist.</exception>
	public static IReadOnlyList<TopicPartitionOffset> AssignAllPartitions(
		IConsumer<string, string> consumer,
		ConsumerConfig config,
		string topic,
		TimeSpan metadataTimeout)
	{
		if (consumer == null)
			throw new ArgumentNullException(nameof(consumer));
		if (config == null)
			throw new ArgumentNullException(nameof(config));
		if (string.IsNullOrWhiteSpace(topic))
			throw new ArgumentException("Topic must be provided.", nameof(topic));

		var partitions = DiscoverPartitions(config, topic, metadataTimeout);
		if (partitions.Count == 0)
			throw new InvalidOperationException(
				$"Topic '{topic}' has no partitions. It probably does not exist on the broker.");

		var committed = LookUpCommittedOffsets(consumer, partitions, metadataTimeout);
		var assignment = ResolveStartOffsets(partitions, committed, config.AutoOffsetReset);

		WarmLeaderMetadata(consumer, partitions[0], metadataTimeout);

		consumer.Assign(assignment);
		return assignment;
	}

	/// <summary>
	/// Makes the consumer learn the topic's partition leaders before it is assigned.
	/// </summary>
	/// <remarks>
	/// <para>
	/// After a manual <c>Assign</c>, librdkafka only discovers where to fetch from on
	/// its periodic metadata refresh, so the first message consistently arrives about
	/// a second late (measured: 1 022 ms ± 6 against a local broker). A watermark
	/// query performs that lookup synchronously, and one query is enough: the
	/// metadata response carries every partition of the topic.
	/// </para>
	/// <para>
	/// The query's result is irrelevant; only the refresh it triggers matters. It can
	/// legitimately fail on a topic whose leader election has not settled yet
	/// (<c>Not leader for partition</c>), and even that error response refreshes the
	/// metadata, so failures are ignored rather than allowed to fail the consume.
	/// </para>
	/// </remarks>
	private static void WarmLeaderMetadata(IConsumer<string, string> consumer, TopicPartition partition, TimeSpan timeout)
	{
		try
		{
			consumer.QueryWatermarkOffsets(partition, timeout);
		}
		catch (KafkaException)
		{
			// Best effort by design; see remarks.
		}
	}

	/// <summary>
	/// Decides where each partition starts, mirroring what a subscription would do:
	/// a committed offset wins; otherwise <paramref name="autoOffsetReset"/> applies.
	/// Pure, so the rule can be tested without a broker.
	/// </summary>
	/// <param name="partitions">Partitions to resolve.</param>
	/// <param name="committed">Committed offsets, as returned by the broker. Entries whose offset is unset are treated as absent.</param>
	/// <param name="autoOffsetReset">The consumer's <c>auto.offset.reset</c>. <c>null</c> behaves as librdkafka's default, <c>Latest</c>.</param>
	public static IReadOnlyList<TopicPartitionOffset> ResolveStartOffsets(
		IReadOnlyList<TopicPartition> partitions,
		IReadOnlyList<TopicPartitionOffset> committed,
		AutoOffsetReset? autoOffsetReset)
	{
		if (partitions == null)
			throw new ArgumentNullException(nameof(partitions));
		if (committed == null)
			throw new ArgumentNullException(nameof(committed));

		// librdkafka's own default for auto.offset.reset is "largest".
		var fallback = autoOffsetReset == AutoOffsetReset.Earliest ? Offset.Beginning : Offset.End;

		var committedByPartition = committed
			.Where(c => c.Offset.IsSpecial == false && c.Offset.Value >= 0)
			.ToDictionary(c => c.TopicPartition, c => c.Offset);

		return partitions
			.Select(p => new TopicPartitionOffset(
				p,
				committedByPartition.TryGetValue(p, out var offset) ? offset : fallback))
			.ToList();
	}

	private static List<TopicPartition> DiscoverPartitions(ConsumerConfig config, string topic, TimeSpan timeout)
	{
		var adminConfig = new AdminClientConfig();
		foreach (var entry in config)
		{
			if (!ConsumerOnlyKeys.Contains(entry.Key))
				adminConfig.Set(entry.Key, entry.Value);
		}

		using var admin = new AdminClientBuilder(adminConfig).Build();
		var metadata = admin.GetMetadata(topic, timeout);
		var topicMetadata = metadata.Topics.Find(t => string.Equals(t.Topic, topic, StringComparison.Ordinal));

		if (topicMetadata == null || topicMetadata.Error.IsError)
			return new List<TopicPartition>();

		return topicMetadata.Partitions
			.Select(p => new TopicPartition(topic, new Partition(p.PartitionId)))
			.ToList();
	}

	/// <summary>
	/// Fetches committed offsets for the consumer's group. Talks to the group
	/// coordinator but does not join the group, so no rebalance happens.
	/// A failure here (coordinator unavailable, no group yet) is not fatal: it
	/// simply means nothing is committed, which is the common case in a test.
	/// </summary>
	private static IReadOnlyList<TopicPartitionOffset> LookUpCommittedOffsets(
		IConsumer<string, string> consumer,
		List<TopicPartition> partitions,
		TimeSpan timeout)
	{
		try
		{
			return consumer.Committed(partitions, timeout);
		}
		catch (KafkaException)
		{
			return Array.Empty<TopicPartitionOffset>();
		}
	}
}
