import AppKit
import SwiftUI

@MainActor
final class SettingsWindowController: NSWindowController, NSWindowDelegate {
    init() {
        let window = NSWindow(
            contentRect: NSRect(x: 0, y: 0, width: 920, height: 700),
            styleMask: [.titled, .closable, .miniaturizable],
            backing: .buffered,
            defer: false
        )
        window.title = "Skill Palette"
        window.titleVisibility = .hidden
        window.titlebarAppearsTransparent = true
        window.isMovableByWindowBackground = true
        // The close-to-Dock animation otherwise asks LaunchServices for a
        // cached icon. Bind the current bundled artwork directly to this
        // window so the animation always matches the visible app icon.
        window.miniwindowImage = AppIcon.current
        window.center()
        window.contentView = NSHostingView(rootView: SettingsView())
        super.init(window: window)
        window.delegate = self
    }

    required init?(coder: NSCoder) { fatalError("init(coder:) has not been implemented") }

    func show() {
        // A menu-bar-only app cannot reliably own a focusable settings window
        // on recent macOS releases. Become a regular app while preferences are
        // visible, then return to accessory mode when the window closes.
        NSApp.setActivationPolicy(.regular)
        NSRunningApplication.current.activate(options: [.activateIgnoringOtherApps])
        window?.level = .normal
        showWindow(nil)
        window?.makeKeyAndOrderFront(nil)
    }

    func windowWillClose(_ notification: Notification) {}
}

private enum SettingsDestination: String, CaseIterable, Identifiable {
    case general
    case skills
    case diagnostics

    var id: String { rawValue }

    var title: String {
        switch self {
        case .general: "通用"
        case .skills: "Skills"
        case .diagnostics: "权限与诊断"
        }
    }

    var symbol: String {
        switch self {
        case .general: "slider.horizontal.3"
        case .skills: "wand.and.stars"
        case .diagnostics: "waveform.path.ecg"
        }
    }
}

struct SettingsView: View {
    @ObservedObject private var settings = OverlaySettings.shared
    @ObservedObject private var index = SkillIndex.shared
    @ObservedObject private var runtime = RuntimeStatus.shared
    @State private var destination: SettingsDestination = .general
    @State private var filter = ""
    @State private var showsAdvancedAppRules = false
    @State private var selectedSkillScope: SkillScope = .all
    @State private var diagnosticFeedback: DiagnosticFeedback?

    private var visibleSkills: [Skill] {
        index.skills(in: selectedSkillScope, matching: filter)
    }

    var body: some View {
        VStack(spacing: 0) {
            PreferencesToolbar(selection: $destination)
            Divider()

            ScrollView {
                VStack(alignment: .leading, spacing: 32) {
                    switch destination {
                    case .general:
                        generalPage
                    case .skills:
                        skillsPage
                    case .diagnostics:
                        diagnosticsPage
                    }
                }
                .frame(maxWidth: 760, alignment: .leading)
                .padding(.horizontal, 40)
                .padding(.vertical, 38)
                .frame(maxWidth: .infinity, alignment: .top)
            }
            .background(Color(nsColor: .windowBackgroundColor))
        }
        .frame(width: 920, height: 700)
        .animation(.easeOut(duration: 0.18), value: destination)
        .onChange(of: destination) { value in
            if value == .diagnostics {
                runtime.refreshPermissions()
            }
        }
    }

