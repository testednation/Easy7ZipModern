using System;
using System.IO;
using System.Web.Script.Serialization;

namespace Easy7ZipModern.Models;

public class AppSettings
{
	public bool IsDarkMode { get; set; }

	/// <summary>Which UI palette to use: Dark, Light, Midnight or High Contrast.</summary>
	public string ThemeName { get; set; }

	public bool EnableDoubleClickQuickExtract { get; set; }

	public bool RememberPasswords { get; set; }

	public bool AutoTrySavedPasswords { get; set; }

	public bool OpenFolderAfterExtraction { get; set; }

	public bool DeleteSourceAfterExtraction { get; set; }

	public string OverwriteMode { get; set; }

	/// <summary>UI zoom factor (1.0 = 100%). Adjusted with Ctrl +/-/0, Ctrl+mouse
	/// wheel, or the −/%/+ buttons in the title bar. Range 0.5–2.0.</summary>
	public double UiScale { get; set; }

	public bool EnableContextMenu { get; set; }
	public bool ContextMenuCascaded { get; set; }
	public bool ContextMenuIcons { get; set; }
	public bool ContextMenuOpen { get; set; }
	public bool ContextMenuExtractFiles { get; set; }
	public bool ContextMenuExtractHere { get; set; }
	public bool ContextMenuExtractTo { get; set; }
	public bool ContextMenuTest { get; set; }
	public bool ContextMenuAdd { get; set; }
	public bool ContextMenuAdd7z { get; set; }
	public bool ContextMenuAddZip { get; set; }
	public bool ContextMenuCrcSha { get; set; }
	public bool ContextMenuScanFileType { get; set; }


	private static string SettingsFilePath
	{
		get
		{
			string folderPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
			string text = Path.Combine(folderPath, "Easy7ZipModern");
			if (!Directory.Exists(text))
			{
				Directory.CreateDirectory(text);
			}
			return Path.Combine(text, "settings.json");
		}
	}

	public AppSettings()
	{
		IsDarkMode = true;
		ThemeName = "Dark";
		EnableDoubleClickQuickExtract = true;
		RememberPasswords = true;
		AutoTrySavedPasswords = true;
		OpenFolderAfterExtraction = true;
		DeleteSourceAfterExtraction = false;
		OverwriteMode = "Overwrite";
		UiScale = 1.0;

		EnableContextMenu = true;
		ContextMenuCascaded = false; // Default to main context menu for maximum Explorer compatibility
		ContextMenuIcons = true;
		ContextMenuOpen = true;
		ContextMenuExtractFiles = true;
		ContextMenuExtractHere = true;
		ContextMenuExtractTo = true;
		ContextMenuTest = true;
		ContextMenuAdd = true;
		ContextMenuAdd7z = true;
		ContextMenuAddZip = true;
		ContextMenuCrcSha = true;
		ContextMenuScanFileType = true;
	}

	public static AppSettings Load()
	{
		try
		{
			if (File.Exists(SettingsFilePath))
			{
				string input = File.ReadAllText(SettingsFilePath);
				JavaScriptSerializer javaScriptSerializer = new JavaScriptSerializer();
				return javaScriptSerializer.Deserialize<AppSettings>(input) ?? new AppSettings();
			}
		}
		catch
		{
		}
		return new AppSettings();
	}

	public void Save()
	{
		try
		{
			JavaScriptSerializer javaScriptSerializer = new JavaScriptSerializer();
			string contents = javaScriptSerializer.Serialize(this);
			File.WriteAllText(SettingsFilePath, contents);
		}
		catch
		{
		}
	}
}
