// Copyright (c) Files Community
// Licensed under the MIT License.

using System.Globalization;
using System.IO;
using Windows.ApplicationModel;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Globalization;
using Windows.Storage;

namespace Files.App.Helpers
{
	/// <summary>
	/// Compatibility layer over <see cref="ApplicationData"/> and <see cref="Package"/> so the app
	/// can run without package identity (unpackaged self-contained builds, e.g. on Windows Server).
	/// Fallbacks are backed by plain directories under LocalApplicationData and by in-memory
	/// <see cref="PropertySet"/> stores; single-instance flags do not persist across restarts there.
	/// </summary>
	internal static class AppData
	{
		static AppData()
		{
			try
			{
				_ = Package.Current.Id;
				HasPackageIdentity = true;
			}
			catch
			{
				HasPackageIdentity = false;
			}

			if (!HasPackageIdentity)
			{
				Directory.CreateDirectory(LocalFolderPath);
				Directory.CreateDirectory(RoamingFolderPath);
				Directory.CreateDirectory(TemporaryFolderPath);
				Directory.CreateDirectory(LocalCacheFolderPath);
			}
		}

		/// <summary>Whether the process runs with package identity.</summary>
		public static bool HasPackageIdentity { get; }

		private static string FallbackRoot
			=> Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "FilesDev");

		// Settings

		private static readonly AppSettingsContainer _localSettings = new();
		public static AppSettingsContainer LocalSettings => _localSettings;
		public static IPropertySet LocalSettingsValues => LocalSettings.Values;

		// Folder paths

		public static string LocalFolderPath
			=> HasPackageIdentity ? ApplicationData.Current.LocalFolder.Path : Path.Combine(FallbackRoot, "LocalState");

		public static string RoamingFolderPath
			=> HasPackageIdentity ? ApplicationData.Current.RoamingFolder.Path : Path.Combine(FallbackRoot, "RoamingState");

		public static string TemporaryFolderPath
			=> HasPackageIdentity ? ApplicationData.Current.TemporaryFolder.Path : Path.Combine(FallbackRoot, "TempState");

		public static string LocalCacheFolderPath
			=> HasPackageIdentity ? ApplicationData.Current.LocalCacheFolder.Path : Path.Combine(FallbackRoot, "LocalCache");

		// StorageFolder accessors

		public static Task<StorageFolder> GetLocalFolderAsync()
			=> HasPackageIdentity
				? Task.FromResult(ApplicationData.Current.LocalFolder)
				: GetFallbackFolderAsync(LocalFolderPath);

		public static Task<StorageFolder> GetRoamingFolderAsync()
			=> HasPackageIdentity
				? Task.FromResult(ApplicationData.Current.RoamingFolder)
				: GetFallbackFolderAsync(RoamingFolderPath);

		public static Task<StorageFolder> GetTemporaryFolderAsync()
			=> HasPackageIdentity
				? Task.FromResult(ApplicationData.Current.TemporaryFolder)
				: GetFallbackFolderAsync(TemporaryFolderPath);

		public static Task<StorageFolder> GetLocalCacheFolderAsync()
			=> HasPackageIdentity
				? Task.FromResult(ApplicationData.Current.LocalCacheFolder)
				: GetFallbackFolderAsync(LocalCacheFolderPath);

		private static async Task<StorageFolder> GetFallbackFolderAsync(string path)
		{
			SystemIO.Directory.CreateDirectory(path);
			return await StorageFolder.GetFolderFromPathAsync(path);
		}

		// Package info

		public static string PackageName
			=> HasPackageIdentity ? Package.Current.Id.Name : "FilesDev";

		public static string PackageFamilyName
			=> HasPackageIdentity ? Package.Current.Id.FamilyName : "FilesDev_ykqwq8d6ps0ag";

		public static string DisplayName
			=> HasPackageIdentity ? Package.Current.DisplayName : "Files - Dev";

		public static PackageVersion Version
			=> HasPackageIdentity ? Package.Current.Id.Version : new(4, 2, 37, 0);

		public static string EffectivePath
			=> HasPackageIdentity ? Package.Current.EffectivePath : AppContext.BaseDirectory;

		public static string InstalledLocationPath
			=> HasPackageIdentity ? Package.Current.InstalledLocation.Path : AppContext.BaseDirectory;

		// Languages

		private static string _primaryLanguageOverride = string.Empty;