    private var generalPage: some View {
        Group {
            PageHeader(
                title: "通用",
                subtitle: "设置如何在 Codex 中唤起并插入 Skill。"
            )

            SettingsGroup(title: "唤起方式", footer: "默认使用 #，可避免与 Codex 原本的 @ 菜单冲突。") {
                SettingsTextRow(
                    title: "触发字符",
                    detail: "在 Codex 输入框键入这些字符时打开 Skill 搜索。"
                ) {
                    TextField("#", text: $settings.triggerCharacters)
                        .fontDesign(.monospaced)
                        .textFieldStyle(.roundedBorder)
                        .frame(width: 120)
                        .accessibilityLabel("触发字符")
                }

                SettingsToggleRow(
                    title: "仅在 Codex 中启用",
                    detail: "避免在其他应用输入 # 或 @ 时意外呼出搜索。",
                    isOn: $settings.codexOnly
                )

                AdvancedAppRulesEditor(
                    isExpanded: $showsAdvancedAppRules,
                    bundleMatchers: $settings.bundleMatchers
                )
            }

            SettingsGroup(title: "插入方式", footer: "选中 Skill 后，应用会将调用文本插回原来的 Codex 输入位置。") {
                SettingsTextRow(
                    title: "调用前缀",
                    detail: "插入示例：\(settings.invocationPrefix)dashiai-ppt"
                ) {
                    TextField("@", text: $settings.invocationPrefix)
                        .fontDesign(.monospaced)
                        .textFieldStyle(.roundedBorder)
                        .frame(width: 120)
                        .accessibilityLabel("Skill 调用前缀")
                }
            }
        }
    }

    private var skillsPage: some View {
        Group {
            PageHeader(
                title: "Skills",
                subtitle: "通过标签和收藏，让常用 Skill 更容易被搜索到。"
            )

            HStack(spacing: 12) {
                Label("\(index.skills.count) 个已索引 Skill", systemImage: "tray.full")
                    .font(.subheadline)
                    .foregroundStyle(.secondary)
                Spacer()
                Button {
                    index.rescan()
                } label: {
                    Label("重新扫描", systemImage: "arrow.clockwise")
                }
                .controlSize(.regular)
                .accessibilityHint("重新读取本机 Skill 目录")
            }

            VStack(spacing: 14) {
                SkillScopePicker(
                    scopes: index.availableScopes(),
                    selection: $selectedSkillScope,
                    count: { index.skills(in: $0).count }
                )

                HStack(spacing: 10) {
                    Image(systemName: "magnifyingglass")
                        .foregroundStyle(.secondary)
                    TextField("搜索名称、描述或标签", text: $filter)
                        .textFieldStyle(.plain)
                        .accessibilityLabel("筛选 Skills")
                        .onChange(of: filter) { value in
                            if !value.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty {
                                selectedSkillScope = .all
                            }
                        }
                    if !filter.isEmpty {
                        Button {
                            filter = ""
                        } label: {
                            Image(systemName: "xmark.circle.fill")
                                .foregroundStyle(.tertiary)
                        }
                        .buttonStyle(.plain)
                        .accessibilityLabel("清除筛选")
                    }
                }
                .padding(.horizontal, 12)
                .frame(height: 38)
                .background(.quaternary, in: RoundedRectangle(cornerRadius: 9, style: .continuous))

                if !filter.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty {
                    Label("正在搜索全部分类", systemImage: "magnifyingglass")
                        .font(.caption)
                        .foregroundStyle(.secondary)
                        .frame(maxWidth: .infinity, alignment: .leading)
                }

                if visibleSkills.isEmpty {
                    EmptySkillState(isFiltering: !filter.isEmpty)
                } else {
                    LazyVStack(spacing: 10) {
                        ForEach(visibleSkills) { skill in
                            SkillSettingsRow(skill: skill)
                        }
                    }
                }
            }
        }
    }

