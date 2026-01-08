using XUnitAssured.Extensions.Http;
using XUnitAssured.Http.Extensions;

namespace XUnitAssured.Http.Samples.Remote.Test;

[Trait("Category", "CRUD Operations")]
[Trait("Environment", "Remote")]
/// <summary>
/// Remote tests demonstrating CRUD operations (GET, POST, PUT, DELETE) against a deployed API.
/// These tests showcase the fluent DSL for testing REST API endpoints on a remote server.
/// </summary>
/// <remarks>
/// Tests the same CRUD operations as local tests but against a remote server configured in testsettings.json.
/// Note: Some tests create/modify data on the remote server. Ensure proper cleanup or use dedicated test environment.
/// </remarks>
public class CrudOperationsTests : HttpSamplesRemoteTestBase, IClassFixture<HttpSamplesRemoteFixture>
{
	public CrudOperationsTests(HttpSamplesRemoteFixture fixture) : base(fixture)
	{
	}

	[Fact(Skip = "Remote test - requires deployed API environment", DisplayName = "GET all products should return list of products with 200 OK status")]
	public void Example01_GetAllProducts_ShouldReturnListOfProducts()
	{
		// Arrange & Act & Assert
		Given()
			.ApiResource("/api/products")
			.Get()
			.When()
				.Execute()
			.Then()
				.AssertStatusCode(200);
		// Note: Cannot validate array in root with current JsonPath implementation
	}

	[Fact(Skip = "Remote test - requires deployed API environment", DisplayName = "GET product by ID should return product details with 200 OK status")]
	public void Example02_GetProductById_ShouldReturnProduct()
	{
		// Arrange
		var productId = 1;

		// Act & Assert
		Given()
			.ApiResource($"/api/products/{productId}")
			.Get()
			.When()
				.Execute()
			.Then()
				.AssertStatusCode(200)
				.AssertJsonPath<int>("$.id", value => value == productId, $"Product ID should be {productId}")
				.AssertJsonPath<string>("$.name", value => !string.IsNullOrEmpty(value), "Product name should not be empty")
				.AssertJsonPath<decimal>("$.price", value => value > 0, "Product price should be positive");
	}

	[Fact(Skip = "Remote test - requires deployed API environment", DisplayName = "GET product by non-existent ID should return 404 Not Found")]
	public void Example03_GetProductById_NotFound_ShouldReturn404()
	{
		// Arrange
		var nonExistentId = 9999;

		// Act & Assert
		Given()
			.ApiResource($"/api/products/{nonExistentId}")
			.Get()
			.When()
				.Execute()
			.Then()
				.AssertStatusCode(404)
				.AssertJsonPath<string>("$.message", value => value?.Contains("not found") == true, "Should return 'not found' message");
	}

	[Fact(Skip = "Remote test - requires deployed API environment", DisplayName = "POST create product with valid data should return 201 Created with product details")]
	public void Example04_CreateProduct_ShouldReturnCreatedProduct()
	{
		// Arrange
		var newProduct = new
		{
			name = "Remote Test Product",
			description = "A product created by remote automated test",
			price = 99.99m
		};

		// Act & Assert
		Given()
			.ApiResource("/api/products")
			.Post(newProduct)
			.When()
				.Execute()
			.Then()
				.AssertStatusCode(201)
				.AssertJsonPath<int>("$.id", value => value > 0, "Created product should have an ID")
				.AssertJsonPath<string>("$.name", value => value == newProduct.name, $"Product name should be '{newProduct.name}'")
				.AssertJsonPath<string>("$.description", value => value == newProduct.description, $"Product description should be '{newProduct.description}'")
				.AssertJsonPath<decimal>("$.price", value => value == newProduct.price, $"Product price should be {newProduct.price}")
				.AssertJsonPath<string>("$.createdAt", value => !string.IsNullOrEmpty(value), "Created product should have createdAt timestamp");
	}

