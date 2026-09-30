using System;
using System.Collections.Generic;

using Shouldly;
using Xunit;

using XUnitAssured.Core.Results;

namespace XUnitAssured.Tests.CoreTests;

[Trait("Category", "Core")]
[Trait("Component", "TestStepResult")]
/// <summary>
/// Tests for the value coercion performed by <see cref="TestStepResult.GetProperty{T}"/>
/// and <see cref="TestStepResult.GetData{T}"/>.
/// Step properties arrive loosely typed, so a type the coercion does not understand
/// used to come back as a silent <c>default</c> — producing assertion failures whose
/// message pointed nowhere near the real cause.
/// </summary>
public class TestStepResultConversionTests
{
	private static TestStepResult WithProperty(string key, object? value) =>
		new()
		{
			Success = true,
			Properties = new Dictionary<string, object?> { [key] = value }
		};

	private enum Severity
	{
		Low = 0,
		High = 2
	}

	[Fact(DisplayName = "GetProperty should return a value already of the requested type")]
	public void GetProperty_Should_Return_Exact_Type()
	{
		WithProperty("StatusCode", 201).GetProperty<int>("StatusCode").ShouldBe(201);
	}

	[Fact(DisplayName = "GetProperty should convert a numeric string to a number")]
	public void GetProperty_Should_Convert_Numeric_String()
	{
		WithProperty("Count", "42").GetProperty<int>("Count").ShouldBe(42);
	}

	[Fact(DisplayName = "GetProperty should convert to a nullable value type")]
	public void GetProperty_Should_Convert_To_Nullable()
	{
		WithProperty("Count", "42").GetProperty<int?>("Count").ShouldBe(42);
	}

	[Fact(DisplayName = "GetProperty should parse a Guid from its string form")]
	public void GetProperty_Should_Parse_Guid()
	{
		var id = Guid.NewGuid();

		WithProperty("CorrelationId", id.ToString()).GetProperty<Guid>("CorrelationId").ShouldBe(id);
	}

	[Fact(DisplayName = "GetProperty should parse a DateTimeOffset from its string form")]
	public void GetProperty_Should_Parse_DateTimeOffset()
	{
		WithProperty("ObservedAt", "2026-09-30T12:34:56+00:00")
			.GetProperty<DateTimeOffset>("ObservedAt")
			.ShouldBe(new DateTimeOffset(2026, 9, 30, 12, 34, 56, TimeSpan.Zero));
	}

	[Fact(DisplayName = "GetProperty should parse a TimeSpan from its string form")]
	public void GetProperty_Should_Parse_TimeSpan()
	{
		WithProperty("Elapsed", "00:01:30").GetProperty<TimeSpan>("Elapsed").ShouldBe(TimeSpan.FromSeconds(90));
	}

	[Fact(DisplayName = "GetProperty should parse an enum from its name")]
	public void GetProperty_Should_Parse_Enum_By_Name()
	{
		WithProperty("Severity", "High").GetProperty<Severity>("Severity").ShouldBe(Severity.High);
	}

	[Fact(DisplayName = "GetProperty should convert an enum from its numeric value")]
	public void GetProperty_Should_Parse_Enum_By_Value()
	{
		WithProperty("Severity", 2).GetProperty<Severity>("Severity").ShouldBe(Severity.High);
	}

	[Fact(DisplayName = "GetProperty should return default for a value that is not convertible")]
	public void GetProperty_Should_Return_Default_For_Unconvertible_Value()
	{
		WithProperty("Count", "not-a-number").GetProperty<int>("Count").ShouldBe(0);
	}

	[Fact(DisplayName = "GetProperty should return default for a missing key")]
	public void GetProperty_Should_Return_Default_For_Missing_Key()
	{
		WithProperty("Other", 1).GetProperty<int>("Absent").ShouldBe(0);
	}

	[Fact(DisplayName = "GetProperty should return default for a null value")]
	public void GetProperty_Should_Return_Default_For_Null_Value()
	{
		WithProperty("Nothing", null).GetProperty<string>("Nothing").ShouldBeNull();
	}

	[Fact(DisplayName = "GetProperty should return default for a null or whitespace key")]
	public void GetProperty_Should_Return_Default_For_Blank_Key()
	{
		var result = WithProperty("Count", 1);

		result.GetProperty<int>("   ").ShouldBe(0);
		result.GetProperty<int>(null!).ShouldBe(0);
	}

	[Fact(DisplayName = "GetProperty should propagate an unexpected exception from a custom converter")]
	public void GetProperty_Should_Propagate_Unexpected_Exception()
	{
		// A broken IConvertible is a defect in the caller's own type, not a value that
		// merely fails to represent a T. Swallowing it would hide the bug entirely.
		var result = WithProperty("Hostile", new HostileConvertible());

		Should.Throw<NotSupportedException>(() => result.GetProperty<int>("Hostile"));
	}

	[Fact(DisplayName = "GetData should convert the payload to the requested type")]
	public void GetData_Should_Convert_Payload()
	{
		var result = new TestStepResult { Success = true, Data = "7", DataType = typeof(string) };

		result.GetData<int>().ShouldBe(7);
		result.GetData<string>().ShouldBe("7");
	}

	[Fact(DisplayName = "GetData should return default when there is no payload")]
	public void GetData_Should_Return_Default_Without_Payload()
	{
		new TestStepResult { Success = true }.GetData<int>().ShouldBe(0);
	}

	/// <summary>
	/// An <see cref="IConvertible"/> whose conversion throws something other than a
	/// conversion failure, standing in for a caller-supplied type with a bug in it.
	/// </summary>
	private sealed class HostileConvertible : IConvertible
	{
		public TypeCode GetTypeCode() => TypeCode.Object;

		public int ToInt32(IFormatProvider? provider) => throw new NotSupportedException("boom");

		public bool ToBoolean(IFormatProvider? provider) => throw new NotSupportedException("boom");
		public byte ToByte(IFormatProvider? provider) => throw new NotSupportedException("boom");
		public char ToChar(IFormatProvider? provider) => throw new NotSupportedException("boom");
		public DateTime ToDateTime(IFormatProvider? provider) => throw new NotSupportedException("boom");
		public decimal ToDecimal(IFormatProvider? provider) => throw new NotSupportedException("boom");
		public double ToDouble(IFormatProvider? provider) => throw new NotSupportedException("boom");
		public short ToInt16(IFormatProvider? provider) => throw new NotSupportedException("boom");
		public long ToInt64(IFormatProvider? provider) => throw new NotSupportedException("boom");
		public sbyte ToSByte(IFormatProvider? provider) => throw new NotSupportedException("boom");
		public float ToSingle(IFormatProvider? provider) => throw new NotSupportedException("boom");
		public string ToString(IFormatProvider? provider) => throw new NotSupportedException("boom");
		public object ToType(Type conversionType, IFormatProvider? provider) => throw new NotSupportedException("boom");
		public ushort ToUInt16(IFormatProvider? provider) => throw new NotSupportedException("boom");
		public uint ToUInt32(IFormatProvider? provider) => throw new NotSupportedException("boom");
		public ulong ToUInt64(IFormatProvider? provider) => throw new NotSupportedException("boom");
	}
}
