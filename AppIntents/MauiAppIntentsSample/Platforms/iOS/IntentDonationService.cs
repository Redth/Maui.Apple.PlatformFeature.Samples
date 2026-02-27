using Foundation;
using MauiAppIntentsSample.Binding;
using MauiAppIntentsSample.Models;
using MauiAppIntentsSample.Services;

namespace MauiAppIntentsSample.Platforms.iOS;

/// <summary>
/// iOS implementation that donates App Intents via the Swift IntentDonationBridge.
/// Donations are fire-and-forget — failures are logged but never crash the app.
/// </summary>
public class IntentDonationService : IIntentDonationService
{
    public void DonateCreateTask(TaskItem task)
    {
        try
        {
            IntentDonationBridge.Shared.DonateCreateTask(
                task.Title,
                (nint)(int)task.Priority,
                (nint)(int)task.Category,
                task.DueDate.HasValue ? (NSDate)task.DueDate.Value : null,
                task.EstimatedMinutes.HasValue ? (nint)task.EstimatedMinutes.Value : -1,
                task.Notes ?? "");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[AppIntents] Failed to donate CreateTask: {ex.Message}");
        }
    }

    public void DonateCompleteTask(TaskItem task)
    {
        try
        {
            IntentDonationBridge.Shared.DonateCompleteTask(task.Id, task.Title);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[AppIntents] Failed to donate CompleteTask: {ex.Message}");
        }
    }

    public void DonateOpenTask(TaskItem task)
    {
        try
        {
            IntentDonationBridge.Shared.DonateOpenTask(task.Id, task.Title);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[AppIntents] Failed to donate OpenTask: {ex.Message}");
        }
    }

    public void DonateSetDueDate(TaskItem task)
    {
        try
        {
            if (task.DueDate.HasValue)
            {
                IntentDonationBridge.Shared.DonateSetDueDate(
                    (NSDate)task.DueDate.Value, task.Id, task.Title);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[AppIntents] Failed to donate SetDueDate: {ex.Message}");
        }
    }

    public void DonateSearchTasks(string query)
    {
        try
        {
            IntentDonationBridge.Shared.DonateSearchTasks(query);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[AppIntents] Failed to donate SearchTasks: {ex.Message}");
        }
    }

    public void DeleteTaskDonations(string taskId)
    {
        try
        {
            IntentDonationBridge.Shared.DeleteTaskDonations(taskId);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[AppIntents] Failed to delete donations: {ex.Message}");
        }
    }
}