    private var diagnosticsPage: some View {
        Group {
            PageHeader(
                title: "权限与诊断",
                subtitle: "确认 macOS 权限、键盘监听和最近一次触发是否正常。"
            )

            SettingsGroup(title: "运行状态", footer: "状态会在打开本页和点击操作后刷新；不保存你的键盘输入内容。") {
                DiagnosticStatusRow(
                    title: "辅助功能",
                    isHealthy: runtime.accessibilityGranted,
                    healthyText: "已开启",
                    unhealthyText: "需要授权"
                )
                DiagnosticStatusRow(
                    title: "输入监控",
                    isHealthy: runtime.inputMonitoringGranted,
                    healthyText: "已开启",
                    unhealthyText: "需要授权"
                )
                DiagnosticStatusRow(
                    title: "键盘监听器",
                    isHealthy: runtime.eventTapRunning,
                    healthyText: "正在运行",
                    unhealthyText: "未运行"
                )
            }

            SettingsGroup(title: "最近活动") {
                KeyValueRow(title: "最近收到键盘事件", value: runtime.lastKeyboardEvent.map(Self.timeFormatter.string) ?? "尚未收到")
                KeyValueRow(title: "当时前台应用", value: runtime.lastForegroundApplication)
                KeyValueRow(title: "最近一次触发", value: runtime.lastTriggerAttempt.map(Self.timeFormatter.string) ?? "尚未触发")
                KeyValueRow(title: "触发结果", value: runtime.lastTriggerOutcome, allowsWrapping: true)
            }

            SettingsGroup(title: "操作", footer: "打开相应系统设置后，回到本页点击“刷新状态”确认结果。") {
                DiagnosticsActions(
                    refresh: {
                        runtime.refreshPermissions()
                        presentDiagnosticFeedback("权限状态已刷新。", success: true)
                    },
                    restartListener: {
                        InputInterceptor.shared.restart()
                        DispatchQueue.main.asyncAfter(deadline: .now() + 0.15) {
                            runtime.refreshPermissions()
                            presentDiagnosticFeedback(
                                runtime.eventTapRunning ? "键盘监听器已重新启动。" : "监听器未能启动，请检查输入监控权限。",
                                success: runtime.eventTapRunning
                            )
                        }
                    },
                    requestInputMonitoring: {
                        runtime.requestInputMonitoring()
                        presentDiagnosticFeedback("已打开“输入监控”系统设置，请开启 Skill Palette。", success: true)
                    },
                    requestAccessibility: {
                        PermissionGuide.requestAccessibility()
                        presentDiagnosticFeedback("已打开“辅助功能”系统设置，请开启 Skill Palette。", success: true)
                    }
                )
                if let diagnosticFeedback {
                    Divider()
                        .padding(.horizontal, 14)
                    DiagnosticFeedbackBanner(feedback: diagnosticFeedback)
                        .padding(14)
                        .transition(.opacity.combined(with: .move(edge: .top)))
                }
            }
        }
    }

    private func presentDiagnosticFeedback(_ message: String, success: Bool) {
        let feedback = DiagnosticFeedback(message: message, success: success)
        withAnimation(.easeOut(duration: 0.18)) {
            diagnosticFeedback = feedback
        }
        DispatchQueue.main.asyncAfter(deadline: .now() + 3) {
            guard diagnosticFeedback?.id == feedback.id else { return }
            withAnimation(.easeIn(duration: 0.16)) {
                diagnosticFeedback = nil
            }
        }
    }

    private static let timeFormatter: DateFormatter = {
        let formatter = DateFormatter()
        formatter.dateFormat = "HH:mm:ss"
        return formatter
    }()
}

private struct PreferencesToolbar: View {
    @Binding var selection: SettingsDestination

    var body: some View {
        HStack {
            Spacer(minLength: 90)
            HStack(spacing: 8) {
                ForEach(SettingsDestination.allCases) { destination in
                    Button {
                        selection = destination
                    } label: {
                        VStack(spacing: 5) {
                            Image(systemName: destination.symbol)
                                .font(.system(size: 23, weight: .medium))
                                .frame(height: 28)
                            Text(destination.title)
                                .font(.caption.weight(selection == destination ? .semibold : .regular))
                        }
                        .foregroundStyle(selection == destination ? Color.accentColor : Color.secondary)
                        .frame(width: 96, height: 66)
                        .background(selection == destination ? Color.accentColor.opacity(0.12) : .clear, in: RoundedRectangle(cornerRadius: 10, style: .continuous))
                        .contentShape(RoundedRectangle(cornerRadius: 10, style: .continuous))
                    }
                    .buttonStyle(.plain)
                    .accessibilityLabel(destination.title)
                    .accessibilityAddTraits(selection == destination ? .isSelected : [])
                }
            }
            Spacer(minLength: 90)
        }
        .padding(.top, 18)
        .padding(.bottom, 13)
        .background(.bar)
    }
}

