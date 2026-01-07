using System.Text.Json;
using System.Text.Json.Serialization;
using XUnitAssured.Kafka.Steps;

namespace XUnitAssured.Tests.KafkaTests;

[Trait("Category", "Kafka")]
[Trait("Component", "JsonOptions")]
/// <summary>
/// Unit tests for Kafka JsonSerializerOptions configurations.
/// Tests naming policies, converters, ignore conditions, and serialization behaviors.
/// </summary>
public class JsonOptionsTests
{
	#region Naming Policy Tests

	[Fact(DisplayName = "JsonOptions with CamelCase naming policy should be applied")]
	public void JsonOptions_With_CamelCase_Naming()
	{
		// Arrange
		var options = new JsonSerializerOptions
		{
			PropertyNamingPolicy = JsonNamingPolicy.CamelCase
		};

		var order = new TestOrder { OrderId = "123", CustomerName = "John Doe" };

		// Act
		var scenario = Given()
			.Topic("camelcase-topic")
			.Produce(order)
			.WithJsonOptions(options);

		// Assert
		var step = (KafkaProduceStep)scenario.CurrentStep;
		step.JsonOptions.ShouldNotBeNull();
		step.JsonOptions.PropertyNamingPolicy.ShouldBe(JsonNamingPolicy.CamelCase);
		
		// Verify serialization format
		var json = JsonSerializer.Serialize(order, options);
		json.ShouldContain("\"orderId\"");
		json.ShouldContain("\"customerName\"");
	}

	[Fact(DisplayName = "JsonOptions with SnakeCaseLower naming policy should be applied")]
	public void JsonOptions_With_SnakeCaseLower_Naming()
	{
		// Arrange
		var options = new JsonSerializerOptions
		{
			PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
		};

		var order = new TestOrder { OrderId = "123", CustomerName = "John Doe" };

		// Act
		var scenario = Given()
			.Topic("snakecase-topic")
			.Produce(order)
			.WithJsonOptions(options);

		// Assert
		var step = (KafkaProduceStep)scenario.CurrentStep;
		step.JsonOptions.ShouldNotBeNull();
		step.JsonOptions.PropertyNamingPolicy.ShouldBe(JsonNamingPolicy.SnakeCaseLower);
		
		// Verify serialization format
		var json = JsonSerializer.Serialize(order, options);
		json.ShouldContain("\"order_id\"");
		json.ShouldContain("\"customer_name\"");
	}

	[Fact(DisplayName = "JsonOptions with SnakeCaseUpper naming policy should be applied")]
	public void JsonOptions_With_SnakeCaseUpper_Naming()
	{
		// Arrange
		var options = new JsonSerializerOptions
		{
			PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseUpper
		};

		var order = new TestOrder { OrderId = "123", CustomerName = "John Doe" };

		// Act
		var scenario = Given()
			.Topic("snakecase-upper-topic")
			.Produce(order)
			.WithJsonOptions(options);

		// Assert
		var step = (KafkaProduceStep)scenario.CurrentStep;
		step.JsonOptions.ShouldNotBeNull();
		step.JsonOptions.PropertyNamingPolicy.ShouldBe(JsonNamingPolicy.SnakeCaseUpper);
		
		// Verify serialization format
		var json = JsonSerializer.Serialize(order, options);
		json.ShouldContain("\"ORDER_ID\"");
		json.ShouldContain("\"CUSTOMER_NAME\"");
	}

	[Fact(DisplayName = "JsonOptions with KebabCaseLower naming policy should be applied")]
	public void JsonOptions_With_KebabCaseLower_Naming()
	{
		// Arrange
		var options = new JsonSerializerOptions
		{
			PropertyNamingPolicy = JsonNamingPolicy.KebabCaseLower
		};

		var order = new TestOrder { OrderId = "123", CustomerName = "John Doe" };

		// Act
		var scenario = Given()
			.Topic("kebabcase-topic")
			.Produce(order)
			.WithJsonOptions(options);

		// Assert
		var step = (KafkaProduceStep)scenario.CurrentStep;
		step.JsonOptions.ShouldNotBeNull();
		step.JsonOptions.PropertyNamingPolicy.ShouldBe(JsonNamingPolicy.KebabCaseLower);
		
		// Verify serialization format
		var json = JsonSerializer.Serialize(order, options);
		json.ShouldContain("\"order-id\"");
		json.ShouldContain("\"customer-name\"");
	}

