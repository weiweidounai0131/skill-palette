import Foundation
import ServiceManagement

@MainActor
final class LaunchAtLogin: ObservableObject {
    static let shared = LaunchAtLogin()

    @Published private(set) var isEnabled = false
    @Published private(set) var needsApproval = false
    @Published private(set) var message: String?

    private init() {
        refresh()
    }

    func refresh() {
        switch SMAppService.mainApp.status {
        case .enabled:
            isEnabled = true
            needsApproval = false
        case .requiresApproval:
            isEnabled = false
            needsApproval = true
        case .notRegistered, .notFound:
            isEnabled = false
            needsApproval = false
        @unknown default:
            isEnabled = false
            needsApproval = false
        }
    }

    func setEnabled(_ enabled: Bool) {
        message = nil

        do {
            if enabled {
                try SMAppService.mainApp.register()
            } else if SMAppService.mainApp.status != .notRegistered {
                try SMAppService.mainApp.unregister()
            }
            refresh()

            if enabled && needsApproval {
                message = "macOS 需要你在“登录项”中确认 Skill Palette。"
            }
        } catch {
            refresh()
            message = enabled
                ? "无法启用登录时启动：\(error.localizedDescription)"
                : "无法关闭登录时启动：\(error.localizedDescription)"
        }
    }
}
