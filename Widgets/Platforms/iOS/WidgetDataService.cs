using System.Text.Json;
using Foundation;
using MauiAppleWidgets.Services;

namespace MauiAppleWidgets.Platforms.iOS;

/// <summary>
/// iOS implementation of IWidgetDataService using file-based I/O via App Group container.
/// UserDefaults(suiteName:) can resolve to different paths for the app vs widget extension,
/// so we write JSON files directly to the shared container directory for reliable cross-process communication.
/// </summary>
public class WidgetDataService : IWidgetDataService
{
	private static readonly JsonSerializerOptions JsonOptions = new()
	{
		PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
		WriteIndented = false
	};

	private string? GetSharedContainerPath()
	{
		var url = NSFileManager.DefaultManager.GetContainerUrl(WidgetConstants.GroupId);
		return url?.Path;
	}

	private string? GetFilePath(string filename)
	{
		var container = GetSharedContainerPath();
		if (container == null) return null;
		return Path.Combine(container, filename);
	}

	public void SendDataToWidget(WidgetData data)
	{
		var path = GetFilePath(WidgetConstants.FromAppFile);
		if (path == null) return;

		var json = JsonSerializer.Serialize(data, JsonOptions);
		File.WriteAllText(path, json);
	}

	public WidgetData? ReadDataFromWidget()
	{
		var path = GetFilePath(WidgetConstants.FromWidgetFile);
		if (path == null || !File.Exists(path)) return null;

		try
		{
			var json = File.ReadAllText(path);
			return JsonSerializer.Deserialize<WidgetData>(json, JsonOptions);
		}
		catch
		{
			return null;
		}
	}

	public void ClearWidgetIncomingData()
	{
		var path = GetFilePath(WidgetConstants.FromWidgetFile);
		if (path != null && File.Exists(path))
			File.Delete(path);
	}

	public void RefreshWidget(string kind = "SimpleWidget")
	{
		try
		{
			var proxy = new WidgetKit.WidgetCenterProxy();
			proxy.ReloadTimeLinesOfKind(kind);
		}
		catch (Exception ex)
		{
			System.Diagnostics.Debug.WriteLine($"[WidgetDataService] ReloadTimeLinesOfKind failed: {ex}");
		}
	}
}
