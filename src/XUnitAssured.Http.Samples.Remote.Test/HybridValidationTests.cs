using Shouldly;
using XUnitAssured.Core.DSL;
using XUnitAssured.Extensions.Http;
using XUnitAssured.Http.Extensions;

namespace XUnitAssured.Http.Samples.Remote.Test;

[Trait("Category", "Validation")]
[Trait("Environment", "Remote")]
/// <summary>
/// Remote tests demonstrating the HYBRID VALIDATION APPROACH against a deployed API:
/// 1. Field validation for structure checking (detects breaking changes)
/// 2. AssertJsonPath with Shouldly (field-specific assertions)
/// </summary>
/// <remarks>
/// Tests validation strategies against a remote server configured in testsettings.json.
/// Demonstrates best practices for contract validation in remote API testing.
/// </remarks>
public class HybridValidationTests : HttpSamplesRemoteTestBase, IClassFixture<HttpSamplesRemoteFixture>
{
	public HybridValidationTests(HttpSamplesRemoteFixture fixture) : base(fixture)
	{
	}

	[Fact(Skip = "Remote test - requires deployed API environment", DisplayName = "Validate contract should detect JSON structure automatically")]
	public void Example01_ValidateContract_DetectsStructureAutomatically()
	{
		// This test validates key fields in the JSON structure
		// Note: Full contract validation with NJsonSchema requires exact casing match

		Given().ApiResource($"/api/products/1")
			.Get()
		.When()
			.Execute()
		.Then()
			// Validate key fields exist and have correct types
			.AssertJsonPath<int>("$.id", id => id.ShouldBeGreaterThan(0))
			.AssertJsonPath<string>("$.name", name => name.ShouldNotBeNullOrEmpty())
			.AssertJsonPath<decimal>("$.price", price => price.ShouldBeGreaterThan(0))
			.AssertJsonPath<string>("$.createdAt", date => date.ShouldNotBeNullOrEmpty())
			.AssertStatusCode(200);
	}

	[Fact(Skip = "Remote test - requires deployed API environment", DisplayName = "Corrected AssertJsonPath with Shouldly should provide clean syntax")]
	public void Example02_CorrectedAssertJsonPath_WithShouldly()
	{
		// This test demonstrates the CORRECTED AssertJsonPath
		// No more ugly type conversions! Clean Shouldly syntax!

		Given().ApiResource($"/api/products/1")
			.Get()
		.When()
			.Execute()
		.Then()
			// ✅ CORRECTED: Uses T properly, no more (int)value casts
			.AssertJsonPath<int>("$.id", id => id.ShouldBe(1))

			// ✅ Clean string assertions
			.AssertJsonPath<string>("$.name", name =>
			{
				name.ShouldNotBeNullOrEmpty();
				name.ShouldContain("Laptop");
			})

			// ✅ Clean decimal assertions (no more GetDecimal() casts!)
			.AssertJsonPath<decimal>("$.price", price =>
			{
				price.ShouldBeGreaterThan(0);
				price.ShouldBeLessThan(10000);
			})

			.AssertStatusCode(200);
	}

	[Fact(Skip = "Remote test - requires deployed API environment", DisplayName = "Hybrid approach should validate both structure and business rules")]
	public void Example03_HybridApproach_StructureAndBusinessRules()
	{
		// BEST PRACTICE: Validate structure and business rules
		// 1. Validate key fields exist with correct types
		// 2. Validate business rules on those fields

		Given().ApiResource($"/api/products/1")
			.Get()
		.When()
			.Execute()
		.Then()
			// STEP 1: Validate structure (key fields exist)
			.AssertJsonPath<int>("$.id", id => id.ShouldBeGreaterThan(0))
			.AssertJsonPath<string>("$.name", name => name.ShouldNotBeNullOrEmpty())
			.AssertJsonPath<decimal>("$.price", price => price.ShouldBeGreaterThan(0))

			// STEP 2: Validate business rules
			.AssertJsonPath<decimal>("$.price", price => price.ShouldBePositive())
			.AssertJsonPath<string>("$.createdAt", date => date.ShouldNotBeNullOrEmpty())

			// STEP 3: Validate HTTP
			.AssertStatusCode(200);
	}

	[Fact(Skip = "Remote test - requires deployed API environment", DisplayName = "Array handling should work correctly with individual element validation")]
	public void Example04_ArrayHandling_StillWorks()
	{
		// Validate array responses by checking individual elements

		var result = Given().ApiResource($"/api/products")
			.Get()
		.When()
			.Execute()
		.Then()
			.AssertStatusCode(200)
			.GetResult();

		// Parse response and validate array
		var responseBody = result.ResponseBody?.ToString() ?? "";
		responseBody.ShouldNotBeNullOrEmpty();
		
		// Verify it's an array with elements
		var jsonDoc = System.Text.Json.JsonDocument.Parse(responseBody);
		jsonDoc.RootElement.ValueKind.ShouldBe(System.Text.Json.JsonValueKind.Array);
		jsonDoc.RootElement.GetArrayLength().ShouldBeGreaterThan(0);
		
		// Validate first element has expected structure
		var firstElement = jsonDoc.RootElement[0];
		firstElement.GetProperty("id").GetInt32().ShouldBeGreaterThan(0);
		firstElement.GetProperty("name").GetString().ShouldNotBeNullOrEmpty();
	}

