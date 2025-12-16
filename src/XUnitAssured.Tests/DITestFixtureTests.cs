using System.Collections.Generic;
using System.Threading.Tasks;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using Xunit;
using Xunit.Microsoft.DependencyInjection;
using Xunit.Microsoft.DependencyInjection.Abstracts;

using XUnitAssured.DependencyInjection;

namespace XUnitAssured.Tests;

[Trait("Unit Test", "DITestFixture")]
public class DITestFixtureTests
{
	/// <summary>
	/// Concrete test fixture for testing purposes.
	/// </summary>
	private class ConcreteDITestFixture : DITestFixture
	{
		protected override void AddServices(IServiceCollection services, IConfiguration? configuration)
		{
			// Base implementation: do nothing
		}

		protected override ValueTask DisposeAsyncCore()
		{
			return new ValueTask();
		}

		protected override IEnumerable<TestAppSettings> GetTestAppSettings()
		{
			yield break;
		}

		// Expose methods for testing
		public void AddServicesPublic(IServiceCollection services, IConfiguration? configuration)
		{
			AddServices(services, configuration);
		}

		public IEnumerable<(string Filename, bool IsOptional)> GetTestAppSettingsPublic()
		{
			foreach (var setting in GetTestAppSettings())
			{
				yield return (setting.Filename, setting.IsOptional);
			}
		}

		public ValueTask DisposeAsyncCorePublic()
		{
			return DisposeAsyncCore();
		}
	}

	[Fact(DisplayName = "AddServices should not throw when called with null configuration")]
	public void AddServices_ShouldNotThrow_WithNullConfiguration()
	{
		// Arrange
		var fixture = new ConcreteDITestFixture();
		var services = new ServiceCollection();

		// Act & Assert - Should not throw
		fixture.AddServicesPublic(services, null);
	}

	[Fact(DisplayName = "AddServices should not add any services by default")]
	public void AddServices_ShouldNotAddServices_ByDefault()
	{
		// Arrange
		var fixture = new ConcreteDITestFixture();
		var services = new ServiceCollection();
		var initialCount = services.Count;

		// Act
		fixture.AddServicesPublic(services, null);

		// Assert
		Assert.Equal(initialCount, services.Count);
	}

	[Fact(DisplayName = "GetTestAppSettings should return empty by default")]
	public void GetTestAppSettings_ShouldReturnEmpty_ByDefault()
	{
		// Arrange
		var fixture = new ConcreteDITestFixture();

		// Act
		var settings = fixture.GetTestAppSettingsPublic().ToList();

		// Assert
		Assert.Empty(settings);
	}

	[Fact(DisplayName = "DisposeAsyncCore should complete successfully")]
	public async Task DisposeAsyncCore_ShouldCompleteSuccessfully()
	{
		// Arrange
		var fixture = new ConcreteDITestFixture();

		// Act
		var result = fixture.DisposeAsyncCorePublic();
		await result;

		// Assert
		Assert.True(result.IsCompletedSuccessfully);
	}

	[Fact(DisplayName = "DITestFixture should be abstract and cannot be instantiated directly")]
	public void DITestFixture_ShouldBeAbstract()
	{
		// Assert
		Assert.True(typeof(DITestFixture).IsAbstract);
	}

	[Fact(DisplayName = "DITestFixture should inherit from TestBedFixture")]
	public void DITestFixture_ShouldInheritFromTestBedFixture()
	{
		// Assert
		Assert.True(typeof(TestBedFixture).IsAssignableFrom(typeof(DITestFixture)));
	}
}
