import Foundation
import AppIntents

/// Bridge that allows C# to donate App Intents to the system when users perform actions in the MAUI UI.
/// The system uses donations to learn user patterns and proactively suggest intents.
@objc(IntentDonationBridge)
public class IntentDonationBridge: NSObject {
    @objc public static let shared = IntentDonationBridge()

    private override init() {
        super.init()
    }

    // MARK: - Donate Intents

    /// Donates a CreateTask intent when the user creates a task in the MAUI UI.
    @objc public func donateCreateTask(
        title: String,
        priorityRawValue: Int,
        categoryRawValue: Int,
        dueDate: Date?,
        estimatedMinutes: Int,
        notes: String
    ) {
        let intent = CreateTaskIntent()
        intent.$taskTitle.wrappedValue = title
        intent.$priority.wrappedValue = TaskPriority(rawValue: priorityRawValue) ?? .medium
        intent.$category.wrappedValue = TaskCategory(rawValue: categoryRawValue) ?? .personal
        intent.$dueDate.wrappedValue = dueDate
        intent.$estimatedMinutes.wrappedValue = estimatedMinutes >= 0 ? estimatedMinutes : nil
        intent.$notes.wrappedValue = notes.isEmpty ? nil : notes
        donate(intent)
    }

    /// Donates a CompleteTask intent when the user completes a task in the MAUI UI.
    @objc public func donateCompleteTask(taskId: String, taskTitle: String) {
        let intent = CompleteTaskIntent()
        let bridgeItem = BridgeTaskItem(
            id: taskId, title: taskTitle, notes: "",
            priorityRawValue: 0, categoryRawValue: 0,
            dueDate: nil, estimatedMinutes: -1, isCompleted: false, createdAt: Date()
        )
        intent.$target.wrappedValue = TaskEntity(from: bridgeItem)
        donate(intent)
    }

    /// Donates an OpenTask intent when the user opens/views a task in the MAUI UI.
    @objc public func donateOpenTask(taskId: String, taskTitle: String) {
        let intent = OpenTaskIntent()
        let bridgeItem = BridgeTaskItem(
            id: taskId, title: taskTitle, notes: "",
            priorityRawValue: 0, categoryRawValue: 0,
            dueDate: nil, estimatedMinutes: -1, isCompleted: false, createdAt: Date()
        )
        intent.$target.wrappedValue = TaskEntity(from: bridgeItem)
        donate(intent)
    }

    /// Donates a SetDueDate intent when the user sets a due date in the MAUI UI.
    @objc public func donateSetDueDate(_ date: Date, taskId: String, taskTitle: String) {
        let intent = SetDueDateIntent()
        let bridgeItem = BridgeTaskItem(
            id: taskId, title: taskTitle, notes: "",
            priorityRawValue: 0, categoryRawValue: 0,
            dueDate: nil, estimatedMinutes: -1, isCompleted: false, createdAt: Date()
        )
        intent.$target.wrappedValue = TaskEntity(from: bridgeItem)
        intent.$dueDate.wrappedValue = date
        donate(intent)
    }

    /// Donates a SearchTasks intent when the user searches in the MAUI UI.
    @objc public func donateSearchTasks(query: String) {
        let intent = SearchTasksIntent()
        intent.$query.wrappedValue = query
        donate(intent)
    }

    // MARK: - Delete Donations

    /// Deletes donations for intents that reference a specific task.
    /// Call this when a task is deleted from the app.
    @objc public func deleteTaskDonations(taskId: String) {
        // Delete donations for each intent type that takes a TaskEntity parameter
        Task {
            do {
                _ = try await IntentDonationManager.shared.deleteDonations(
                    matching: .intentType(CompleteTaskIntent.self)
                )
                _ = try await IntentDonationManager.shared.deleteDonations(
                    matching: .intentType(OpenTaskIntent.self)
                )
                _ = try await IntentDonationManager.shared.deleteDonations(
                    matching: .intentType(SetDueDateIntent.self)
                )
                print("[AppIntents] Cleaned up donations after task deletion")
            } catch {
                print("[AppIntents] Failed to delete donations: \(error)")
            }
        }
    }

    // MARK: - Private

    private func donate<T: AppIntent>(_ intent: T) {
        Task {
            do {
                _ = try await IntentDonationManager.shared.donate(intent: intent)
                print("[AppIntents] Donated \(T.title) intent")
            } catch {
                print("[AppIntents] Failed to donate \(T.title): \(error)")
            }
        }
    }
}
