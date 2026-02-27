using MauiAppleWidgets.Services;

namespace MauiAppleWidgets;

public partial class App : Application
{
	public App()
	{
		InitializeComponent();
	}

	protected override Window CreateWindow(IActivationState? activationState)
	{
		var window = new Window(new AppShell());

		window.Resumed += (s, e) =>
		{
			if (window.Page is AppShell { CurrentPage: MainPage mainPage })
			{
				mainPage.OnResumed();
			}
		};

		return window;
	}

	/// <summary>
	/// Handle deep links from the widget (e.g., mauiapplewidgets://widget?counter=5).
	/// </summary>
	internal static void HandleWidgetUrl(Uri uri)
	{
		if (uri is not { Scheme: WidgetConstants.UrlScheme, Host: WidgetConstants.UrlHost })
			return;

		var query = System.Web.HttpUtility.ParseQueryString(uri.Query);
		var counterValue = query["counter"];

		if (!string.IsNullOrEmpty(counterValue) && int.TryParse(counterValue, out var count))
		{
			NotifyMainPageOfIncomingUrl(count);
		}
	}

	private static void NotifyMainPageOfIncomingUrl(int counterValue)
	{
		var app = Current;
		app?.Dispatcher.Dispatch(() =>
		{
			if (app?.Windows?.Count > 0 &&
				app.Windows[0].Page is AppShell { CurrentPage: MainPage mainPage })
			{
				mainPage.OnResumedByUrl(counterValue);
			}
		});
	}
}