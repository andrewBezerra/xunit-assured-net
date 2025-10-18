using SampleWebApi;
using Microsoft.EntityFrameworkCore;
using Serilog;
using Serilog.Enrichers.Span;
using Serilog.Formatting.Json;

var builder = WebApplication.CreateBuilder(args);
var config = builder.Configuration;

builder.Host.UseSerilog((_, lc) => lc.ReadFrom.Configuration(config)
												.WriteTo.Console(new JsonFormatter(renderMessage: true))
												.Enrich.FromLogContext()
												.Enrich.With<ActivityEnricher>() // <- Adiciona TraceId, SpanId, ParentId
												.Enrich.With<ActivityTagEnricher>()
												.Enrich.WithProperty("Service", new ServiceInfo())
												.Enrich.WithSpan());

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();

if (!builder.Environment.IsEnvironment("Testing"))
	builder.Services.AddDbContext<ApiTestContext>((serviceprovider, options) =>
			{
				options.UseSqlServer("name=ConnectionStrings:SqlServer", b =>
				{
					b.MigrationsAssembly("Jazz.SampleWebApi.ServiceHost");
				});
				options.UseLoggerFactory(serviceprovider.GetRequiredService<ILoggerFactory>());
				options.EnableDetailedErrors();
				options.EnableSensitiveDataLogging();

			});


builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(HandlerTest).Assembly));
builder.Services.AddSwaggerGen();

var app = builder.Build();

app.MapTestApplication();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
	app.UseSwagger();
	app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthorization();





await app.RunAsync();


public partial class Program
{
	protected Program()
	{
	}
}
