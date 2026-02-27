import Foundation

/// Data transfer object for passing task data between Swift and C# via the binding bridge.
/// All properties are @objc compatible (no optionals for value types — use sentinel values).
@objc(BridgeTaskItem) public class BridgeTaskItem: NSObject {
    @objc public var id: String
    @objc public var title: String
    @objc public var notes: String
    @objc public var priorityRawValue: Int
    @objc public var categoryRawValue: Int
    @objc public var dueDate: Date?
    @objc public var estimatedMinutes: Int  // -1 means nil
    @objc public var isCompleted: Bool
    @objc public var createdAt: Date

    @objc public init(
        id: String,
        title: String,
        notes: String,
        priorityRawValue: Int,
        categoryRawValue: Int,
        dueDate: Date?,
        estimatedMinutes: Int,
        isCompleted: Bool,
        createdAt: Date
    ) {
        self.id = id
        self.title = title
        self.notes = notes
        self.priorityRawValue = priorityRawValue
        self.categoryRawValue = categoryRawValue
        self.dueDate = dueDate
        self.estimatedMinutes = estimatedMinutes
        self.isCompleted = isCompleted
        self.createdAt = createdAt
    }
}
