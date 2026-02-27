using MauiAppIntentsSample.Models;

namespace MauiAppIntentsSample.Services;

public class TaskService : ITaskService
{
    private readonly List<TaskItem> _tasks = [];
    private readonly object _lock = new();

    public event EventHandler? TasksChanged;

    public TaskService()
    {
        SeedSampleData();
    }

    public IReadOnlyList<TaskItem> GetAll()
    {
        lock (_lock)
        {
            return _tasks.OrderByDescending(t => t.CreatedAt).ToList();
        }
    }

    public TaskItem? GetById(string id)
    {
        lock (_lock)
        {
            return _tasks.FirstOrDefault(t => t.Id == id);
        }
    }

    public TaskItem Create(string title, TaskPriorityLevel priority, TaskCategoryType category,
                           DateTime? dueDate = null, int? estimatedMinutes = null, string? notes = null)
    {
        var task = new TaskItem
        {
            Title = title,
            Priority = priority,
            Category = category,
            DueDate = dueDate,
            EstimatedMinutes = estimatedMinutes,
            Notes = notes
        };

        lock (_lock)
        {
            _tasks.Add(task);
        }

        OnTasksChanged();
        return task;
    }

    public bool Complete(string id)
    {
        lock (_lock)
        {
            var task = _tasks.FirstOrDefault(t => t.Id == id);
            if (task is null) return false;
            task.IsCompleted = true;
            OnTasksChanged();
            return true;
        }
    }

    public bool SetDueDate(string id, DateTime dueDate)
    {
        lock (_lock)
        {
            var task = _tasks.FirstOrDefault(t => t.Id == id);
            if (task is null) return false;
            task.DueDate = dueDate;
            OnTasksChanged();
            return true;
        }
    }

    public bool Delete(string id)
    {
        lock (_lock)
        {
            var task = _tasks.FirstOrDefault(t => t.Id == id);
            if (task is null) return false;
            _tasks.Remove(task);
            OnTasksChanged();
            return true;
        }
    }

    public IReadOnlyList<TaskItem> Search(string query)
    {
        lock (_lock)
        {
            var lowerQuery = query.ToLowerInvariant();
            return _tasks
                .Where(t => t.Title.Contains(lowerQuery, StringComparison.OrdinalIgnoreCase)
                         || (t.Notes?.Contains(lowerQuery, StringComparison.OrdinalIgnoreCase) ?? false))
                .OrderByDescending(t => t.CreatedAt)
                .ToList();
        }
    }

    public IReadOnlyList<TaskItem> GetFiltered(TaskCategoryType? category = null,
                                                TaskPriorityLevel? priority = null,
                                                bool showCompleted = false)
    {
        lock (_lock)
        {
            var query = _tasks.AsEnumerable();
            if (category.HasValue)
                query = query.Where(t => t.Category == category.Value);
            if (priority.HasValue)
                query = query.Where(t => t.Priority == priority.Value);
            if (!showCompleted)
                query = query.Where(t => !t.IsCompleted);
            return query.OrderByDescending(t => t.CreatedAt).ToList();
        }
    }

    private void OnTasksChanged() => TasksChanged?.Invoke(this, EventArgs.Empty);

    private void SeedSampleData()
    {
        _tasks.AddRange([
            new TaskItem { Title = "Review quarterly report", Priority = TaskPriorityLevel.High, Category = TaskCategoryType.Work, DueDate = DateTime.Today.AddDays(2), EstimatedMinutes = 60 },
            new TaskItem { Title = "Buy groceries", Priority = TaskPriorityLevel.Medium, Category = TaskCategoryType.Shopping, Notes = "Milk, eggs, bread, vegetables" },
            new TaskItem { Title = "Morning run", Priority = TaskPriorityLevel.Low, Category = TaskCategoryType.Fitness, EstimatedMinutes = 30 },
            new TaskItem { Title = "Schedule dentist appointment", Priority = TaskPriorityLevel.Medium, Category = TaskCategoryType.Health, DueDate = DateTime.Today.AddDays(7) },
            new TaskItem { Title = "Fix login page bug", Priority = TaskPriorityLevel.Urgent, Category = TaskCategoryType.Work, DueDate = DateTime.Today.AddDays(1), EstimatedMinutes = 120, Notes = "Users reporting 500 error on iOS Safari" },
        ]);
    }
}
