using XUnitAssured.Core.Abstractions;
using XUnitAssured.Kafka.Abstractions;

namespace XUnitAssured.Kafka.Extensions;

/// <summary>
/// Where <c>Execute()</c> used to live.
///
/// <para>
/// It was an extension on <see cref="ITestScenario"/>, and so were the ones in the other
/// packages. Referencing two of them made the call ambiguous — and referencing two of them is
/// the point of this framework. A scenario crossing an API, a topic and a screen had to end
/// with this class spelled out.
/// </para>
///
/// <para>
/// It is now an instance method on <see cref="IKafkaScenario"/>, which the chain methods return.
/// The compiler picks the builder from the receiver, and the chain ends as it reads.
/// </para>
/// </summary>
public static class KafkaBddExtensions
{
}
