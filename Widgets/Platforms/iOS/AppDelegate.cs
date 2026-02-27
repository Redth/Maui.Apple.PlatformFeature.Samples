using Foundation;
using MauiAppleWidgets.Services;
using UIKit;

namespace MauiAppleWidgets;

[Register("AppDelegate")]
public class AppDelegate : MauiUIApplicationDelegate
{
	protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();

	public override bool OpenUrl(UIApplication application, NSUrl url, NSDictionary options)
	{
		if (url.Scheme == WidgetConstants.UrlScheme)
		{
			var uri = new Uri(url.AbsoluteString!);
			App.HandleWidgetUrl(uri);
			return true;
		}

		return base.OpenUrl(application, url, options);
	}
}
