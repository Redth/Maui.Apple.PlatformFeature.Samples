import WidgetKit
import SwiftUI

/// Constants shared between all widget files.
/// Keep these in sync with WidgetConstants.cs on the .NET MAUI side.
struct Settings {
    static let groupId = "group.com.mauiapplewidgets.app"
    static let fromAppFile = "widget_data_fromapp.json"
    static let fromWidgetFile = "widget_data_fromwidget.json"
    static let widgetKind = "SimpleWidget"
    static let urlScheme = "mauiapplewidgets"
    static let urlHost = "widget"
}
