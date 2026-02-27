import Foundation

/// Protocol that the C# side implements to provide task data to Swift App Intents.
/// All methods must be @objc compatible for .NET iOS binding interop.
@objc(TaskDataProvider) public protocol TaskDataProvider: AnyObject {
    /// Returns all tasks.
    func getAllTasks() -> [BridgeTaskItem]

    /// Returns a single task by ID, or nil if not found.
    func getTask(withId id: String) -> BridgeTaskItem?

    /// Creates a new task and returns it.
    /// estimatedMinutes: pass -1 for nil. notes: pass empty string for nil.
    func createTask(
        title: String,
        priorityRawValue: Int,
        categoryRawValue: Int,
        dueDate: Date?,
        estimatedMinutes: Int,
        notes: String
    ) -> BridgeTaskItem?

    /// Marks a task as completed. Returns true on success.
    func completeTask(withId id: String) -> Bool

    /// Searches tasks by query string. Returns matching tasks.
    func searchTasks(query: String) -> [BridgeTaskItem]

    /// Filters tasks. Pass -1 for categoryRawValue/priorityRawValue to skip that filter.
    func getTasksByFilter(
        categoryRawValue: Int,
        priorityRawValue: Int,
        showCompleted: Bool
    ) -> [BridgeTaskItem]

    /// Sets the due date for a task. Returns true on success.
    func setDueDate(_ date: Date, forTaskWithId id: String) -> Bool
}

/// Singleton manager that holds a reference to the C# task data provider.
/// The MAUI app sets the provider during initialization so Swift intents can access C# data.
@objc(TaskBridgeManager) public class TaskBridgeManager: NSObject {
    @objc public static let shared = TaskBridgeManager()

    @objc public weak var provider: TaskDataProvider?

    private override init() {
        super.init()
    }
}
