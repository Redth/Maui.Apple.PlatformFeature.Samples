using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MauiAppIntentsSample.Models;
using MauiAppIntentsSample.Services;

namespace MauiAppIntentsSample.ViewModels;

public partial class TaskListViewModel : ObservableObject
{
    private readonly ITaskService _taskService;
    private readonly IIntentDonationService? _donationService;

    [ObservableProperty]
    private ObservableCollection<TaskItem> _tasks = [];

    public TaskListViewModel(ITaskService taskService, IIntentDonationService? donationService = null)
    {
        _taskService = taskService;
        _donationService = donationService;
        _taskService.TasksChanged += (_, _) => MainThread.BeginInvokeOnMainThread(LoadTasks);
        LoadTasks();
    }

    [RelayCommand]
    private async Task GoToDetail(TaskItem? task)
    {
        var navParam = new Dictionary<string, object>();
        if (task is not null)
        {
            navParam["Task"] = task;
            _donationService?.DonateOpenTask(task);
        }
        await Shell.Current.GoToAsync("detail", navParam);
    }

    [RelayCommand]
    private async Task AddTask()
    {
        await Shell.Current.GoToAsync("detail");
    }

    [RelayCommand]
    private void ToggleComplete(TaskItem task)
    {
        _taskService.Complete(task.Id);
        _donationService?.DonateCompleteTask(task);
    }

    [RelayCommand]
    private void Refresh()
    {
        LoadTasks();
    }

    private void LoadTasks()
    {
        Tasks = new ObservableCollection<TaskItem>(_taskService.GetAll());
    }
}
