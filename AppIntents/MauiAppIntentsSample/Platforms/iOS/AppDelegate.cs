using Foundation;
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
}
