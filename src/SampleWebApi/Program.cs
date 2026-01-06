using SampleWebApi;
using SampleWebApi.Authentication;
using Microsoft.AspNetCore.Server.Kestrel.Https;
using Microsoft.EntityFrameworkCore;
using Serilog;
using Serilog.Enrichers.Span;
using Serilog.Formatting.Json;

var builder = WebApplication.CreateBuilder(args);
var config = builder.Configuration;

// Configure Kestrel to accept client certificates for mTLS testing
builder.WebHost.ConfigureKestrel(serverOptions =>
{
	serverOptions.ConfigureHttpsDefaults(httpsOptions =>
	{
		httpsOptions.ClientCertificateMode = ClientCertificateMode.AllowCertificate;
	});
});

builder.Host.UseSerilog((_, lc) => lc.ReadFrom.Configuration(config)
												.WriteTo.Console(new JsonFormatter(renderMessage: true))
												.Enrich.FromLogContext()
												.Enrich.With<ActivityEnricher>() // <- Adiciona TraceId, SpanId, ParentId
												.Enrich.With<ActivityTagEnricher>()
												.Enrich.WithProperty("Service", new ServiceInfo())
												.Enrich.WithSpan());

// Add services to the container.

// Configure Authentication with multiple schemes
builder.Services.AddAuthentication()
	.AddJwtBearer(options =>
	{
		options.TokenValidationParameters = new Microsoft.IdentityModel.Tokens.TokenValidationParameters
		{
			ValidateIssuer = true,
			ValidateAudience = true,
			ValidateLifetime = false,
			ValidateIssuerSigningKey = false,
			ValidIssuers = new[] { "dotnet-user-jwts", "local-auth" },
			ValidAudiences = new[] { "https://localhost:7259", "http://localhost:5259" }
		};
	})
	.AddBasicAuthentication()
	.AddApiKeyAuthentication()
	.AddCustomHeaderAuthentication();

builder.Services.AddAuthorization(options =>
{
	// Basic Authentication Policy
	options.AddPolicy("BasicAuthPolicy", policy =>
	{
		policy.AddAuthenticationSchemes(BasicAuthenticationOptions.DefaultScheme);
		policy.RequireAuthenticatedUser();
	});

	// API Key Authentication Policy
	options.AddPolicy("ApiKeyPolicy", policy =>
	{
		policy.AddAuthenticationSchemes(ApiKeyAuthenticationOptions.DefaultScheme);
		policy.RequireAuthenticatedUser();
	});

	// Custom Header Authentication Policy
	options.AddPolicy("CustomHeaderPolicy", policy =>
	{
		policy.AddAuthenticationSchemes(CustomHeaderAuthenticationOptions.DefaultScheme);
		policy.RequireAuthenticatedUser();
	});

	// Bearer/JWT Authentication Policy
	options.AddPolicy("BearerPolicy", policy =>
	{
		policy.AddAuthenticationSchemes("Bearer");
		policy.RequireAuthenticatedUser();
	});

	// OAuth2 Authentication Policy
	options.AddPolicy("OAuth2Policy", policy =>
	{
		policy.AddAuthenticationSchemes("Bearer");
		policy.RequireAuthenticatedUser();
		policy.RequireClaim("AuthType", "OAuth2");
	});

	// Certificate Authentication Policy - Requires manual validation
	options.AddPolicy("CertificatePolicy", policy =>
	{
		policy.RequireAssertion(context =>
		{
			var httpContext = context.Resource as HttpContext;
			return httpContext?.Connection.ClientCertificate != null;
		});
	});
});

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
builder.Services.AddSwaggerGen(c =>
{
	c.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
	{
		Title = "Sample Web API",
		Version = "v1",
		Description = "API para testes de autenticação com múltiplos esquemas",

	});

	// Define security schemes
	c.AddSecurityDefinition("Basic", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
	{
		Type = Microsoft.OpenApi.Models.SecuritySchemeType.Http,
		Scheme = "basic",
		Description = "Basic Authentication. Username: admin, Password: secret123"
	});

	c.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
	{
		Type = Microsoft.OpenApi.Models.SecuritySchemeType.Http,
		Scheme = "bearer",
		BearerFormat = "JWT",
		Description = "JWT Bearer Token. Token: my-super-secret-token-12345"
	});

	c.AddSecurityDefinition("ApiKey", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
	{
		Type = Microsoft.OpenApi.Models.SecuritySchemeType.ApiKey,
		In = Microsoft.OpenApi.Models.ParameterLocation.Header,
		Name = "X-API-Key",
		Description = "API Key. Value: api-key-header-abc123xyz"
	});

	c.AddSecurityDefinition("OAuth2", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
	{
		Type = Microsoft.OpenApi.Models.SecuritySchemeType.OAuth2,
		Flows = new Microsoft.OpenApi.Models.OpenApiOAuthFlows
		{
			ClientCredentials = new Microsoft.OpenApi.Models.OpenApiOAuthFlow
			{
				TokenUrl = new Uri("/api/auth/oauth2/token", UriKind.Relative),
				Scopes = new Dictionary<string, string>
				{
					["read"] = "Read access",
					["write"] = "Write access"
				}
			}
		},
		Description = "OAuth2 Client Credentials flow"
	});
});

var app = builder.Build();

app.MapTestApplication();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
	app.UseSwagger();
	app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

await app.RunAsync();


public partial class Program
{
	protected Program()
	{
	}
}