	[Fact(Skip = "Remote test - requires deployed API environment", DisplayName = "Backward compatibility with legacy Func syntax should still work")]
	public void Example05_BackwardCompatibility_LegacyFuncStillWorks()
	{
		// The old Func<T, bool> syntax still works for backward compatibility

		Given().ApiResource($"/api/products/1")
			.Get()
		.When()
			.Execute()
		.Then()
			// ✅ Legacy syntax (still supported)
			.AssertJsonPath<int>("$.id", id => id == 1, "Product ID should be 1")
			.AssertJsonPath<decimal>("$.price", price => price > 0, "Price should be positive")

			.AssertStatusCode(200);
	}

	[Fact(Skip = "Remote test - requires deployed API environment", DisplayName = "Complete workflow should showcase all validation features")]
	public void Example06_CompleteWorkflow_ShowcaseAllFeatures()
	{
		// Create a product
		var newProduct = new
		{
			name = "Remote Hybrid Test Product",
			description = "Showcasing structure validation + assertions on remote API",
			price = 299.99m
		};

		var createResult = Given().ApiResource($"/api/products")
			.Post(newProduct)
		.When()
			.Execute()
		.Then()
			// ✅ Validate structure of POST response
			.AssertJsonPath<int>("$.id", id => id.ShouldBeGreaterThan(0))
			.AssertJsonPath<string>("$.name", name => name.ShouldBe(newProduct.name))
			.AssertJsonPath<string>("$.description", desc => desc.ShouldBe(newProduct.description))
			.AssertJsonPath<decimal>("$.price", price => price.ShouldBe(newProduct.price))
			.AssertJsonPath<string>("$.createdAt", date => date.ShouldNotBeNullOrEmpty())

			.AssertStatusCode(201)
			.GetResult();

		// Extract ID for GET request
		var productId = createResult.JsonPath<int>("$.id");

		// Verify with GET
		Given().ApiResource($"/api/products/{productId}")
			.Get()
		.When()
			.Execute()
		.Then()
			// ✅ Validate retrieved product matches created
			.AssertJsonPath<string>("$.name", name => name.ShouldBe(newProduct.name))
			.AssertJsonPath<string>("$.description", desc => desc.ShouldBe(newProduct.description))
			.AssertJsonPath<decimal>("$.price", price => price.ShouldBe(newProduct.price))

			.AssertStatusCode(200);

		// Cleanup
		Given().ApiResource($"/api/products/{productId}")
			.Delete()
		.When()
			.Execute()
		.Then()
			.AssertStatusCode(204);
	}

	[Fact(Skip = "Remote test - requires deployed API environment", DisplayName = "Detect breaking changes by validating all expected fields")]
	public void Example07_DetectBreakingChanges_ValidateAllFields()
	{
		// This test will FAIL if:
		// - A required field is removed
		// - Field types change (e.g., Price becomes string)
		// - Field names change (e.g., "name" -> "productName")

		Given().ApiResource($"/api/products/1")
			.Get()
		.When()
			.Execute()
		.Then()
			// ✅ Validate all expected fields exist with correct types
			.AssertJsonPath<int>("$.id", id => id.ShouldBeGreaterThan(0))
			.AssertJsonPath<string>("$.name", name => name.ShouldNotBeNullOrEmpty())
			.AssertJsonPath<string>("$.description", desc => desc.ShouldNotBeNull())
			.AssertJsonPath<decimal>("$.price", price => price.ShouldBeGreaterThanOrEqualTo(0))
			.AssertJsonPath<string>("$.createdAt", date => date.ShouldNotBeNullOrEmpty())
			.AssertStatusCode(200);

		// Field validation protects against breaking changes!
	}

	[Fact(Skip = "Remote test - requires deployed API environment", DisplayName = "Remote API response time should be within acceptable limits")]
	public void Example08_RemoteApi_ResponseTimeShouldBeAcceptable()
	{
		// Remote APIs have network latency - validate response time is reasonable
		var stopwatch = System.Diagnostics.Stopwatch.StartNew();

		Given().ApiResource($"/api/products/1")
			.Get()
		.When()
			.Execute()
		.Then()
			.AssertStatusCode(200);

		stopwatch.Stop();
		
		// Remote API should respond within 5 seconds (adjust based on your requirements)
		stopwatch.ElapsedMilliseconds.ShouldBeLessThan(5000, 
			$"Remote API took {stopwatch.ElapsedMilliseconds}ms which exceeds 5000ms threshold");
	}
}
