namespace MauiAppIntentsSample.Models;

public class TaskItem
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Title { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public TaskPriorityLevel Priority { get; set; } = TaskPriorityLevel.Medium;
    public TaskCategoryType Category { get; set; } = TaskCategoryType.Personal;
    public DateTime? DueDate { get; set; }
    public int? EstimatedMinutes { get; set; }
    public bool IsCompleted { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public enum TaskPriorityLevel
{
    Low = 0,
    Medium = 1,
    High = 2,
    Urgent = 3
}

public enum TaskCategoryType
{
    Work = 0,
    Personal = 1,
    Shopping = 2,
    Health = 3,
    Fitness = 4
}
