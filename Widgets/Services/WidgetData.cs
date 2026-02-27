using System.Text.Json.Serialization;

namespace MauiAppleWidgets.Services;

/// <summary>
/// Shared data contract between the .NET MAUI app and the iOS Widget Extension.
/// Both sides serialize/deserialize this as JSON via the App Group shared container.
/// </summary>
public record WidgetData
{
	[JsonPropertyName("version")]
	public int Version { get; init; } = 1;

	[JsonPropertyName("title")]
	public string Title { get; init; } = "";

	[JsonPropertyName("message")]
	public string Message { get; init; } = "";

	[JsonPropertyName("counter")]
	public int Counter { get; init; }

	[JsonPropertyName("updatedAt")]
	public string UpdatedAt { get; init; } = DateTime.UtcNow.ToString("o");

	[JsonPropertyName("extras")]
	public Dictionary<string, string> Extras { get; init; } = new();
}
