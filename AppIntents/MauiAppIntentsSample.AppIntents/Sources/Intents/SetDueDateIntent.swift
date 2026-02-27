import AppIntents

/// Sets or updates the due date for a task.
/// Demonstrates: AppEntity parameter combined with Date parameter.
struct SetDueDateIntent: AppIntent {
    static var title: LocalizedStringResource = "Set Due Date"
    static var description = IntentDescription("Sets the due date for a task in TaskTracker")
    static var openAppWhenRun: Bool = false

    @Parameter(title: "Task")
    var target: TaskEntity

    @Parameter(title: "Due Date")
    var dueDate: Date

    static var parameterSummary: some ParameterSummary {
        Summary("Set due date of \(\.$target) to \(\.$dueDate)")
    }

    func perform() async throws -> some IntentResult & ProvidesDialog {
        guard let provider = TaskBridgeManager.shared.provider else {
            throw IntentError.appNotReady
        }

        let success = provider.setDueDate(dueDate, forTaskWithId: target.id)
        if !success {
            throw IntentError.taskNotFound
        }

        let formatter = DateFormatter()
        formatter.dateStyle = .medium
        let dateStr = formatter.string(from: dueDate)

        return .result(dialog: "Set due date of '\(target.title)' to \(dateStr)")
    }
}
