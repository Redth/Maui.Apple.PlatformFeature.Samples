using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MauiAppIntentsSample.Models;
using MauiAppIntentsSample.Services;

namespace MauiAppIntentsSample.ViewModels;

[QueryProperty(nameof(Task), "Task")]
public partial class TaskDetailViewModel : ObservableObject
{
    private readonly ITaskService _taskService;
    private readonly IIntentDonationService? _donationService;

    [ObservableProperty]
    private TaskItem? _task;

    [ObservableProperty]
    private string _title = string.Empty;

    [ObservableProperty]
    private string _notes = string.Empty;

    [ObservableProperty]
    private TaskPriorityLevel _priority = TaskPriorityLevel.Medium;

    [ObservableProperty]
    private TaskCategoryType _category = TaskCategoryType.Personal;

    [ObservableProperty]
    private DateTime? _dueDate;

    [ObservableProperty]
    private int? _estimatedMinutes;

    [ObservableProperty]
    private bool _isEditing;

    public Array PriorityValues => Enum.GetValues<TaskPriorityLevel>();
    public Array CategoryValues => Enum.GetValues<TaskCategoryType>();

    public TaskDetailViewModel(ITaskService taskService, IIntentDonationService? donationService = null)
    {
        _taskService = taskService;
        _donationService = donationService;
    }

    partial void OnTaskChanged(TaskItem? value)
    {
        if (value is not null)
        {
            Title = value.Title;
            Notes = value.Notes ?? string.Empty;
            Priority = value.Priority;
            Category = value.Category;
            DueDate = value.DueDate;
            EstimatedMinutes = value.EstimatedMinutes;
            IsEditing = true;
        }
        else
        {
            IsEditing = false;
        }
    }

    [RelayCommand]
    private async Task Save()
    {
        if (string.IsNullOrWhiteSpace(Title))
        {
            await Shell.Current.DisplayAlert("Error", "Title is required.", "OK");
            return;
        }

        if (IsEditing && Task is not null)
        {
            Task.Title = Title;
            Task.Notes = string.IsNullOrWhiteSpace(Notes) ? null : Notes;
            Task.Priority = Priority;
            Task.Category = Category;
            Task.DueDate = DueDate;
            Task.EstimatedMinutes = EstimatedMinutes;
        }
        else
        {
            var created = _taskService.Create(Title, Priority, Category, DueDate, EstimatedMinutes,
                                string.IsNullOrWhiteSpace(Notes) ? null : Notes);
            _donationService?.DonateCreateTask(created);
        }

        await Shell.Current.GoToAsync("..");
    }

    [RelayCommand]
    private async Task Cancel()
    {
        await Shell.Current.GoToAsync("..");
    }
}
