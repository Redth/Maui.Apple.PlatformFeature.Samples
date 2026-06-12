using Maui.AppIntents;

namespace MauiAppIntentsSample.Models;

[AppEntity("TaskItem", TypeDisplayName = "Task", Indexed = true,
    UrlRepresentation = "tasktracker://task/{id}?details={Details}")]
public class TaskItem
{
    [AppEntityIdentifier]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [AppEntityDisplay]
    public string Title { get; set; } = string.Empty;

    [AppEntitySubtitle]
    public string? Notes { get; set; }

    [AppEntityProperty("Details", IndexingKey = "contentDescription")]
    public string Details { get; set; } = string.Empty;

    [AppEntityProperty("Priority")]
    public TaskPriorityLevel Priority { get; set; } = TaskPriorityLevel.Medium;

    [AppEntityProperty("Category")]
    public TaskCategoryType Category { get; set; } = TaskCategoryType.Personal;

    [AppEntityProperty("Due Date")]
    public DateTime? DueDate { get; set; }

    [AppEntityProperty("Estimated Minutes")]
    public int? EstimatedMinutes { get; set; }

    [AppEntityProperty("Completed")]
    public bool IsCompleted { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

[AppEnum("Task Priority", UrlRepresentation = "tasktracker://priority")]
public enum TaskPriorityLevel
{
    [AppEnumCase("Low")]
    Low = 0,

    [AppEnumCase("Medium")]
    Medium = 1,

    [AppEnumCase("High")]
    High = 2,

    [AppEnumCase("Urgent")]
    Urgent = 3
}

[AppEnum("Task Category")]
public enum TaskCategoryType
{
    [AppEnumCase("Work")]
    Work = 0,

    [AppEnumCase("Personal")]
    Personal = 1,

    [AppEnumCase("Shopping")]
    Shopping = 2,

    [AppEnumCase("Health")]
    Health = 3,

    [AppEnumCase("Fitness")]
    Fitness = 4
}
