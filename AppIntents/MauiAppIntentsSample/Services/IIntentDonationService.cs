using MauiAppIntentsSample.Models;

namespace MauiAppIntentsSample.Services;

/// <summary>
/// Service for donating App Intents to the system when users perform actions in the UI.
/// This helps iOS learn user patterns and proactively suggest shortcuts.
/// </summary>
public interface IIntentDonationService
{
    void DonateCreateTask(TaskItem task);
    void DonateCompleteTask(TaskItem task);
    void DonateOpenTask(TaskItem task);
    void DonateSetDueDate(TaskItem task);
    void DonateSearchTasks(string query);
    void DeleteTaskDonations(string taskId);
}
