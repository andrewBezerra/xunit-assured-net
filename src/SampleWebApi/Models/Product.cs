namespace SampleWebApi.Models;

/// <summary>
/// Product model for CRUD operations testing.
/// </summary>
public class Product
{
	/// <summary>
	/// Product unique identifier.
	/// </summary>
	public int Id { get; set; }

	/// <summary>
	/// Product name.
	/// </summary>
	public string Name { get; set; } = string.Empty;

	/// <summary>
	/// Product description.
	/// </summary>
	public string? Description { get; set; }

	/// <summary>
	/// Product price.
	/// </summary>
	public decimal Price { get; set; }

	/// <summary>
	/// Product creation date.
	/// </summary>
	public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

	/// <summary>
	/// Product last update date.
	/// </summary>
	public DateTime? UpdatedAt { get; set; }
}
