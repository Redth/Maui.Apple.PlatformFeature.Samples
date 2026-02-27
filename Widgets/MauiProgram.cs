using Microsoft.Extensions.Logging;
using MauiAppleWidgets.Services;

namespace MauiAppleWidgets;

public static class MauiProgram
{
	public static MauiApp CreateMauiApp()
	{
		var builder = MauiApp.CreateBuilder();
		builder
			.UseMauiApp<App>()
			.ConfigureFonts(fonts =>
			{
				fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
				fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
			});

#if IOS
		builder.Services.AddSingleton<IWidgetDataService, Platforms.iOS.WidgetDataService>();
#else
		builder.Services.AddSingleton<IWidgetDataService, StubWidgetDataService>();
#endif
		builder.Services.AddTransient<MainPage>();

#if DEBUG
		builder.Logging.AddDebug();
#endif

		return builder.Build();
	}
}