private struct PageHeader: View {
    let title: String
    let subtitle: String

    var body: some View {
        VStack(alignment: .leading, spacing: 7) {
            Text(title)
                .font(.title2.weight(.semibold))
            Text(subtitle)
                .font(.body)
                .foregroundStyle(.secondary)
        }
        .accessibilityElement(children: .combine)
    }
}

private struct SettingsGroup<Content: View>: View {
    let title: String
    var footer: String? = nil
    @ViewBuilder let content: Content

    var body: some View {
        VStack(alignment: .leading, spacing: 10) {
            Text(title)
                .font(.headline)
            VStack(spacing: 0) {
                content
            }
            .background(Color(nsColor: .controlBackgroundColor), in: RoundedRectangle(cornerRadius: 11, style: .continuous))
            .overlay {
                RoundedRectangle(cornerRadius: 11, style: .continuous)
                    .stroke(Color(nsColor: .separatorColor).opacity(0.65), lineWidth: 0.5)
            }

            if let footer {
                Text(footer)
                    .font(.caption)
                    .foregroundStyle(.secondary)
                    .padding(.horizontal, 2)
            }
        }
    }
}

private struct SettingsTextRow<Trailing: View>: View {
    let title: String
    let detail: String
    @ViewBuilder let trailing: Trailing

    var body: some View {
        HStack(alignment: .center, spacing: 20) {
            VStack(alignment: .leading, spacing: 3) {
                Text(title)
                Text(detail)
                    .font(.caption)
                    .foregroundStyle(.secondary)
            }
            Spacer(minLength: 20)
            trailing
        }
        .padding(.horizontal, 16)
        .padding(.vertical, 13)
    }
}

struct SkillScopePicker: View {
    let scopes: [SkillScope]
    @Binding var selection: SkillScope
    let count: (SkillScope) -> Int

    var body: some View {
        HStack(spacing: 12) {
            Label("分类", systemImage: "folder")
                .font(.subheadline.weight(.medium))
                .foregroundStyle(.secondary)

            Spacer()

            Picker("Skill 分类", selection: $selection) {
                ForEach(scopes) { scope in
                    Label("\(scope.title)（\(count(scope))）", systemImage: scope.symbol)
                        .tag(scope)
                }
            }
            .labelsHidden()
            .pickerStyle(.menu)
            .frame(minWidth: 210, alignment: .trailing)
            .accessibilityLabel("Skill 分类")
            .accessibilityValue("\(selection.title)，\(count(selection)) 个 Skill")
        }
        .padding(.horizontal, 14)
        .padding(.vertical, 9)
        .background(Color(nsColor: .controlBackgroundColor), in: RoundedRectangle(cornerRadius: 10, style: .continuous))
        .overlay {
            RoundedRectangle(cornerRadius: 10, style: .continuous)
                .stroke(Color(nsColor: .separatorColor).opacity(0.55), lineWidth: 0.5)
        }
    }
}

private struct SettingsToggleRow: View {
    let title: String
    let detail: String
    @Binding var isOn: Bool

    var body: some View {
        HStack(spacing: 20) {
            VStack(alignment: .leading, spacing: 3) {
                Text(title)
                Text(detail)
                    .font(.caption)
                    .foregroundStyle(.secondary)
            }
            Spacer(minLength: 20)
            Toggle(title, isOn: $isOn)
                .labelsHidden()
                .accessibilityLabel(title)
        }
        .padding(.horizontal, 16)
        .padding(.vertical, 13)
    }
}

private struct AdvancedAppRulesEditor: View {
    @Binding var isExpanded: Bool
    @Binding var bundleMatchers: String

