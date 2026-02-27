import Foundation
import os

/// Handles reading and writing JSON-encoded WidgetData to the App Group shared container
/// using file-based I/O. This is more reliable than UserDefaults for cross-process communication
/// because UserDefaults(suiteName:) can resolve to different plist files for the app vs
/// widget extension, especially in simulator builds without proper code signing.
class SharedStorage {
    private let logger = Logger(subsystem: "com.mauiapplewidgets", category: "SharedStorage")

    private func containerURL() -> URL? {
        return FileManager.default.containerURL(forSecurityApplicationGroupIdentifier: Settings.groupId)
    }

    private func fileURL(for filename: String) -> URL? {
        return containerURL()?.appendingPathComponent(filename)
    }

    // MARK: - Read data written by the .NET MAUI app

    func readAppData() -> WidgetData? {
        guard let url = fileURL(for: Settings.fromAppFile) else {
            logger.error("Cannot resolve App Group container URL")
            return nil
        }
        guard FileManager.default.fileExists(atPath: url.path) else {
            logger.info("App data file does not exist yet at \(url.path)")
            return nil
        }
        do {
            let data = try Data(contentsOf: url)
            let decoded = try JSONDecoder().decode(WidgetData.self, from: data)
            logger.info("Read app data: counter=\(decoded.counter) message=\(decoded.message)")
            return decoded
        } catch {
            logger.error("Failed to read app data: \(error.localizedDescription)")
            return nil
        }
    }

    // MARK: - Read data written by the widget itself

    func readWidgetData() -> WidgetData? {
        guard let url = fileURL(for: Settings.fromWidgetFile) else { return nil }
        guard FileManager.default.fileExists(atPath: url.path) else { return nil }
        do {
            let data = try Data(contentsOf: url)
            return try JSONDecoder().decode(WidgetData.self, from: data)
        } catch {
            return nil
        }
    }

    // MARK: - Write data from widget for the app to consume

    func writeWidgetData(_ data: WidgetData) {
        guard let url = fileURL(for: Settings.fromWidgetFile) else { return }
        do {
            let jsonData = try JSONEncoder().encode(data)
            try jsonData.write(to: url, options: .atomic)
            logger.info("Wrote widget data: counter=\(data.counter)")
        } catch {
            logger.error("Failed to write widget data: \(error.localizedDescription)")
        }
    }

    // MARK: - Best available values

    /// Returns the counter from whichever source was updated most recently.
    func getBestCounter() -> Int {
        let appData = readAppData()
        let widgetData = readWidgetData()

        switch (appData, widgetData) {
        case let (app?, widget?):
            // Both exist — use the one updated most recently
            return app.updatedAt >= widget.updatedAt ? app.counter : widget.counter
        case let (app?, nil):
            return app.counter
        case let (nil, widget?):
            return widget.counter
        default:
            return 0
        }
    }

    func getBestMessage() -> String {
        let appData = readAppData()
        let widgetData = readWidgetData()

        switch (appData, widgetData) {
        case let (app?, widget?):
            return app.updatedAt >= widget.updatedAt ? app.message : widget.message
        case let (app?, nil):
            return app.message
        case let (nil, widget?):
            return widget.message
        default:
            return ""
        }
    }
}
