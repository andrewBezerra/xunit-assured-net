using System;

namespace XUnitAssured.Base;

/// <summary>
/// [OBSOLETE] This class is deprecated in favor of BaseSettingsValidator which uses IValidateOptions.
/// Will be removed in v3.0.
/// </summary>
[Obsolete("Use BaseSettingsValidator with IValidateOptions<BaseSettings> pattern instead. This class will be removed in v3.0.", error: false)]
public class TestSettingsValidator
{
	/// <summary>
	/// [OBSOLETE] Validation is now handled by BaseSettingsValidator using the Options pattern.
	/// </summary>
	[Obsolete("Configure validation using services.AddOptions<BaseSettings>().ValidateOnStart() with BaseSettingsValidator instead.", error: false)]
	public void Validate(BaseSettings settings)
	{
		throw new NotSupportedException(
			"TestSettingsValidator.Validate() is obsolete. " +
			"Use the Options pattern with IValidateOptions<BaseSettings> instead. " +
			"See BREAKING_CHANGES_V2.md for migration guide.");
	}
}

