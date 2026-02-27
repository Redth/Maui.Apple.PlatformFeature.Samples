using MauiAppIntentsSample.ViewModels;

namespace MauiAppIntentsSample.Views;

public partial class TaskListPage : ContentPage
{
    public TaskListPage(TaskListViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
