import AppIntents

/// Represents a task as an AppEntity, making it available to Siri, Shortcuts, and Spotlight.
struct TaskEntity: AppEntity {
    static var defaultQuery = TaskEntityQuery()

    static var typeDisplayRepresentation: TypeDisplayRepresentation = "Task"

    var id: String

    @Property(title: "Title")
    var title: String

    @Property(title: "Notes")
    var notes: String?

    @Property(title: "Priority")
    var priority: TaskPriority

    @Property(title: "Category")
    var category: TaskCategory

    @Property(title: "Due Date")
    var dueDate: Date?

    @Property(title: "Estimated Minutes")
    var estimatedMinutes: Int?

    @Property(title: "Completed")
    var isCompleted: Bool

    var displayRepresentation: DisplayRepresentation {
        let priorityText = TaskPriority.caseDisplayRepresentations[priority]?.title ?? "Medium"
        let categoryText = TaskCategory.caseDisplayRepresentations[category]?.title ?? "Personal"
        return DisplayRepresentation(
            title: "\(title)",
            subtitle: "\(priorityText) · \(categoryText)"
        )
    }

    /// Creates a TaskEntity from a BridgeTaskItem (data from C#).
    init(from bridge: BridgeTaskItem) {
        self.id = bridge.id
        self.title = bridge.title
        self.notes = bridge.notes.isEmpty ? nil : bridge.notes
        self.priority = TaskPriority(rawValue: bridge.priorityRawValue) ?? .medium
        self.category = TaskCategory(rawValue: bridge.categoryRawValue) ?? .personal
        self.dueDate = bridge.dueDate
        self.estimatedMinutes = bridge.estimatedMinutes >= 0 ? bridge.estimatedMinutes : nil
        self.isCompleted = bridge.isCompleted
    }
}
