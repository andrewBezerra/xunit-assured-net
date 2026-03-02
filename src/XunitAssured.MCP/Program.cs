using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModelContextProtocol;

var builder = Host.CreateApplicationBuilder(args);

// Suppress all console/debug logging — stdio transport uses stdin/stdout
// for MCP protocol messages, any extra output breaks communication.
builder.Logging.ClearProviders();
builder.Logging.SetMinimumLevel(LogLevel.None);

builder.Services
	.AddMcpServer(options =>
	{
		options.ServerInfo = new()
		{
			Name = "XUnitAssured",
			Version = "1.0.0"
		};
	})
	.WithStdioServerTransport()
	.WithToolsFromAssembly();

await builder.Build().RunAsync();
