using Microsoft.AspNetCore.Mvc;
using SampleWebApi.Models;

namespace SampleWebApi.Controllers;

/// <summary>
/// Products controller for CRUD operations testing.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class ProductsController : ControllerBase
{
	private static readonly List<Product> _products = new()
	{
		new Product { Id = 1, Name = "Laptop", Description = "High-performance laptop", Price = 1299.99m, CreatedAt = DateTime.UtcNow },
		new Product { Id = 2, Name = "Mouse", Description = "Wireless mouse", Price = 29.99m, CreatedAt = DateTime.UtcNow },
		new Product { Id = 3, Name = "Keyboard", Description = "Mechanical keyboard", Price = 89.99m, CreatedAt = DateTime.UtcNow }
	};

	private static int _nextId = 4;
	private readonly ILogger<ProductsController> _logger;

	public ProductsController(ILogger<ProductsController> logger)
	{
		_logger = logger;
	}

	/// <summary>
	/// Get all products.
	/// </summary>
	/// <returns>List of all products</returns>
	[HttpGet]
	[ProducesResponseType(typeof(IEnumerable<Product>), StatusCodes.Status200OK)]
	public ActionResult<IEnumerable<Product>> GetAll()
	{
		_logger.LogInformation("Fetching all products. Count: {Count}", _products.Count);
		return Ok(_products);
	}

	/// <summary>
	/// Get product by ID.
	/// </summary>
	/// <param name="id">Product ID</param>
	/// <returns>Product details</returns>
	[HttpGet("{id}")]
	[ProducesResponseType(typeof(Product), StatusCodes.Status200OK)]
	[ProducesResponseType(StatusCodes.Status404NotFound)]
	public ActionResult<Product> GetById(int id)
	{
		_logger.LogInformation("Fetching product with ID: {Id}", id);
		
		var product = _products.FirstOrDefault(p => p.Id == id);
		
		if (product == null)
		{
			_logger.LogWarning("Product with ID {Id} not found", id);
			return NotFound(new { message = $"Product with ID {id} not found" });
		}

		return Ok(product);
	}

	/// <summary>
	/// Create a new product.
	/// </summary>
	/// <param name="product">Product to create</param>
	/// <returns>Created product</returns>
	[HttpPost]
	[ProducesResponseType(typeof(Product), StatusCodes.Status201Created)]
	[ProducesResponseType(StatusCodes.Status400BadRequest)]
	public ActionResult<Product> Create([FromBody] Product product)
	{
		if (string.IsNullOrWhiteSpace(product.Name))
		{
			_logger.LogWarning("Attempted to create product with empty name");
			return BadRequest(new { message = "Product name is required" });
		}

		if (product.Price < 0)
		{
			_logger.LogWarning("Attempted to create product with negative price: {Price}", product.Price);
			return BadRequest(new { message = "Product price must be non-negative" });
		}

		product.Id = _nextId++;
		product.CreatedAt = DateTime.UtcNow;
		product.UpdatedAt = null;

		_products.Add(product);

		_logger.LogInformation("Created product with ID: {Id}, Name: {Name}", product.Id, product.Name);

		return CreatedAtAction(nameof(GetById), new { id = product.Id }, product);
	}

	/// <summary>
	/// Update an existing product.
	/// </summary>
	/// <param name="id">Product ID</param>
	/// <param name="updatedProduct">Updated product data</param>
	/// <returns>Updated product</returns>
	[HttpPut("{id}")]
	[ProducesResponseType(typeof(Product), StatusCodes.Status200OK)]
	[ProducesResponseType(StatusCodes.Status400BadRequest)]
	[ProducesResponseType(StatusCodes.Status404NotFound)]
	public ActionResult<Product> Update(int id, [FromBody] Product updatedProduct)
	{
		_logger.LogInformation("Updating product with ID: {Id}", id);

		var product = _products.FirstOrDefault(p => p.Id == id);

		if (product == null)
		{
			_logger.LogWarning("Product with ID {Id} not found for update", id);
			return NotFound(new { message = $"Product with ID {id} not found" });
		}

		if (string.IsNullOrWhiteSpace(updatedProduct.Name))
		{
			_logger.LogWarning("Attempted to update product {Id} with empty name", id);
			return BadRequest(new { message = "Product name is required" });
		}

		if (updatedProduct.Price < 0)
		{
			_logger.LogWarning("Attempted to update product {Id} with negative price: {Price}", id, updatedProduct.Price);
			return BadRequest(new { message = "Product price must be non-negative" });
		}

		// Update properties
		product.Name = updatedProduct.Name;
		product.Description = updatedProduct.Description;
		product.Price = updatedProduct.Price;
		product.UpdatedAt = DateTime.UtcNow;

		_logger.LogInformation("Updated product ID: {Id}, Name: {Name}", product.Id, product.Name);

		return Ok(product);
	}

	/// <summary>
	/// Delete a product.
	/// </summary>
	/// <param name="id">Product ID</param>
	/// <returns>No content on success</returns>
	[HttpDelete("{id}")]
	[ProducesResponseType(StatusCodes.Status204NoContent)]
	[ProducesResponseType(StatusCodes.Status404NotFound)]
	public IActionResult Delete(int id)
	{
		_logger.LogInformation("Deleting product with ID: {Id}", id);

		var product = _products.FirstOrDefault(p => p.Id == id);

		if (product == null)
		{
			_logger.LogWarning("Product with ID {Id} not found for deletion", id);
			return NotFound(new { message = $"Product with ID {id} not found" });
		}

		_products.Remove(product);

		_logger.LogInformation("Deleted product with ID: {Id}", id);

		return NoContent();
	}

	/// <summary>
	/// Reset products to initial state (for testing purposes).
	/// </summary>
	[HttpPost("reset")]
	[ProducesResponseType(StatusCodes.Status200OK)]
	public IActionResult Reset()
	{
		_logger.LogInformation("Resetting products to initial state");

		_products.Clear();
		_products.AddRange(new[]
		{
			new Product { Id = 1, Name = "Laptop", Description = "High-performance laptop", Price = 1299.99m, CreatedAt = DateTime.UtcNow },
			new Product { Id = 2, Name = "Mouse", Description = "Wireless mouse", Price = 29.99m, CreatedAt = DateTime.UtcNow },
			new Product { Id = 3, Name = "Keyboard", Description = "Mechanical keyboard", Price = 89.99m, CreatedAt = DateTime.UtcNow }
		});

		_nextId = 4;

		return Ok(new { message = "Products reset successfully", count = _products.Count });
	}
}
