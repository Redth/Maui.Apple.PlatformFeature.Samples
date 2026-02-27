using MauiAppIntentsSample.Models;

namespace MauiAppIntentsSample.Services;

public interface ITaskService
{
    IReadOnlyList<TaskItem> GetAll();
    TaskItem? GetById(string id);
    TaskItem Create(string title, TaskPriorityLevel priority, TaskCategoryType category,
                    DateTime? dueDate = null, int? estimatedMinutes = null, string? notes = null);
    bool Complete(string id);
    bool SetDueDate(string id, DateTime dueDate);
    bool Delete(string id);
    IReadOnlyList<TaskItem> Search(string query);
    IReadOnlyList<TaskItem> GetFiltered(TaskCategoryType? category = null,
                                        TaskPriorityLevel? priority = null,
                                        bool showCompleted = false);
    event EventHandler? TasksChanged;
}
