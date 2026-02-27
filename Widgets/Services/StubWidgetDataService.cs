namespace MauiAppleWidgets.Services;

/// <summary>
/// No-op implementation for platforms that don't support iOS widgets (Android, Windows, etc.).
/// </summary>
public class StubWidgetDataService : IWidgetDataService
{
	public void SendDataToWidget(WidgetData data) { }
	public WidgetData? ReadDataFromWidget() => null;
	public void ClearWidgetIncomingData() { }
	public void RefreshWidget(string kind = "SimpleWidget") { }
}
