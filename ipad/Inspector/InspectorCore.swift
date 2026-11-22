import Foundation
import CryptoKit
import Darwin

enum InspectionError: Error, LocalizedError {
    case invalid(String)
    var errorDescription: String? { if case .invalid(let text) = self { return text }; return nil }
}
struct NativeInfo: Encodable, Sendable {
    let architecture: String
    let applePlatform: UInt32?
    let encrypted: Bool
    let sha256: String
    let bytes: Int
}
struct GameInfo: Encodable, Sendable {
    let version: String?
    let build: String?
    let framework: NativeInfo
    let metadataVersion: UInt32
    let metadataSHA256: String
    let compatibleBindingsAvailable: Bool
}
struct InspectionReport: Encodable, Sendable {
    let schema = "forger.ipad-mac-inspection.v1"
    let macProcessor: String
    let macOS: String
    let game: GameInfo?
    let note: String
    let modInstalled = false
    let playableIpadModAvailable = false
}
enum InspectorCore {
    static func digest(_ data: Data) -> String { SHA256.hash(data: data).map { String(format: "%02x", $0) }.joined() }
    static func integer(_ data: Data, _ offset: Int, big: Bool = false) throws -> UInt32 {
        guard offset >= 0, offset <= data.count - 4 else { throw InspectionError.invalid("The native file is truncated.") }
        let b = Array(data[offset..<(offset+4)])
        return big ? UInt32(b[0])<<24 | UInt32(b[1])<<16 | UInt32(b[2])<<8 | UInt32(b[3]) : UInt32(b[3])<<24 | UInt32(b[2])<<16 | UInt32(b[1])<<8 | UInt32(b[0])
    }
    static func inspectNative(_ data: Data) throws -> NativeInfo {
        guard data.count >= 32 else { throw InspectionError.invalid("The Unity framework is too small.") }
        var slice = data
        let magic = try integer(data, 0, big: true)
        if magic == 0xcafebabe {
            let count = try integer(data, 4, big: true)
            guard count > 0, count <= 16, data.count >= 8 + Int(count)*20 else { throw InspectionError.invalid("Invalid universal framework header.") }
            var arm: Range<Int>?
            for n in 0..<Int(count) {
                let at = 8+n*20
                if try integer(data, at, big: true) == 0x0100000c {
                    let offset = Int(try integer(data, at+8, big: true)), size = Int(try integer(data, at+12, big: true))
                    guard size >= 32, offset >= 8+Int(count)*20, offset <= data.count, size <= data.count-offset else { throw InspectionError.invalid("Invalid ARM64 framework slice.") }
                    arm = offset..<(offset+size)
                }
            }
            guard let arm else { throw InspectionError.invalid("The selected framework has no ARM64 version.") }
            slice = data.subdata(in: arm)
        }
        guard try integer(slice, 0) == 0xfeedfacf, try integer(slice, 4) == 0x0100000c else { throw InspectionError.invalid("This is not an ARM64 Apple game framework.") }
        let commands = Int(try integer(slice, 16)), size = Int(try integer(slice, 20))
        guard commands <= 4096, size <= slice.count-32 else { throw InspectionError.invalid("Invalid native load commands.") }
        var at = 32, encrypted = false
        var platform: UInt32?
        for _ in 0..<commands {
            guard at+8 <= 32+size else { throw InspectionError.invalid("Truncated native load command.") }
            let command = try integer(slice, at), length = Int(try integer(slice, at+4))
            guard length >= 8, length <= 32+size-at else { throw InspectionError.invalid("Invalid native load command length.") }
            if command == 0x21 || command == 0x2c {
                guard length >= 20 else { throw InspectionError.invalid("Invalid encryption record.") }
                let cryptID = try integer(slice, at+16)
                encrypted = encrypted || cryptID != 0
            }
            if command == 0x32 {
                guard length >= 24 else { throw InspectionError.invalid("Invalid platform record.") }
                platform = try integer(slice, at+8)
            } else if command == 0x25 && platform == nil { platform = 2 }
            at += length
        }
        guard at == 32+size else { throw InspectionError.invalid("The load-command table is inconsistent.") }
        return NativeInfo(architecture: "arm64", applePlatform: platform, encrypted: encrypted, sha256: digest(data), bytes: data.count)
    }
    static func findGame(_ input: URL) throws -> URL {
        let manager = FileManager.default
        let direct = input.appendingPathComponent("Frameworks/UnityFramework.framework/UnityFramework")
        if manager.fileExists(atPath: direct.path) { return input }
        // App Store iPad apps on Apple Silicon can be inside a macOS wrapper.
        for sub in ["Wrapper", "Contents/Wrapper"] {
            let folder = input.appendingPathComponent(sub)
            if let children = try? manager.contentsOfDirectory(at: folder, includingPropertiesForKeys: nil) {
                for child in children where child.pathExtension == "app" {
                    if manager.fileExists(atPath: child.appendingPathComponent("Frameworks/UnityFramework.framework/UnityFramework").path) { return child }
                }
            }
        }
        throw InspectionError.invalid("No iPad Unity game was found in that application. Select Among Us from Applications, if it is installed on this Mac.")
    }
    static func inspectGame(_ input: URL) throws -> GameInfo {
        let app = try findGame(input)
        let bytes = try Data(contentsOf: app.appendingPathComponent("Info.plist"))
        guard let info = try PropertyListSerialization.propertyList(from: bytes, format: nil) as? [String:Any],
              info["CFBundleIdentifier"] as? String == "com.innersloth.spacemafia" else { throw InspectionError.invalid("Please select the official Among Us application.") }
        let native = try inspectNative(Data(contentsOf: app.appendingPathComponent("Frameworks/UnityFramework.framework/UnityFramework"), options: .mappedIfSafe))
        let metadata = try Data(contentsOf: app.appendingPathComponent("Data/Managed/Metadata/global-metadata.dat"), options: .mappedIfSafe)
        guard try integer(metadata, 0) == 0xfab11baf else { throw InspectionError.invalid("The IL2CPP metadata header is not recognised.") }
        return GameInfo(version: info["CFBundleShortVersionString"] as? String, build: info["CFBundleVersion"] as? String, framework: native, metadataVersion: try integer(metadata,4), metadataSHA256: digest(metadata), compatibleBindingsAvailable: false)
    }
    static func report(game: GameInfo? = nil, note: String) -> InspectionReport {
        var arm: Int32 = 0, length = MemoryLayout<Int32>.size
        let result = sysctlbyname("hw.optional.arm64", &arm, &length, nil, 0)
        return InspectionReport(macProcessor: result == 0 && arm == 1 ? "Apple Silicon" : "Intel or unknown", macOS: ProcessInfo.processInfo.operatingSystemVersionString, game: game, note: note)
    }
    static func saveBuildInputs(_ input: URL, to destination: URL) throws {
        let app = try findGame(input), game = try inspectGame(input)
        guard !game.framework.encrypted, game.framework.applePlatform == 2 || game.framework.applePlatform == nil else {
            throw InspectionError.invalid("These are not usable, unencrypted iOS development inputs. No files were exported.")
        }
        let sourceRoot = app.resolvingSymlinksInPath()
        guard !destination.resolvingSymlinksInPath().path.hasPrefix(sourceRoot.path+"/") else {
            throw InspectionError.invalid("Save the ZIP outside the original game application.")
        }
        let manager = FileManager.default
        let temporary = manager.temporaryDirectory.appendingPathComponent("Forger-iPad-Inputs-"+UUID().uuidString,isDirectory:true)
        try manager.createDirectory(at:temporary,withIntermediateDirectories:false)
        defer {
            if temporary.lastPathComponent.hasPrefix("Forger-iPad-Inputs-") && temporary.deletingLastPathComponent().standardizedFileURL == manager.temporaryDirectory.standardizedFileURL {
                try? manager.removeItem(at:temporary)
            }
        }
        // Exact allowlist: no Data folders, accounts, receipts, profiles or keys.
        let files = [
            ("Frameworks/UnityFramework.framework/UnityFramework","UnityFramework"),
            ("Data/Managed/Metadata/global-metadata.dat","global-metadata.dat"),
            ("Info.plist","Game-Info.plist")
        ]
        for (source,name) in files {
            let file = app.appendingPathComponent(source).resolvingSymlinksInPath()
            guard file.path.hasPrefix(sourceRoot.path+"/") else { throw InspectionError.invalid("A build input points outside the selected game application.") }
            try manager.copyItem(at:file,to:temporary.appendingPathComponent(name))
        }
        let encoder = JSONEncoder(); encoder.outputFormatting = [.prettyPrinted,.sortedKeys]
        try encoder.encode(report(game:game,note:"Private native build inputs only. Not an installer; do not publish.")).write(to:temporary.appendingPathComponent("build-report.json"))
        try "Private development inputs from the selected local game. Not an installer. Do not publish game files. No account or signing data was collected.\n".write(to:temporary.appendingPathComponent("PRIVATE-READ-ME.txt"),atomically:true,encoding:.utf8)
        // Build the zip separately, then copy it only after successful completion.
        let archive = temporary.appendingPathComponent("inputs.zip")
        let process = Process(); process.executableURL = URL(fileURLWithPath:"/usr/bin/zip")
        process.currentDirectoryURL = temporary
        process.arguments = ["-q",archive.path]+files.map{$0.1}+["build-report.json","PRIVATE-READ-ME.txt"]
        let diagnostic = Pipe();process.standardError = diagnostic;process.standardOutput = FileHandle.nullDevice
        try process.run();process.waitUntilExit()
        guard process.terminationStatus == 0 else { throw InspectionError.invalid("The private archive could not be created.") }
        let archiveData = try Data(contentsOf:archive,options:.mappedIfSafe)
        try archiveData.write(to:destination,options:.atomic)
    }
}
