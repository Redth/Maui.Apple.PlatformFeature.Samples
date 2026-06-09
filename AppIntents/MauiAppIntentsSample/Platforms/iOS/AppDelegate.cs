using Foundation;
using Maui.AppIntents;
using UIKit;
using MauiAppIntentsSample.Binding;
using MauiAppIntentsSample.Platforms.iOS;
using MauiAppIntentsSample.Services;

namespace MauiAppIntentsSample;

[Register("AppDelegate")]
public class AppDelegate : MauiUIApplicationDelegate
{
	protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();

	public override bool FinishedLaunching(UIApplication application, NSDictionary? launchOptions)
	{
		var result = base.FinishedLaunching(application, launchOptions);

		// Wire up the Swift App Intents bridge so Siri can access C# business logic
		WireUpAppIntentsBridge();
#if MAUI_APPINTENTS
		WireUpGeneratedAppIntentsBridge();
#endif

		return result;
	}

	private void WireUpAppIntentsBridge()
	{
		try
		{
			var taskService = IPlatformApplication.Current?.Services.GetService<ITaskService>();
			if (taskService is not null)
			{
				var bridgeProvider = new AppIntentsBridgeProvider(taskService);
				TaskBridgeManager.Shared.Provider = bridgeProvider;
				Console.WriteLine("[AppIntents] Bridge wired up successfully.");
			}
			else
			{
				Console.WriteLine("[AppIntents] WARNING: ITaskService not found in DI container.");
			}
		}
		catch (Exception ex)
		{
			Console.WriteLine($"[AppIntents] ERROR wiring bridge: {ex.Message}");
		}
	}

#if MAUI_APPINTENTS
	private void WireUpGeneratedAppIntentsBridge()
	{
		try
		{
			var services = IPlatformApplication.Current?.Services;
			if (services is null)
			{
				Console.WriteLine("[AppIntents] WARNING: MAUI service provider not found for generated bridge.");
				return;
			}

			MauiAppIntentsNative.WireUp(services);
			Console.WriteLine("[AppIntents] Generated bridge wired up successfully.");
		}
		catch (Exception ex)
		{
			Console.WriteLine($"[AppIntents] ERROR wiring generated bridge: {ex}");
		}
	}
#endif
}
