using System;

namespace Easy7ZipModern.Services;

public class ExtractionResult
{
	public bool Success { get; set; }

	public string OutputDirectory { get; set; }

	public string EngineUsed { get; set; }

	public string ErrorMessage { get; set; }

	public int FileCount { get; set; }

	public TimeSpan Duration { get; set; }
}
