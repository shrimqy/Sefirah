// Copyright (c) Files Community
// Licensed under the MIT License.

using System.Globalization;
using Sefirah.Data.Items;
using Windows.Globalization;
using Windows.System.UserProfile;

namespace Sefirah.Helpers;

/// <summary>
/// Provides static helper to manage supported languages in the application.
/// </summary>
public static class AppLanguageHelper
{
	/// <summary>
	/// A constant string representing the default language code.
	/// It is initialized as an empty string.
	/// </summary>
	private static readonly string _defaultCode = string.Empty;

	/// <summary>
	/// A collection of available languages.
	/// </summary>
	public static ObservableCollection<AppLanguageItem> SupportedLanguages { get; }

	/// <summary>
	/// Gets the preferred language.
	/// </summary>
	public static AppLanguageItem PreferredLanguage { get; private set; }

	/// <summary>
	/// Gets the preferred language.
	/// </summary>
	public static bool IsPreferredLanguageRtl
	{
		get
		{
			if (PreferredLanguage.Code is null)
				return false;

			var culture = new CultureInfo(PreferredLanguage.Code);
			return culture.TextInfo.IsRightToLeft;
		}
	}

	/// <summary>
	/// Initializes the <see cref="AppLanguageHelper"/> class.
	/// </summary>
	static AppLanguageHelper()
	{
		// Populate the Languages collection with available languages
		var appLanguages = ApplicationLanguages.ManifestLanguages
			.Append(string.Empty) // Add default language code
			.Select(language => new AppLanguageItem(language))
			.OrderBy(language => language.Code is not "") // Default language on top
			.ThenBy(language => language.Name)
			.ToList();

		// Get the current primary language override.
		var current = new AppLanguageItem(ApplicationLanguages.PrimaryLanguageOverride);

		// Find the index of the saved language
		var index = appLanguages.IndexOf(appLanguages.FirstOrDefault(dl => dl.Name == current.Name) ?? appLanguages.First());

		// Set the system default language as the first item in the Languages collection
		var systemCulture = GetSystemCulture();
		appLanguages[0] = IsSupported(appLanguages, systemCulture)
			? new AppLanguageItem(systemCulture.Name, systemDefault: true)
			: new AppLanguageItem("en-US", systemDefault: true);

		// Initialize the list
		SupportedLanguages = new(appLanguages);
		PreferredLanguage = SupportedLanguages[index];
	}

	/// <summary>
	/// Gets the culture the app falls back to when no language override is set.
	/// </summary>
	/// <remarks>
	/// <see cref="GlobalizationPreferences"/> reports the display language the user picked in Windows.
	/// <see cref="CultureInfo.InstalledUICulture"/> is wrong here because it reports en-US inside a
	/// packaged app, and <see cref="CultureInfo.CurrentUICulture"/> follows this app's own override.
	/// </remarks>
	private static CultureInfo GetSystemCulture()
	{
		var preferred = GlobalizationPreferences.Languages.FirstOrDefault();
		if (string.IsNullOrEmpty(preferred))
			return CultureInfo.CurrentUICulture;

		try
		{
			return new CultureInfo(preferred);
		}
		catch (CultureNotFoundException)
		{
			return CultureInfo.CurrentUICulture;
		}
	}

	/// <summary>
	/// Determines whether the app ships resources for the given culture.
	/// </summary>
	/// <remarks>
	/// Manifest languages can be neutral, e.g. "cs" covers "cs-CZ", so both forms are matched.
	/// </remarks>
	private static bool IsSupported(IEnumerable<AppLanguageItem> languages, CultureInfo culture)
		=> languages.Any(language =>
			language.Code.Equals(culture.Name, StringComparison.OrdinalIgnoreCase) ||
			language.Code.Equals(culture.TwoLetterISOLanguageName, StringComparison.OrdinalIgnoreCase));

	/// <summary>
	/// Attempts to change the preferred language code by index.
	/// </summary>
	/// <param name="index">The index of the new language.</param>
	/// <returns>True if the language was successfully changed; otherwise, false.</returns>
	public static bool TryChange(int index)
	{
		if (index >= SupportedLanguages.Count || PreferredLanguage == SupportedLanguages[index])
			return false;

		PreferredLanguage = SupportedLanguages[index];

		// Update the primary language override
		ApplicationLanguages.PrimaryLanguageOverride = index == 0 ? _defaultCode : PreferredLanguage.Code;
		return true;
	}
}