    var body: some View {
        VStack(spacing: 0) {
            Button {
                withAnimation(.easeInOut(duration: 0.18)) {
                    isExpanded.toggle()
                }
            } label: {
                HStack(spacing: 11) {
                    Image(systemName: "chevron.right")
                        .font(.caption.weight(.bold))
                        .foregroundStyle(.tertiary)
                        .rotationEffect(.degrees(isExpanded ? 90 : 0))

                    VStack(alignment: .leading, spacing: 3) {
                        HStack(spacing: 7) {
                            Text("目标应用识别规则")
                            Text("高级")
                                .font(.caption2.weight(.semibold))
                                .foregroundStyle(.secondary)
                                .padding(.horizontal, 6)
                                .padding(.vertical, 2)
                                .background(.quaternary, in: Capsule())
                        }
                        Text("仅在自动识别失败时需要修改")
                            .font(.caption)
                            .foregroundStyle(.secondary)
                    }
                    Spacer()
                }
                .contentShape(Rectangle())
                .padding(.horizontal, 16)
                .padding(.vertical, 13)
            }
            .buttonStyle(.plain)
            .accessibilityLabel("目标应用识别规则，高级设置")
            .accessibilityValue(isExpanded ? "已展开" : "已收起")

            if isExpanded {
                Divider()
                    .padding(.horizontal, 16)

                VStack(alignment: .leading, spacing: 8) {
                    Text("识别关键词")
                        .font(.subheadline.weight(.medium))
                    TextField("例如：codex, chatgpt", text: $bundleMatchers)
                        .textFieldStyle(.roundedBorder)
                        .accessibilityLabel("目标应用识别关键词")
                    Text("多个关键词请用英文逗号分隔。默认值适用于 Codex 和 ChatGPT 桌面端。")
                        .font(.caption)
                        .foregroundStyle(.secondary)
                }
                .padding(16)
            }
        }
    }
}

private struct DiagnosticsActions: View {
    let refresh: () -> Void
    let restartListener: () -> Void
    let requestInputMonitoring: () -> Void
    let requestAccessibility: () -> Void

    var body: some View {
        VStack(alignment: .leading, spacing: 12) {
            HStack(spacing: 10) {
                Button(action: refresh) {
                    Label("刷新状态", systemImage: "arrow.clockwise")
                }
                Button(action: restartListener) {
                    Label("重新启动监听器", systemImage: "arrow.triangle.2.circlepath")
                }
            }

            Divider()

            HStack(spacing: 10) {
                Button(action: requestInputMonitoring) {
                    Label("输入监控设置", systemImage: "keyboard")
                }
                Button(action: requestAccessibility) {
                    Label("辅助功能设置", systemImage: "hand.raised")
                }
            }
        }
        .buttonStyle(.bordered)
        .controlSize(.regular)
        .padding(14)
        .frame(maxWidth: .infinity, alignment: .leading)
    }
}

private struct DiagnosticFeedback: Identifiable, Equatable {
    let id = UUID()
    let message: String
    let success: Bool
}

private struct DiagnosticFeedbackBanner: View {
    let feedback: DiagnosticFeedback

    var body: some View {
        Label(feedback.message, systemImage: feedback.success ? "checkmark.circle.fill" : "exclamationmark.triangle.fill")
            .font(.subheadline)
            .foregroundStyle(feedback.success ? Color.green : Color.orange)
            .frame(maxWidth: .infinity, alignment: .leading)
            .accessibilityElement(children: .combine)
    }
}

private struct DiagnosticStatusRow: View {
    let title: String
    let isHealthy: Bool
    let healthyText: String
    let unhealthyText: String

