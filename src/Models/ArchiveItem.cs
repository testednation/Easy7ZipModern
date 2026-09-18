using System;

namespace Easy7ZipModern.Models;

public class ArchiveItem
{
	public string Name { get; set; }

	public string Path { get; set; }

	public long Size { get; set; }

	public long CompressedSize { get; set; }

	public DateTime? Modified { get; set; }

	public string Attributes { get; set; }

	public bool IsDirectory { get; set; }

	public bool IsEncrypted { get; set; }

	public string Extension { get; set; }

	public string DisplaySize
	{
		get
		{
			if (IsDirectory)
			{
				return "";
			}
			return FormatSize(Size);
		}
	}

	public string DisplayCompressedSize
	{
		get
		{
			if (IsDirectory)
			{
				return "";
			}
			return FormatSize(CompressedSize);
		}
	}

	public string DisplayModified
	{
		get
		{
			if (!Modified.HasValue)
			{
				return "";
			}
			return Modified.Value.ToString("yyyy-MM-dd HH:mm:ss");
		}
	}

	public string TypeDescription
	{
		get
		{
			if (IsDirectory)
			{
				return "Folder";
			}
			if (string.IsNullOrEmpty(Extension))
			{
				return "File";
			}
			return Extension.TrimStart('.').ToUpperInvariant() + " File";
		}
	}

	public string IconKind
	{
		get
		{
			if (IsDirectory)
			{
				return "Folder";
			}
			switch ((Extension ?? "").ToLowerInvariant())
			{
			case ".zip":
			case ".7z":
			case ".rar":
			case ".tar":
			case ".gz":
			case ".bz2":
			case ".xz":
			case ".zst":
				return "Archive";
			case ".exe":
			case ".msi":
			case ".msp":
				return "Executable";
			case ".txt":
			case ".log":
			case ".md":
			case ".ini":
			case ".cfg":
				return "Document";
			case ".png":
			case ".jpg":
			case ".jpeg":
			case ".bmp":
			case ".gif":
			case ".ico":
				return "Image";
			case ".dll":
			case ".sys":
				return "Binary";
			default:
				return "File";
			}
		}
	}

	public static string FormatSize(long bytes)
	{
		if (bytes < 0)
		{
			return "0 B";
		}
		if (bytes < 1024)
		{
			return bytes + " B";
		}
		if (bytes < 1048576)
		{
			return ((double)bytes / 1024.0).ToString("F1") + " KB";
		}
		if (bytes < 1073741824)
		{
			return ((double)bytes / 1048576.0).ToString("F2") + " MB";
		}
		return ((double)bytes / 1073741824.0).ToString("F2") + " GB";
	}
}