		/// <summary>
		/// Languages the app ships resources for. Unpackaged, these are the satellite
		/// culture folders next to the executable.
		/// </summary>
		public static IReadOnlyList<string> ManifestLanguages
		{
			get
			{
				if (HasPackageIdentity)
					return ApplicationLanguages.ManifestLanguages;

				var cultures = new HashSet<string>(
					CultureInfo.GetCultures(CultureTypes.AllCultures).Select(c => c.Name),
					StringComparer.OrdinalIgnoreCase);

				return Directory.GetDirectories(AppContext.BaseDirectory)
					.Select(Path.GetFileName)
					.Where(name => !string.IsNullOrEmpty(name) && cultures.Contains(name!))
					.ToArray()!;
			}
		}

		public static string PrimaryLanguageOverride
		{
			get => HasPackageIdentity ? ApplicationLanguages.PrimaryLanguageOverride : _primaryLanguageOverride;
			set
			{
				if (HasPackageIdentity)
					ApplicationLanguages.PrimaryLanguageOverride = value;
				else
					_primaryLanguageOverride = value;
			}
		}
	}

	/// <summary>
	/// Drop-in replacement for <see cref="ApplicationDataContainer"/> limited to the members used by
	/// the app: <see cref="Containers"/>, <see cref="Values"/>, <see cref="CreateContainer"/> and
	/// <see cref="DeleteContainer"/>. Unpackaged instances keep everything in memory.
	/// </summary>
	internal sealed class AppSettingsContainer
	{
		private readonly ApplicationDataContainer? _packaged;
		private readonly PropertySet _values;
		private readonly Dictionary<string, AppSettingsContainer> _containers;

		private AppSettingsContainer(ApplicationDataContainer packaged)
		{
			_packaged = packaged;
			_values = null!;
			_containers = null!;
		}

		public AppSettingsContainer()
		{
			_packaged = null;
			_values = new PropertySet();
			_containers = new(StringComparer.Ordinal);
		}

		private static AppSettingsContainer Create(ApplicationDataContainer? packaged)
			=> packaged is null ? new() : new(packaged);

		public IReadOnlyDictionary<string, AppSettingsContainer> Containers
		{
			get
			{
				if (_packaged is null)
					return _containers;

				return new PackagedContainersView(_packaged);
			}
		}

		public IPropertySet Values => _packaged?.Values ?? _values;

		public AppSettingsContainer CreateContainer(string name, ApplicationDataCreateDisposition disposition)
		{
			if (_packaged is not null)
				return new AppSettingsContainer(_packaged.CreateContainer(name, disposition));

			if (!_containers.TryGetValue(name, out var child))
			{
				child = new AppSettingsContainer();
				_containers[name] = child;
			}

			return child;
		}

		public void DeleteContainer(string name)
		{
			if (_packaged is not null)
				_packaged.DeleteContainer(name);
			else
				_containers.Remove(name);
		}

		public bool ContainsContainer(string name)
		{
			if (_packaged is not null)
				return _packaged.Containers.ContainsKey(name);

			return _containers.ContainsKey(name);
		}

		public bool TryGetContainer(string name, out AppSettingsContainer container)
		{
			if (_packaged is not null)
			{
				if (_packaged.Containers.TryGetValue(name, out var child))
				{
					container = new AppSettingsContainer(child);
					return true;
				}

				container = null!;
				return false;
			}

			return _containers.TryGetValue(name, out container!);
		}

		private sealed class PackagedContainersView : IReadOnlyDictionary<string, AppSettingsContainer>
		{
			private readonly ApplicationDataContainer _container;

			public PackagedContainersView(ApplicationDataContainer container) => _container = container;

			public AppSettingsContainer this[string key]
				=> new AppSettingsContainer(_container.Containers[key]);

			public IEnumerable<string> Keys => _container.Containers.Keys;

			public IEnumerable<AppSettingsContainer> Values
				=> _container.Containers.Values.Select(c => new AppSettingsContainer(c));

			public int Count => _container.Containers.Count;

			public bool ContainsKey(string key) => _container.Containers.ContainsKey(key);

			public IEnumerator<KeyValuePair<string, AppSettingsContainer>> GetEnumerator()
			{
				foreach (var pair in _container.Containers)
					yield return new(pair.Key, new AppSettingsContainer(pair.Value));
			}

			System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();

			public bool TryGetValue(string key, out AppSettingsContainer value)
			{
				if (_container.Containers.TryGetValue(key, out var child))
				{
					value = new AppSettingsContainer(child);
					return true;
				}

				value = null!;
				return false;
			}
		}
	}
}
