using MauiAppIntentsSample.ViewModels;

namespace MauiAppIntentsSample.Views;

public partial class TaskDetailPage : ContentPage
{
    public TaskDetailPage(TaskDetailViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
