using Microsoft.Extensions.Logging;
using MauiAppIntentsSample.Services;
using MauiAppIntentsSample.ViewModels;
using MauiAppIntentsSample.Views;

namespace MauiAppIntentsSample;

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

		// Services
		builder.Services.AddSingleton<ITaskService, TaskService>();
#if IOS
		builder.Services.AddSingleton<IIntentDonationService, Platforms.iOS.IntentDonationService>();
#endif

		// ViewModels
		builder.Services.AddSingleton<TaskListViewModel>();
		builder.Services.AddTransient<TaskDetailViewModel>();

		// Views
		builder.Services.AddSingleton<TaskListPage>();
		builder.Services.AddTransient<TaskDetailPage>();

#if DEBUG
		builder.Logging.AddDebug();
#endif

		return builder.Build();
	}
}
