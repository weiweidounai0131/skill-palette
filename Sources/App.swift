import AppKit
@main
enum CodexSkillOverlayApplication {
    static func main() {
        let application = NSApplication.shared
        let delegate = AppDelegate()
        application.delegate = delegate
        application.run()
    }
}

@MainActor
final class AppDelegate: NSObject, NSApplicationDelegate {
    private var statusItem: NSStatusItem?
    private let settingsWindow = SettingsWindowController()

    func applicationDidFinishLaunching(_ notification: Notification) {
        // Keep the menu-bar entry, and also behave like a regular macOS app so
        // the current artwork is represented consistently in the Dock.
        NSApp.setActivationPolicy(.regular)
        // NSAlert otherwise asks AppKit for the cached application icon on the
        // first launch. Load the bundled icon explicitly so permission prompts
        // always use the current Skill Palette artwork.
        if let icon = AppIcon.current {
            NSApp.applicationIconImage = icon
        }
        configureStatusItem()
        SkillIndex.shared.rescan()
        // Finish building the panel before the event tap becomes available.
        // This removes the startup race where the very first # arrived while
        // SwiftUI was still constructing the picker.
        _ = PickerController.shared
        InputInterceptor.shared.start()
        // If macOS required a restart while permissions were being granted,
        // restore the unfinished guide so the user can see the completed
        // status and explicitly continue into the app.
        DispatchQueue.main.asyncAfter(deadline: .now() + 0.35) {
            PermissionGuide.resumePendingGuideAfterLaunchIfNeeded()
        }
    }

    func applicationShouldTerminateAfterLastWindowClosed(_ sender: NSApplication) -> Bool {
        false
    }

    private func configureStatusItem() {
        let item = NSStatusBar.system.statusItem(withLength: NSStatusItem.variableLength)
        let symbolConfiguration = NSImage.SymbolConfiguration(pointSize: 15, weight: .medium)
        let image = NSImage(systemSymbolName: "wand.and.stars", accessibilityDescription: "Skill Palette")?
            .withSymbolConfiguration(symbolConfiguration)
        image?.isTemplate = true
        item.button?.image = image
        item.button?.imagePosition = .imageOnly
        item.button?.toolTip = "Skill Palette · 搜索并调用 Skill"

        let menu = NSMenu()
        menu.addItem(withTitle: "打开设置…", action: #selector(openSettings), keyEquivalent: ",")
        menu.addItem(withTitle: "重新扫描 Skills", action: #selector(rescan), keyEquivalent: "r")
        menu.addItem(NSMenuItem.separator())
        menu.addItem(withTitle: "检查系统权限", action: #selector(checkPermissions), keyEquivalent: "")
        menu.addItem(NSMenuItem.separator())
        menu.addItem(withTitle: "退出 Skill Palette", action: #selector(quit), keyEquivalent: "q")
        menu.items.forEach { $0.target = self }
        item.menu = menu
        statusItem = item
    }

    @objc private func openSettings() {
        settingsWindow.show()
    }

    @objc private func rescan() {
        SkillIndex.shared.rescan()
    }

    @objc private func checkPermissions() {
        PermissionGuide.show(requestAccessibility: true)
    }

    @objc private func quit() {
        NSApp.terminate(nil)
    }
}

enum PermissionGuide {
    private static let pendingGuideDefaultsKey = "permissionGuidePendingCompletion"

    static func requestAccessibility() {
        // Hide first: the macOS consent prompt and System Settings must always
        // be reachable, even if this guide was previously the key window.
        PermissionGuideController.shared.suspendForSystemSettings()
        let options = [kAXTrustedCheckOptionPrompt.takeUnretainedValue() as String: true] as CFDictionary
        _ = AXIsProcessTrustedWithOptions(options)
        openPrivacySettings(anchor: "Privacy_Accessibility")
    }

    static func openInputMonitoringSettings() {
        openPrivacySettings(anchor: "Privacy_ListenEvent")
    }

    static func show(requestAccessibility: Bool) {
        RuntimeStatus.shared.refreshPermissions()
        if !RuntimeStatus.shared.accessibilityGranted || !RuntimeStatus.shared.inputMonitoringGranted {
            UserDefaults.standard.set(true, forKey: pendingGuideDefaultsKey)
        }
        PermissionGuideController.shared.show()
        if requestAccessibility {
            self.requestAccessibility()
        }
    }

    static func resumePendingGuideAfterLaunchIfNeeded() {
        RuntimeStatus.shared.refreshPermissions()
        let needsPermission = !RuntimeStatus.shared.accessibilityGranted || !RuntimeStatus.shared.inputMonitoringGranted
        guard UserDefaults.standard.bool(forKey: pendingGuideDefaultsKey) || needsPermission else { return }
        if needsPermission {
            UserDefaults.standard.set(true, forKey: pendingGuideDefaultsKey)
        }
        PermissionGuideController.shared.show()
    }

    static func completePendingGuide() {
        UserDefaults.standard.set(false, forKey: pendingGuideDefaultsKey)
    }

    static func openPrivacyAndSecurity() {
        guard let url = URL(string: "x-apple.systempreferences:com.apple.preference.security") else { return }
        PermissionGuideController.shared.suspendForSystemSettings()
        NSWorkspace.shared.open(url)
    }

    private static func openPrivacySettings(anchor: String) {
        guard let url = URL(string: "x-apple.systempreferences:com.apple.preference.security?\(anchor)") else { return }
        PermissionGuideController.shared.suspendForSystemSettings()
        NSWorkspace.shared.open(url)
    }
}

enum AppIcon {
    static var current: NSImage? {
        guard let url = Bundle.main.url(forResource: "AppIcon", withExtension: "icns") else { return nil }
        return NSImage(contentsOf: url)
    }
}
