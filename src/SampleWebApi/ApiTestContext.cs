using Microsoft.EntityFrameworkCore;

namespace SampleWebApi;

public class ApiTestContext : DbContext
{
	public ApiTestContext(DbContextOptions<ApiTestContext> options) : base(options) { }
}