	[Fact(DisplayName = "JsonOptions with KebabCaseUpper naming policy should be applied")]
	public void JsonOptions_With_KebabCaseUpper_Naming()
	{
		// Arrange
		var options = new JsonSerializerOptions
		{
			PropertyNamingPolicy = JsonNamingPolicy.KebabCaseUpper
		};

		var order = new TestOrder { OrderId = "123", CustomerName = "John Doe" };

		// Act
		var scenario = Given()
			.Topic("kebabcase-upper-topic")
			.Produce(order)
			.WithJsonOptions(options);

		// Assert
		var step = (KafkaProduceStep)scenario.CurrentStep;
		step.JsonOptions.ShouldNotBeNull();
		step.JsonOptions.PropertyNamingPolicy.ShouldBe(JsonNamingPolicy.KebabCaseUpper);
		
		// Verify serialization format
		var json = JsonSerializer.Serialize(order, options);
		json.ShouldContain("\"ORDER-ID\"");
		json.ShouldContain("\"CUSTOMER-NAME\"");
	}

	#endregion

	#region Ignore Conditions Tests

	[Fact(DisplayName = "JsonOptions with ignore null values should not serialize nulls")]
	public void JsonOptions_With_Ignore_Null_Values()
	{
		// Arrange
		var options = new JsonSerializerOptions
		{
			DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
			PropertyNamingPolicy = JsonNamingPolicy.CamelCase
		};

		var order = new TestOrder 
		{ 
			OrderId = "123", 
			CustomerName = null,  // Should be ignored
			OptionalField = null  // Should be ignored
		};

		// Act
		var scenario = Given()
			.Topic("ignore-null-topic")
			.Produce(order)
			.WithJsonOptions(options);

		// Assert
		var step = (KafkaProduceStep)scenario.CurrentStep;
		step.JsonOptions.ShouldNotBeNull();
		step.JsonOptions.DefaultIgnoreCondition.ShouldBe(JsonIgnoreCondition.WhenWritingNull);
		
		// Verify nulls are not serialized
		var json = JsonSerializer.Serialize(order, options);
		json.ShouldContain("\"orderId\"");
		json.ShouldNotContain("\"customerName\"");
		json.ShouldNotContain("\"optionalField\"");
	}

	[Fact(DisplayName = "JsonOptions with ignore default values should not serialize defaults")]
	public void JsonOptions_With_Ignore_Default_Values()
	{
		// Arrange
		var options = new JsonSerializerOptions
		{
			DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingDefault,
			PropertyNamingPolicy = JsonNamingPolicy.CamelCase
		};

		var order = new TestOrder 
		{ 
			OrderId = "123",
			Quantity = 0,         // Default int, should be ignored
			IsActive = false      // Default bool, should be ignored
		};

		// Act
		var scenario = Given()
			.Topic("ignore-default-topic")
			.Produce(order)
			.WithJsonOptions(options);

		// Assert
		var step = (KafkaProduceStep)scenario.CurrentStep;
		step.JsonOptions.ShouldNotBeNull();
		step.JsonOptions.DefaultIgnoreCondition.ShouldBe(JsonIgnoreCondition.WhenWritingDefault);
		
		// Verify defaults are not serialized
		var json = JsonSerializer.Serialize(order, options);
		json.ShouldContain("\"orderId\"");
		json.ShouldNotContain("\"quantity\"");
		json.ShouldNotContain("\"isActive\"");
	}

	[Fact(DisplayName = "JsonOptions with never ignore should serialize all values")]
	public void JsonOptions_With_Never_Ignore()
	{
		// Arrange
		var options = new JsonSerializerOptions
		{
			DefaultIgnoreCondition = JsonIgnoreCondition.Never,
			PropertyNamingPolicy = JsonNamingPolicy.CamelCase
		};

		var order = new TestOrder 
		{ 
			OrderId = "123",
			CustomerName = null,
			Quantity = 0,
			IsActive = false
		};

		// Act
		var scenario = Given()
			.Topic("never-ignore-topic")
			.Produce(order)
			.WithJsonOptions(options);

		// Assert
		var step = (KafkaProduceStep)scenario.CurrentStep;
		step.JsonOptions.ShouldNotBeNull();
		step.JsonOptions.DefaultIgnoreCondition.ShouldBe(JsonIgnoreCondition.Never);
		
		// Verify all values are serialized
		var json = JsonSerializer.Serialize(order, options);
		json.ShouldContain("\"orderId\"");
		json.ShouldContain("\"customerName\":null");
		json.ShouldContain("\"quantity\":0");
		json.ShouldContain("\"isActive\":false");
	}