    var body: some View {
        HStack {
            Label(title, systemImage: isHealthy ? "checkmark.circle.fill" : "exclamationmark.triangle.fill")
                .foregroundStyle(isHealthy ? Color.primary : Color.orange)
            Spacer()
            Text(isHealthy ? healthyText : unhealthyText)
                .font(.subheadline.weight(.medium))
                .foregroundStyle(isHealthy ? .green : .orange)
        }
        .padding(.horizontal, 16)
        .padding(.vertical, 13)
        .accessibilityElement(children: .combine)
    }
}

private struct KeyValueRow: View {
    let title: String
    let value: String
    var allowsWrapping = false

    var body: some View {
        HStack(alignment: allowsWrapping ? .top : .center, spacing: 20) {
            Text(title)
                .foregroundStyle(.primary)
            Spacer(minLength: 20)
            Text(value)
                .multilineTextAlignment(.trailing)
                .lineLimit(allowsWrapping ? nil : 1)
                .truncationMode(.middle)
                .foregroundStyle(.secondary)
        }
        .padding(.horizontal, 16)
        .padding(.vertical, 13)
    }
}

private struct SkillSettingsRow: View {
    @ObservedObject private var settings = OverlaySettings.shared
    let skill: Skill

    var body: some View {
        VStack(alignment: .leading, spacing: 10) {
            HStack(alignment: .top, spacing: 11) {
                Button {
                    settings.toggleFavorite(skill)
                } label: {
                    Image(systemName: settings.isFavorite(skill) ? "star.fill" : "star")
                        .font(.system(size: 15, weight: .medium))
                        .foregroundStyle(settings.isFavorite(skill) ? .yellow : .secondary)
                        .frame(width: 28, height: 28)
                        .background(.quaternary, in: Circle())
                }
                .buttonStyle(.plain)
                .accessibilityLabel(settings.isFavorite(skill) ? "取消收藏 \(skill.displayName)" : "收藏 \(skill.displayName)")

                VStack(alignment: .leading, spacing: 3) {
                    Text(skill.displayName)
                        .font(.headline)
                    Text(skill.description.isEmpty ? skill.path : skill.description)
                        .font(.caption)
                        .foregroundStyle(.secondary)
                        .lineLimit(2)
                    if skill.category != "其他" {
                        Label(skill.category, systemImage: "folder")
                            .font(.caption2)
                            .foregroundStyle(.secondary)
                    }
                }
                Spacer(minLength: 8)
            }

            HStack(spacing: 8) {
                Image(systemName: "tag")
                    .foregroundStyle(.secondary)
                TextField("添加标签，例如：PPT、汇报、幻灯片", text: Binding(
                    get: { settings.tags(for: skill) },
                    set: { settings.setTags($0, for: skill) }
                ))
                .textFieldStyle(.plain)
                .accessibilityLabel("\(skill.displayName) 的标签")
            }
            .padding(.horizontal, 10)
            .frame(height: 34)
            .background(.quaternary, in: RoundedRectangle(cornerRadius: 8, style: .continuous))
        }
        .padding(14)
        .background(Color(nsColor: .controlBackgroundColor), in: RoundedRectangle(cornerRadius: 11, style: .continuous))
        .overlay {
            RoundedRectangle(cornerRadius: 11, style: .continuous)
                .stroke(Color(nsColor: .separatorColor).opacity(0.65), lineWidth: 0.5)
        }
    }
}

private struct EmptySkillState: View {
    let isFiltering: Bool

    var body: some View {
        VStack(spacing: 8) {
            Image(systemName: isFiltering ? "magnifyingglass" : "tray")
                .font(.system(size: 26))
                .foregroundStyle(.secondary)
            Text(isFiltering ? "没有匹配的 Skill" : "暂未扫描到 Skill")
                .font(.headline)
            Text(isFiltering ? "试试名称、描述或你添加的标签。" : "点击“重新扫描”读取本机 Skill 目录。")
                .font(.caption)
                .foregroundStyle(.secondary)
        }
        .frame(maxWidth: .infinity)
        .padding(.vertical, 56)
        .background(.quaternary, in: RoundedRectangle(cornerRadius: 12, style: .continuous))
    }
}
