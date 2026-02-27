using MauiAppIntentsSample.Views;

namespace MauiAppIntentsSample;

public partial class AppShell : Shell
{
	public AppShell()
	{
		InitializeComponent();
		Routing.RegisterRoute("detail", typeof(TaskDetailPage));
	}
}