	#endregion

	#region Custom Converter Tests

	[Fact(DisplayName = "JsonOptions with custom DateTime converter should be applied")]
	public void JsonOptions_With_Custom_DateTime_Converter()
	{
		// Arrange
		var options = new JsonSerializerOptions
		{
			PropertyNamingPolicy = JsonNamingPolicy.CamelCase
		};
		options.Converters.Add(new CustomDateTimeConverter());

		var order = new TestOrder 
		{ 
			OrderId = "123",
			CreatedAt = new DateTime(2024, 1, 7, 12, 30, 0, DateTimeKind.Utc)
		};

		// Act
		var scenario = Given()
			.Topic("custom-datetime-topic")
			.Produce(order)
			.WithJsonOptions(options);

		// Assert
		var step = (KafkaProduceStep)scenario.CurrentStep;
		step.JsonOptions.ShouldNotBeNull();
		step.JsonOptions.Converters.Count.ShouldBe(1);
		
		// Verify custom format
		var json = JsonSerializer.Serialize(order, options);
		json.ShouldContain("\"createdAt\":\"2024-01-07 12:30:00\"");
	}

	[Fact(DisplayName = "JsonOptions with custom decimal converter should be applied")]
	public void JsonOptions_With_Custom_Decimal_Converter()
	{
		// Arrange
		var options = new JsonSerializerOptions
		{
			PropertyNamingPolicy = JsonNamingPolicy.CamelCase
		};
		options.Converters.Add(new CustomDecimalConverter());

		var order = new TestOrder 
		{ 
			OrderId = "123",
			Amount = 99.999m
		};

		// Act
		var scenario = Given()
			.Topic("custom-decimal-topic")
			.Produce(order)
			.WithJsonOptions(options);

		// Assert
		var step = (KafkaProduceStep)scenario.CurrentStep;
		step.JsonOptions.ShouldNotBeNull();
		step.JsonOptions.Converters.Count.ShouldBe(1);
		
		// Verify custom format (2 decimal places)
		var json = JsonSerializer.Serialize(order, options);
		json.ShouldContain("\"amount\":\"100.00\"");
	}

	[Fact(DisplayName = "JsonOptions with multiple custom converters should all be applied")]
	public void JsonOptions_With_Multiple_Converters()
	{
		// Arrange
		var options = new JsonSerializerOptions
		{
			PropertyNamingPolicy = JsonNamingPolicy.CamelCase
		};
		options.Converters.Add(new CustomDateTimeConverter());
		options.Converters.Add(new CustomDecimalConverter());

		var order = new TestOrder 
		{ 
			OrderId = "123",
			Amount = 99.999m,
			CreatedAt = new DateTime(2024, 1, 7, 12, 30, 0, DateTimeKind.Utc)
		};

		// Act
		var scenario = Given()
			.Topic("multiple-converters-topic")
			.Produce(order)
			.WithJsonOptions(options);

		// Assert
		var step = (KafkaProduceStep)scenario.CurrentStep;
		step.JsonOptions.ShouldNotBeNull();
		step.JsonOptions.Converters.Count.ShouldBe(2);
		
		// Verify both converters work
		var json = JsonSerializer.Serialize(order, options);
		json.ShouldContain("\"amount\":\"100.00\"");
		json.ShouldContain("\"createdAt\":\"2024-01-07 12:30:00\"");
	}

	#endregion

	#region Write Indented and Other Options Tests

	[Fact(DisplayName = "JsonOptions with write indented should format JSON")]
	public void JsonOptions_With_Write_Indented()
	{
		// Arrange
		var options = new JsonSerializerOptions
		{
			WriteIndented = true,
			PropertyNamingPolicy = JsonNamingPolicy.CamelCase
		};

		var order = new TestOrder { OrderId = "123", CustomerName = "John" };

		// Act
		var scenario = Given()
			.Topic("indented-topic")
			.Produce(order)
			.WithJsonOptions(options);

		// Assert
		var step = (KafkaProduceStep)scenario.CurrentStep;
		step.JsonOptions.ShouldNotBeNull();
		step.JsonOptions.WriteIndented.ShouldBe(true);
		
		// Verify indented format
		var json = JsonSerializer.Serialize(order, options);
		json.ShouldContain("\n");  // Should have newlines
		json.ShouldContain("  ");  // Should have indentation
	}

