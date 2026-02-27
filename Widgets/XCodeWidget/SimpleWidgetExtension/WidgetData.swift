import Foundation

/// Shared data contract — must match WidgetData.cs on the .NET MAUI side.
struct WidgetData: Codable {
    var version: Int = 1
    var title: String = ""
    var message: String = ""
    var counter: Int = 0
    var updatedAt: String = ""
    var extras: [String: String] = [:]
}
