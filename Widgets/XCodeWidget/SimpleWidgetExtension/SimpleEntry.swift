import WidgetKit
import SwiftUI

/// The data model for a single widget timeline entry.
struct SimpleEntry: TimelineEntry {
    let date: Date
    let title: String
    let counter: Int
    let message: String
    let emoji: String
    let widgetUrl: String
}
