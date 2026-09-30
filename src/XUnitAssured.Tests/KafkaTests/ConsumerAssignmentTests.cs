using System;
using System.Collections.Generic;
using System.Linq;

using Confluent.Kafka;

using XUnitAssured.Kafka.Helpers;

namespace XUnitAssured.Tests.KafkaTests;

[Trait("Category", "Kafka")]
[Trait("Component", "ConsumeStep")]
/// <summary>
/// Tests the rule that decides where a manually assigned consumer starts reading.
/// Consume steps assign partitions directly instead of subscribing, to skip the
/// consumer-group join; the starting offsets must therefore reproduce what a
/// subscription would have chosen, or the change would alter which messages a
/// test sees. The rule is a pure function, so it is asserted without a broker.
/// </summary>
public class ConsumerAssignmentTests
{
	private const string Topic = "orders";

	private static TopicPartition Partition(int id) => new(Topic, new Partition(id));

	private static IReadOnlyList<TopicPartition> Partitions(params int[] ids) =>
		ids.Select(Partition).ToList();

	private static TopicPartitionOffset Committed(int partition, long offset) =>
		new(Partition(partition), new Offset(offset));

	[Fact(DisplayName = "A committed offset should take precedence over the reset policy")]
	public void Committed_Offset_Should_Win_Over_Reset_Policy()
	{
		var assignment = ConsumerAssignment.ResolveStartOffsets(
			Partitions(0),
			new[] { Committed(0, 42) },
			AutoOffsetReset.Earliest);

		assignment.ShouldHaveSingleItem();
		assignment[0].Offset.Value.ShouldBe(42);
	}

	[Fact(DisplayName = "Without a committed offset, Earliest should start from the beginning")]
	public void Earliest_Should_Start_From_Beginning_When_Nothing_Is_Committed()
	{
		var assignment = ConsumerAssignment.ResolveStartOffsets(
			Partitions(0),
			Array.Empty<TopicPartitionOffset>(),
			AutoOffsetReset.Earliest);

		assignment[0].Offset.ShouldBe(Offset.Beginning);
	}

	[Fact(DisplayName = "Without a committed offset, Latest should start from the end")]
	public void Latest_Should_Start_From_End_When_Nothing_Is_Committed()
	{
		var assignment = ConsumerAssignment.ResolveStartOffsets(
			Partitions(0),
			Array.Empty<TopicPartitionOffset>(),
			AutoOffsetReset.Latest);

		assignment[0].Offset.ShouldBe(Offset.End);
	}

	[Fact(DisplayName = "An unset reset policy should behave like librdkafka's default, Latest")]
	public void Unset_Policy_Should_Behave_Like_Latest()
	{
		var assignment = ConsumerAssignment.ResolveStartOffsets(
			Partitions(0),
			Array.Empty<TopicPartitionOffset>(),
			autoOffsetReset: null);

		assignment[0].Offset.ShouldBe(Offset.End);
	}

	[Fact(DisplayName = "Each partition should be resolved independently")]
	public void Partitions_Should_Be_Resolved_Independently()
	{
		// Partition 1 has a commit, partitions 0 and 2 do not.
		var assignment = ConsumerAssignment.ResolveStartOffsets(
			Partitions(0, 1, 2),
			new[] { Committed(1, 7) },
			AutoOffsetReset.Earliest);

		assignment.Count.ShouldBe(3);
		assignment.Single(a => a.Partition.Value == 0).Offset.ShouldBe(Offset.Beginning);
		assignment.Single(a => a.Partition.Value == 1).Offset.Value.ShouldBe(7);
		assignment.Single(a => a.Partition.Value == 2).Offset.ShouldBe(Offset.Beginning);
	}

	[Fact(DisplayName = "A committed entry with no real offset should count as absent")]
	public void Unset_Committed_Entry_Should_Count_As_Absent()
	{
		// The broker answers a committed-offset query with Offset.Unset for a
		// partition the group never committed. That is "nothing committed", not
		// an instruction to start at an unset position.
		var assignment = ConsumerAssignment.ResolveStartOffsets(
			Partitions(0),
			new[] { new TopicPartitionOffset(Partition(0), Offset.Unset) },
			AutoOffsetReset.Earliest);

		assignment[0].Offset.ShouldBe(Offset.Beginning);
	}

	[Fact(DisplayName = "The assignment should cover every partition exactly once")]
	public void Assignment_Should_Cover_Every_Partition_Once()
	{
		var partitions = Partitions(3, 0, 5);

		var assignment = ConsumerAssignment.ResolveStartOffsets(
			partitions,
			Array.Empty<TopicPartitionOffset>(),
			AutoOffsetReset.Earliest);

		assignment.Select(a => a.TopicPartition).ShouldBe(partitions, ignoreOrder: true);
	}

	[Fact(DisplayName = "Null inputs should be rejected")]
	public void Null_Inputs_Should_Be_Rejected()
	{
		Should.Throw<ArgumentNullException>(() =>
			ConsumerAssignment.ResolveStartOffsets(null!, Array.Empty<TopicPartitionOffset>(), AutoOffsetReset.Earliest));

		Should.Throw<ArgumentNullException>(() =>
			ConsumerAssignment.ResolveStartOffsets(Partitions(0), null!, AutoOffsetReset.Earliest));
	}
}