	[Fact(DisplayName = "JsonOptions with encoder settings should be applied")]
	public void JsonOptions_With_Encoder_Settings()
	{
		// Arrange
		var options = new JsonSerializerOptions
		{
			PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
			Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
		};

		var order = new TestOrder 
		{ 
			OrderId = "123", 
			CustomerName = "John & Jane" 
		};

		// Act
		var scenario = Given()
			.Topic("encoder-topic")
			.Produce(order)
			.WithJsonOptions(options);

		// Assert
		var step = (KafkaProduceStep)scenario.CurrentStep;
		step.JsonOptions.ShouldNotBeNull();
		step.JsonOptions.Encoder.ShouldBe(System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping);
		
		// Verify encoding
		var json = JsonSerializer.Serialize(order, options);
		json.ShouldContain("John & Jane");  // & not escaped
	}

	[Fact(DisplayName = "JsonOptions with allow trailing commas should be applied")]
	public void JsonOptions_With_Allow_Trailing_Commas()
	{
		// Arrange
		var options = new JsonSerializerOptions
		{
			AllowTrailingCommas = true,
			PropertyNamingPolicy = JsonNamingPolicy.CamelCase
		};

		var order = new TestOrder { OrderId = "123" };

		// Act
		var scenario = Given()
			.Topic("trailing-commas-topic")
			.Produce(order)
			.WithJsonOptions(options);

		// Assert
		var step = (KafkaProduceStep)scenario.CurrentStep;
		step.JsonOptions.ShouldNotBeNull();
		step.JsonOptions.AllowTrailingCommas.ShouldBe(true);
	}

	#endregion

	#region Complex JsonOptions Scenarios

	[Fact(DisplayName = "JsonOptions with all features combined should work correctly")]
	public void JsonOptions_With_All_Features_Combined()
	{
		// Arrange
		var options = new JsonSerializerOptions
		{
			PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
			DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
			WriteIndented = false,
			AllowTrailingCommas = true,
			PropertyNameCaseInsensitive = true
		};
		options.Converters.Add(new CustomDateTimeConverter());
		options.Converters.Add(new CustomDecimalConverter());

		var order = new TestOrder 
		{ 
			OrderId = "123",
			CustomerName = null,  // Should be ignored
			Amount = 99.999m,      // Should be formatted as 100.00
			CreatedAt = new DateTime(2024, 1, 7, 12, 30, 0),
			Quantity = 5
		};

		// Act
		var scenario = Given()
			.Topic("complex-json-topic")
			.Produce(order)
			.WithJsonOptions(options);

		// Assert
		var step = (KafkaProduceStep)scenario.CurrentStep;
		step.JsonOptions.ShouldNotBeNull();
		step.JsonOptions.PropertyNamingPolicy.ShouldBe(JsonNamingPolicy.CamelCase);
		step.JsonOptions.DefaultIgnoreCondition.ShouldBe(JsonIgnoreCondition.WhenWritingNull);
		step.JsonOptions.WriteIndented.ShouldBe(false);
		step.JsonOptions.Converters.Count.ShouldBe(2);
		
		// Verify serialization
		var json = JsonSerializer.Serialize(order, options);
		json.ShouldContain("\"orderId\":\"123\"");
		json.ShouldNotContain("\"customerName\"");
		json.ShouldContain("\"amount\":\"100.00\"");
		json.ShouldContain("\"createdAt\":\"2024-01-07 12:30:00\"");
		json.ShouldContain("\"quantity\":5");
	}

	#endregion

	#region Test Models

	private class TestOrder
	{
		public string? OrderId { get; set; }
		public string? CustomerName { get; set; }
		public string? OptionalField { get; set; }
		public int Quantity { get; set; }
		public decimal Amount { get; set; }
		public bool IsActive { get; set; }
		public DateTime CreatedAt { get; set; }
	}

	#endregion

	#region Custom Converters

	private class CustomDateTimeConverter : JsonConverter<DateTime>
	{
		public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
		{
			return DateTime.Parse(reader.GetString()!);
		}

		public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options)
		{
			writer.WriteStringValue(value.ToString("yyyy-MM-dd HH:mm:ss"));
		}
	}

	private class CustomDecimalConverter : JsonConverter<decimal>
	{
		public override decimal Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
		{
			return decimal.Parse(reader.GetString()!, System.Globalization.CultureInfo.InvariantCulture);
		}

		public override void Write(Utf8JsonWriter writer, decimal value, JsonSerializerOptions options)
		{
			writer.WriteStringValue(Math.Round(value, 2).ToString("F2", System.Globalization.CultureInfo.InvariantCulture));
		}
	}

	#endregion
}