	[Fact(Skip = "Remote test - requires deployed API environment", DisplayName = "POST create product with invalid data should return 400 Bad Request")]
	public void Example05_CreateProduct_WithInvalidData_ShouldReturn400()
	{
		// Arrange - Product with empty name (invalid)
		var invalidProduct = new
		{
			name = "",
			description = "Invalid product",
			price = 10.0m
		};

		// Act & Assert
		Given()
			.ApiResource("/api/products")
			.Post(invalidProduct)
		.When()
			.Execute()
			.Then()
				.AssertStatusCode(400)
				.AssertJsonPath<string>("$.message", value => value?.Contains("required") == true, "Should return validation error message");
	}

	[Fact(Skip = "Remote test - requires deployed API environment", DisplayName = "POST create product with negative price should return 400 Bad Request")]
	public void Example06_CreateProduct_WithNegativePrice_ShouldReturn400()
	{
		// Arrange - Product with negative price (invalid)
		var invalidProduct = new
		{
			name = "Invalid Product",
			description = "Product with negative price",
			price = -50.0m
		};

		// Act & Assert
		Given()
			.ApiResource("/api/products")
			.Post(invalidProduct)
		.When()
			.Execute()
			.Then()
				.AssertStatusCode(400)
				.AssertJsonPath<string>("$.message", value => value?.Contains("non-negative") == true, "Should return price validation error");
	}

	[Fact(Skip = "Remote test - requires deployed API environment", DisplayName = "PUT update product should return 200 OK with updated product details")]
	public void Example07_UpdateProduct_ShouldReturnUpdatedProduct()
	{
		// Arrange
		var productId = 1;
		var updatedProduct = new
		{
			name = "Updated Laptop (Remote)",
			description = "Updated description for laptop from remote test",
			price = 1499.99m
		};

		// Act & Assert
		Given()
			.ApiResource($"/api/products/{productId}")
			.Put(updatedProduct)
		.When()
			.Execute()
		.Then()
				.AssertStatusCode(200)
				.AssertJsonPath<int>("$.id", value => value == productId, $"Product ID should remain {productId}")
				.AssertJsonPath<string>("$.name", value => value == updatedProduct.name, $"Product name should be updated to '{updatedProduct.name}'")
				.AssertJsonPath<string>("$.description", value => value == updatedProduct.description, $"Product description should be updated")
				.AssertJsonPath<decimal>("$.price", value => value == updatedProduct.price, $"Product price should be updated to {updatedProduct.price}")
				.AssertJsonPath<string>("$.updatedAt", value => !string.IsNullOrEmpty(value), "Updated product should have updatedAt timestamp");
	}

	[Fact(Skip = "Remote test - requires deployed API environment", DisplayName = "PUT update non-existent product should return 404 Not Found")]
	public void Example08_UpdateProduct_NotFound_ShouldReturn404()
	{
		// Arrange
		var nonExistentId = 9999;
		var updatedProduct = new
		{
			name = "Non-existent Product",
			description = "This product doesn't exist",
			price = 100.0m
		};

		// Act & Assert
		Given()
			.ApiResource($"/api/products/{nonExistentId}")
			.Put(updatedProduct)
			.When()
				.Execute()
			.Then()
				.AssertStatusCode(404)
				.AssertJsonPath<string>("$.message", value => value?.Contains("not found") == true, "Should return 'not found' message");
	}

	[Fact(Skip = "Remote test - requires deployed API environment", DisplayName = "PUT update product with invalid data should return 400 Bad Request")]
	public void Example09_UpdateProduct_WithInvalidData_ShouldReturn400()
	{
		// Arrange
		var productId = 1;
		var invalidProduct = new
		{
			name = "",
			description = "Invalid update",
			price = 100.0m
		};

		// Act & Assert
		Given()
			.ApiResource($"/api/products/{productId}")
			.Put(invalidProduct)
			.When()
				.Execute()
			.Then()
				.AssertStatusCode(400)
				.AssertJsonPath<string>("$.message", value => value?.Contains("required") == true, "Should return validation error");
	}

