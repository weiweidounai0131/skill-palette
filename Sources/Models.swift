import Foundation
import SwiftUI

struct Skill: Identifiable, Codable, Hashable {
    let id: String
    let name: String
    let description: String
    let path: String
    let category: String

    var displayName: String { name.isEmpty ? URL(fileURLWithPath: path).deletingLastPathComponent().lastPathComponent : name }
    var legacyID: String { name }
}

enum SkillScope: Hashable, Identifiable {
    case favorites
    case all
    case category(String)

    var id: String {
        switch self {
        case .favorites: "favorites"
        case .all: "all"
        case let .category(name): "category:\(name)"
        }
    }

    var title: String {
        switch self {
        case .favorites: "收藏"
        case .all: "全部"
        case let .category(name): name
        }
    }

    var symbol: String {
        switch self {
        case .favorites: "star.fill"
        case .all: "square.grid.2x2"
        case .category: "folder"
        }
    }
}

final class OverlaySettings: ObservableObject {
    static let shared = OverlaySettings()

    @Published var triggerCharacters: String { didSet { save() } }
    @Published var codexOnly: Bool { didSet { save() } }
    @Published var bundleMatchers: String { didSet { save() } }
    @Published var invocationPrefix: String { didSet { save() } }
    @Published var showDockIcon: Bool { didSet { save() } }
    @Published var tags: [String: String] { didSet { save() } }
    @Published var favorites: [String] { didSet { save() } }
    @Published var recentNames: [String] { didSet { save() } }

    private let defaults = UserDefaults.standard
    private enum Key {
        static let triggers = "triggers"
        static let codexOnly = "codexOnly"
        static let matchers = "matchers"
        static let prefix = "prefix"
        static let showDockIcon = "showDockIcon"
        static let tags = "tags"
        static let legacyAliases = "aliases"
        static let favorites = "favorites"
        static let recents = "recents"
    }

    private init() {
        triggerCharacters = defaults.string(forKey: Key.triggers) ?? "#"
        codexOnly = defaults.object(forKey: Key.codexOnly) as? Bool ?? true
        bundleMatchers = defaults.string(forKey: Key.matchers) ?? "codex,chatgpt"
        invocationPrefix = defaults.string(forKey: Key.prefix) ?? "@"
        // Skill Palette is primarily a menu-bar utility. Existing installs
        // can still opt into a Dock icon from Settings at any time.
        showDockIcon = defaults.object(forKey: Key.showDockIcon) as? Bool ?? false
        tags = defaults.dictionary(forKey: Key.tags) as? [String: String]
            ?? defaults.dictionary(forKey: Key.legacyAliases) as? [String: String]
            ?? [:]
        favorites = defaults.stringArray(forKey: Key.favorites) ?? []
        recentNames = defaults.stringArray(forKey: Key.recents) ?? []
    }

    func tags(for skill: Skill) -> String {
        tags[skill.id] ?? tags[skill.legacyID] ?? ""
    }

    func setTags(_ value: String, for skill: Skill) {
        tags[skill.id] = value
        if skill.id != skill.legacyID {
            tags.removeValue(forKey: skill.legacyID)
        }
    }

    func isFavorite(_ skill: Skill) -> Bool {
        favorites.contains(skill.id) || favorites.contains(skill.legacyID)
    }

    func toggleFavorite(_ skill: Skill) {
        let storedIDs = [skill.id, skill.legacyID]
        if storedIDs.contains(where: favorites.contains) {
            favorites.removeAll { storedIDs.contains($0) }
        } else {
            favorites.append(skill.id)
        }
    }

    func recordUse(_ skill: Skill) {
        recentNames.removeAll { $0 == skill.id || $0 == skill.legacyID }
        recentNames.insert(skill.id, at: 0)
        recentNames = Array(recentNames.prefix(8))
    }

    func renderedInvocation(for skill: Skill) -> String {
        "\(invocationPrefix)\(skill.displayName) "
    }

    var normalizedTriggers: Set<String> {
        Set(triggerCharacters.map(String.init))
    }

    var normalizedMatchers: [String] {
        bundleMatchers
            .split(separator: ",")
            .map { $0.trimmingCharacters(in: .whitespacesAndNewlines).lowercased() }
            .filter { !$0.isEmpty }
    }

    private func save() {
        defaults.set(triggerCharacters, forKey: Key.triggers)
        defaults.set(codexOnly, forKey: Key.codexOnly)
        defaults.set(bundleMatchers, forKey: Key.matchers)
        defaults.set(invocationPrefix, forKey: Key.prefix)
        defaults.set(showDockIcon, forKey: Key.showDockIcon)
        defaults.set(tags, forKey: Key.tags)
        defaults.set(favorites, forKey: Key.favorites)
        defaults.set(recentNames, forKey: Key.recents)
    }
}
