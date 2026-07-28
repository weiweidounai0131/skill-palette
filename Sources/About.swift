import AppKit
import SwiftUI

@MainActor
final class AboutWindowController: NSWindowController {
    init() {
        let window = NSWindow(
            contentRect: NSRect(x: 0, y: 0, width: 450, height: 470),
            styleMask: [.titled, .closable, .miniaturizable, .fullSizeContentView],
            backing: .buffered,
            defer: false
        )
        window.title = "关于 Skill Palette"
        window.titleVisibility = .hidden
        window.titlebarAppearsTransparent = true
        window.isMovableByWindowBackground = true
        window.isReleasedWhenClosed = false
        window.level = .normal
        window.miniwindowImage = AppIcon.current
        window.contentView = NSHostingView(rootView: AboutSkillPaletteView())
        super.init(window: window)
    }

    required init?(coder: NSCoder) { fatalError("init(coder:) has not been implemented") }

    func show() {
        NSApp.setActivationPolicy(.regular)
        NSRunningApplication.current.activate(options: [.activateIgnoringOtherApps])
        window?.center()
        showWindow(nil)
        window?.makeKeyAndOrderFront(nil)
    }
}

private struct AboutSkillPaletteView: View {
    private let repositoryURL = URL(string: "https://github.com/weiweidounai0131/skill-palette")!
    private let releaseURL = URL(string: "https://github.com/weiweidounai0131/skill-palette/releases/latest")!

    var body: some View {
        VStack(spacing: 0) {
            Spacer(minLength: 28)

            Image(nsImage: AppIcon.current ?? NSImage())
                .resizable()
                .interpolation(.high)
                .frame(width: 126, height: 126)
                .accessibilityHidden(true)

            Text("Skill Palette")
                .font(.system(size: 30, weight: .semibold))
                .padding(.top, 17)

            Text("版本 1.1.1")
                .font(.system(size: 16))
                .foregroundStyle(.secondary)
                .padding(.top, 5)

            Text("在 Codex 中搜索并调用本机 Skills。")
                .font(.system(size: 16))
                .foregroundStyle(.secondary)
                .padding(.top, 21)

            HStack(spacing: 28) {
                Link(destination: repositoryURL) {
                    Text("GitHub")
                }
                .accessibilityLabel("在 GitHub 打开 Skill Palette 项目")

                Link(destination: releaseURL) {
                    Text("更新")
                }
                .accessibilityLabel("在 GitHub 查看 Skill Palette 更新")
            }
            .font(.system(size: 18, weight: .medium))
            .padding(.top, 24)

            Spacer(minLength: 30)

            Text("Copyright © 2026 Jiang Jiawei. All rights reserved.")
                .font(.system(size: 12))
                .foregroundStyle(.tertiary)
                .multilineTextAlignment(.center)
                .padding(.bottom, 24)
        }
        .frame(width: 450, height: 470)
        .background(Color(nsColor: .windowBackgroundColor))
        .accessibilityElement(children: .contain)
    }
}
