import Foundation
import ApplicationServices

/// A privacy-preserving health report. It records only timestamps and app
/// names—never the keys or text the user types.
final class RuntimeStatus: ObservableObject {
    static let shared = RuntimeStatus()

    @Published private(set) var accessibilityGranted = false
    @Published private(set) var inputMonitoringGranted = false
    @Published private(set) var eventTapRunning = false
    @Published private(set) var lastKeyboardEvent: Date?
    @Published private(set) var lastForegroundApplication = "尚未检测"
    @Published private(set) var lastTriggerAttempt: Date?
    @Published private(set) var lastTriggerOutcome = "尚未在 Codex 中输入触发符"

    private init() { refreshPermissions() }

    func refreshPermissions() {
        accessibilityGranted = AXIsProcessTrusted()
        inputMonitoringGranted = CGPreflightListenEventAccess()
    }

    func setEventTapRunning(_ running: Bool) {
        eventTapRunning = running
    }

    func recordKeyboardEvent(frontmostApplication: String) {
        lastKeyboardEvent = Date()
        lastForegroundApplication = frontmostApplication
    }

    func recordTrigger(outcome: String) {
        lastTriggerAttempt = Date()
        lastTriggerOutcome = outcome
    }

    func requestInputMonitoring() {
        // CGRequestListenEventAccess may display a macOS confirmation sheet or
        // restart this app. Never leave our guide in front of that sheet.
        PermissionGuideController.shared.suspendForSystemSettings()
        _ = CGRequestListenEventAccess()
        PermissionGuide.openInputMonitoringSettings()
        refreshPermissions()
    }
}
