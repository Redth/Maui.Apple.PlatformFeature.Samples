import SwiftUI

/// Thin host app required by Xcode — this app is never shipped.
/// Only the Widget Extension target matters.
@main
struct XCodeWidgetApp: App {
    var body: some Scene {
        WindowGroup {
            ContentView()
        }
    }
}
