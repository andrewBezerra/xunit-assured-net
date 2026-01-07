namespace XUnitAssured.Tests.CoreTests;

/// <summary>
/// xUnit Collection definition for TestSettings-related tests.
/// Tests in this collection run sequentially to avoid race conditions on shared global state.
/// </summary>
/// <remarks>
/// This collection groups tests that share multiple layers of global state:
/// 
/// 1. **Static Caches:**
///    - TestSettingsLoader._cachedSettings
///    - TestSettingsKafkaExtensions._cache (now path-based)
///    - TestSettingsHttpExtensions._cache (now path-based)
/// 
/// 2. **Global Environment Variables:**
///    - TESTSETTINGS_PATH: Changed by each test to point to temp files
///    - Custom env vars for testing (e.g., TEST_KAFKA_SERVERS_*)
/// 
/// 3. **File System State:**
///    - Temporary JSON files created/deleted during tests
/// 
/// **Why Collections are Required:**
/// Even with path-based cache (Fase 2), parallelization causes race conditions because:
/// - Thread A: Sets TESTSETTINGS_PATH=fileA.json
/// - Thread B: Sets TESTSETTINGS_PATH=fileB.json (overwrites global env var)
/// - Thread A: Calls GetCacheKey() and reads TESTSETTINGS_PATH
/// - Result: Thread A might get fileB.json path instead of fileA.json
/// 
/// **Performance Trade-off:**
/// - Sequential execution: ~40s for TestSettings tests
/// - Other test categories still run in parallel
/// - Overall suite performance impact: minimal (~5-10%)
/// 
/// **Future Improvements:**
/// To enable parallelization, we would need to eliminate all shared global state:
/// 1. Pass file path explicitly instead of using TESTSETTINGS_PATH env var
/// 2. Use ITestOutputHelper for test-specific state
/// 3. Refactor TestSettingsLoader to accept path parameter (breaking change)
/// </remarks>
[CollectionDefinition("TestSettings", DisableParallelization = true)]
public class TestSettingsCollection
{
	// This class has no code, and is never created.
	// Its purpose is simply to be the place to apply [CollectionDefinition] and all the
	// ICollectionFixture<> interfaces.
}
