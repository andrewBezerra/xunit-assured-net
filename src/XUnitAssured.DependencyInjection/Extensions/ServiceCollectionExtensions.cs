using System;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace XUnitAssured.DependencyInjection.Extensions;

/// <summary>
/// Extension methods for IServiceCollection to simplify common DI operations in tests.
/// </summary>
public static class ServiceCollectionExtensions
{
	/// <summary>
	/// Adds and configures a settings class from a configuration section with validation.
	/// </summary>
	/// <typeparam name="TSettings">The settings type</typeparam>
	/// <param name="services">Service collection</param>
	/// <param name="configuration">Configuration instance</param>
	/// <param name="sectionName">Configuration section name</param>
	/// <param name="validateOnStart">Whether to validate options on application start (default: true)</param>
	/// <returns>Service collection for chaining</returns>
	public static IServiceCollection AddSettings<TSettings>(
		this IServiceCollection services,
		IConfiguration configuration,
		string sectionName,
		bool validateOnStart = true)
		where TSettings : class
	{
		var optionsBuilder = services.AddOptions<TSettings>()
			.Bind(configuration.GetSection(sectionName));

		if (validateOnStart)
		{
			optionsBuilder.ValidateOnStart();
		}

		return services;
	}

	/// <summary>
	/// Gets a configured settings instance from the service collection.
	/// Useful for getting settings during fixture setup.
	/// </summary>
	/// <typeparam name="TSettings">The settings type</typeparam>
	/// <param name="services">Service collection</param>
	/// <returns>Configured settings instance</returns>
	public static TSettings GetSettings<TSettings>(this IServiceCollection services)
		where TSettings : class
	{
		using var tempProvider = services.BuildServiceProvider();
		return tempProvider.GetRequiredService<IOptions<TSettings>>().Value;
	}

	/// <summary>
	/// Adds a singleton service with automatic disposal if it implements IDisposable or IAsyncDisposable.
	/// </summary>
	/// <typeparam name="TService">Service type</typeparam>
	/// <typeparam name="TImplementation">Implementation type</typeparam>
	/// <param name="services">Service collection</param>
	/// <returns>Service collection for chaining</returns>
	public static IServiceCollection AddSingletonWithDisposal<TService, TImplementation>(
		this IServiceCollection services)
		where TService : class
		where TImplementation : class, TService
	{
		return services.AddSingleton<TService, TImplementation>();
	}

	/// <summary>
	/// Adds a singleton service instance with automatic disposal if it implements IDisposable or IAsyncDisposable.
	/// </summary>
	/// <typeparam name="TService">Service type</typeparam>
	/// <param name="services">Service collection</param>
	/// <param name="instance">Service instance</param>
	/// <returns>Service collection for chaining</returns>
	public static IServiceCollection AddSingletonWithDisposal<TService>(
		this IServiceCollection services,
		TService instance)
		where TService : class
	{
		return services.AddSingleton(instance);
	}
}
