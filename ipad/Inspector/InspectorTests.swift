import Foundation

@main struct InspectorTests {
    static func main() throws {
        var assertions = 0
        func check(_ ok:Bool,_ message:String) { assertions += 1; if !ok { fatalError(message) } }
        func put(_ value:UInt32,into data:inout Data,at:Int) {
            for i in 0..<4 { data[at+i] = UInt8(truncatingIfNeeded:value >> (i*8)) }
        }
        var fixture = Data(repeating:0,count:80)
        put(0xfeedfacf,into:&fixture,at:0); put(0x0100000c,into:&fixture,at:4)
        put(6,into:&fixture,at:12);put(2,into:&fixture,at:16);put(48,into:&fixture,at:20)
        put(0x32,into:&fixture,at:32);put(24,into:&fixture,at:36);put(2,into:&fixture,at:40)
        put(0x2c,into:&fixture,at:56);put(24,into:&fixture,at:60)
        let plain = try InspectorCore.inspectNative(fixture)
        check(plain.architecture == "arm64" && plain.applePlatform == 2 && !plain.encrypted,"Parse device framework")
        put(1,into:&fixture,at:72)
        check(try InspectorCore.inspectNative(fixture).encrypted,"Detect encrypted framework without modifying it")
        for size in [0,1,8,31,79] {
            do { _ = try InspectorCore.inspectNative(Data(fixture.prefix(size))); fatalError("Accepted truncation") }
            catch { assertions += 1 }
        }
        var invalid = fixture;put(4,into:&invalid,at:60)
        do { _ = try InspectorCore.inspectNative(invalid); fatalError("Accepted invalid command size") }
        catch { assertions += 1 }
        let noGame = InspectorCore.report(note:"fixture")
        let reportData = try JSONEncoder().encode(noGame)
        check(noGame.game == nil && !noGame.modInstalled && !noGame.playableIpadModAvailable,"No fake installer status")
        let text = String(decoding:reportData,as:UTF8.self)
        check(!text.contains(NSHomeDirectory()),"Report must not disclose home path")
        print("PASS: \(assertions) native Mac inspector assertions. Fixtures only; no game or device accessed.")
    }
}