	[Fact(Skip = "Remote test - requires deployed API environment", DisplayName = "DELETE product should return 204 No Content and product should not be found")]
	public void Example10_DeleteProduct_ShouldReturn204()
	{
		// Arrange - First create a product to delete
		var productToDelete = new
		{
			name = "Remote Product To Delete",
			description = "This product will be deleted by remote test",
			price = 50.0m
		};

		var createResponse = Given()
			.ApiResource("/api/products")
			.Post(productToDelete)
			.When()
				.Execute()
			.Then()
				.AssertStatusCode(201)
				.GetResult();

		// Extract the created product ID
		var createdProductId = createResponse.JsonPath<int>("$.id");

		// Act & Assert - Delete the product
		Given().ApiResource($"/api/products/{createdProductId}")
			.Delete()
		.When()
			.Execute()
		.Then()
			.AssertStatusCode(204);

		// Verify product was deleted
		Given()
			.ApiResource($"/api/products/{createdProductId}")
			.Get()
			.When()
				.Execute()
			.Then()
				.AssertStatusCode(404);
	}

	[Fact(Skip = "Remote test - requires deployed API environment", DisplayName = "DELETE non-existent product should return 404 Not Found")]
	public void Example11_DeleteProduct_NotFound_ShouldReturn404()
	{
		// Arrange
		var nonExistentId = 9999;

		// Act & Assert
		Given().ApiResource($"/api/products/{nonExistentId}")
			.Delete()
		.When()
			.Execute()
		.Then()
			.AssertStatusCode(404)
			.AssertJsonPath<string>("$.message", value => value?.Contains("not found") == true, "Should return 'not found' message");
	}

	[Fact(Skip = "Remote test - requires deployed API environment", DisplayName = "Complete CRUD workflow should create, read, update, and delete product successfully")]
	public void Example12_CompleteWorkflow_CreateReadUpdateDelete()
	{
		// Step 1: Create a new product
		var newProduct = new
		{
			name = "Remote Workflow Test Product",
			description = "Testing complete CRUD workflow on remote API",
			price = 199.99m
		};

		var createResult = Given()
			.ApiResource("/api/products")
			.Post(newProduct)
		.When()
				.Execute()
			.Then()
				.AssertStatusCode(201)
				.GetResult();

		var productId = createResult.JsonPath<int>("$.id");

		// Step 2: Read the created product
		Given()
			.ApiResource($"/api/products/{productId}")
			.Get()
		.When()
				.Execute()
			.Then()
				.AssertStatusCode(200)
				.AssertJsonPath<string>("$.name", value => value == newProduct.name, "Product name should match");

		// Step 3: Update the product
		var updatedProduct = new
		{
			name = "Updated Remote Workflow Product",
			description = "Updated during remote workflow test",
			price = 249.99m
		};

		Given()
			.ApiResource($"/api/products/{productId}")
			.Put(updatedProduct)
		.When()
				.Execute()
			.Then()
				.AssertStatusCode(200)
				.AssertJsonPath<string>("$.name", value => value == updatedProduct.name, "Product name should be updated");

		// Step 4: Delete the product
		Given().ApiResource($"/api/products/{productId}")
			.Delete()
		.When()
			.Execute()
		.Then()
			.AssertStatusCode(204);

		// Step 5: Verify deletion
		Given()
			.ApiResource($"/api/products/{productId}")
			.Get()
		.When()
				.Execute()
			.Then()
				.AssertStatusCode(404);
	}

	[Fact(Skip = "Remote test - requires deployed API environment", DisplayName = "POST reset products should restore initial state with 3 products")]
	public void Example13_ResetProducts_ShouldRestoreInitialState()
	{
		// Act & Assert
		Given()
			.ApiResource("/api/products/reset")
			.Post(new { })
			.When()
				.Execute()
			.Then()
				.AssertStatusCode(200)
				.AssertJsonPath<int>("$.count", value => value == 3, "Should reset to 3 initial products");
	}
}
