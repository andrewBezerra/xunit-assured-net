using SampleWebApi.Models;

namespace SampleWebApi.Controllers;

/// <summary>
/// The products, one store per running host.
/// </summary>
/// <remarks>
/// It used to be static fields of the controller, shared by every host in the process. Test
/// classes each start their own host, so a test that reset the products in one class could
/// remove, mid-test, a product another class had just created: the samples failed about one
/// run in ten, never in the test that reset. Registered as a singleton, each host gets its own
/// store, and a test class can only disturb its own data.
/// </remarks>
public sealed class ProductStore
{
	public object Lock { get; } = new();

	public List<Product> Products { get; } = new();

	public int NextId { get; set; }

	public ProductStore() => Reset();

	/// <summary>Back to the three initial products; the next id is 4.</summary>
	public void Reset()
	{
		lock (Lock)
		{
			Products.Clear();
			Products.AddRange(new[]
			{
				new Product { Id = 1, Name = "Laptop", Description = "High-performance laptop", Price = 1299.99m, CreatedAt = DateTime.UtcNow },
				new Product { Id = 2, Name = "Mouse", Description = "Wireless mouse", Price = 29.99m, CreatedAt = DateTime.UtcNow },
				new Product { Id = 3, Name = "Keyboard", Description = "Mechanical keyboard", Price = 89.99m, CreatedAt = DateTime.UtcNow }
			});

			NextId = 4;
		}
	}
}
