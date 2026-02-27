namespace MauiAppleWidgets.Services;

/// <summary>
/// Abstraction for bidirectional communication between the .NET MAUI app and the iOS Widget.
/// </summary>
public interface IWidgetDataService
{
	/// <summary>
	/// Write data from the app for the widget to consume.
	/// Serializes WidgetData as JSON into the App Group shared container.
	/// </summary>
	void SendDataToWidget(WidgetData data);

	/// <summary>
	/// Read data written by the widget (e.g., from interactive AppIntent buttons).
	/// Returns null if no incoming data is available.
	/// </summary>
	WidgetData? ReadDataFromWidget();

	/// <summary>
	/// Clear the incoming data key after it has been processed by the app.
	/// </summary>
	void ClearWidgetIncomingData();

	/// <summary>
	/// Signal the OS to reload the widget's timeline, causing it to re-read shared data.
	/// </summary>
	void RefreshWidget(string kind = "SimpleWidget");
}
