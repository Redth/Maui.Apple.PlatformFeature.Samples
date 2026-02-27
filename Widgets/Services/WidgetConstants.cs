namespace MauiAppleWidgets.Services;

/// <summary>
/// Shared constants for widget communication. Keep these in sync with Settings.swift on the widget side.
/// </summary>
public static class WidgetConstants
{
	/// <summary>App Group ID — must match the App Group configured in Apple Developer Console and Entitlements.</summary>
	public const string GroupId = "group.com.mauiapplewidgets.app";

	/// <summary>Filename for data sent FROM the app TO the widget (in the shared container).</summary>
	public const string FromAppFile = "widget_data_fromapp.json";

	/// <summary>Filename for data sent FROM the widget TO the app (in the shared container).</summary>
	public const string FromWidgetFile = "widget_data_fromwidget.json";

	/// <summary>Custom URL scheme for deep linking from widget to app.</summary>
	public const string UrlScheme = "mauiapplewidgets";

	/// <summary>URL host for widget deep links.</summary>
	public const string UrlHost = "widget";

	/// <summary>The widget kind identifier — must match the `kind` property in SimpleWidget.swift.</summary>
	public const string WidgetKind = "SimpleWidget";
}
