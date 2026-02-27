using Foundation;
using MauiAppIntentsSample.Binding;
using MauiAppIntentsSample.Models;
using MauiAppIntentsSample.Services;

namespace MauiAppIntentsSample.Platforms.iOS;

/// <summary>
/// Implements the Swift TaskDataProvider protocol in C#.
/// This bridges Swift App Intents to the C# TaskService business logic.
/// Registered as the provider on TaskBridgeManager.Shared during app startup.
/// </summary>
public class AppIntentsBridgeProvider : TaskDataProvider
{
    private readonly ITaskService _taskService;

    public AppIntentsBridgeProvider(ITaskService taskService)
    {
        _taskService = taskService;
    }

    public override BridgeTaskItem[] GetAllTasks()
    {
        return _taskService.GetAll().Select(ToBridge).ToArray();
    }

    public override BridgeTaskItem? GetTask(string id)
    {
        var task = _taskService.GetById(id);
        return task is not null ? ToBridge(task) : null;
    }

    public override BridgeTaskItem? CreateTask(string title, nint priorityRawValue,
        nint categoryRawValue, NSDate? dueDate, nint estimatedMinutes, string notes)
    {
        var created = _taskService.Create(
            title,
            (TaskPriorityLevel)(int)priorityRawValue,
            (TaskCategoryType)(int)categoryRawValue,
            dueDate is not null ? (DateTime)dueDate : null,
            (int)estimatedMinutes >= 0 ? (int)estimatedMinutes : null,
            string.IsNullOrEmpty(notes) ? null : notes
        );

        return ToBridge(created);
    }

    public override bool CompleteTask(string id)
    {
        return _taskService.Complete(id);
    }

    public override BridgeTaskItem[] SearchTasks(string query)
    {
        return _taskService.Search(query).Select(ToBridge).ToArray();
    }

    public override BridgeTaskItem[] GetTasksByFilter(nint categoryRawValue,
        nint priorityRawValue, bool showCompleted)
    {
        TaskCategoryType? category = (int)categoryRawValue >= 0
            ? (TaskCategoryType)(int)categoryRawValue : null;
        TaskPriorityLevel? priority = (int)priorityRawValue >= 0
            ? (TaskPriorityLevel)(int)priorityRawValue : null;

        return _taskService.GetFiltered(category, priority, showCompleted)
            .Select(ToBridge).ToArray();
    }

    public override bool SetDueDate(NSDate date, string taskId)
    {
        return _taskService.SetDueDate(taskId, (DateTime)date);
    }

    /// <summary>
    /// Converts a C# TaskItem to a Swift-compatible BridgeTaskItem.
    /// </summary>
    private static BridgeTaskItem ToBridge(TaskItem task)
    {
        return new BridgeTaskItem(
            id: task.Id,
            title: task.Title,
            notes: task.Notes ?? "",
            priorityRawValue: (nint)(int)task.Priority,
            categoryRawValue: (nint)(int)task.Category,
            dueDate: task.DueDate.HasValue ? (NSDate)task.DueDate.Value : null,
            estimatedMinutes: task.EstimatedMinutes.HasValue ? (nint)task.EstimatedMinutes.Value : -1,
            isCompleted: task.IsCompleted,
            createdAt: (NSDate)task.CreatedAt
        );
    }
}
