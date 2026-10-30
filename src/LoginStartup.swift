import Foundation

// A per-user, reversible login job. It runs the installed app, never this source tree.
final class LoginStartup {
    private let url=FileManager.default.homeDirectoryForCurrentUser.appendingPathComponent("Library/LaunchAgents/local.codex.VK03Panel.plist")
    var enabled:Bool { FileManager.default.fileExists(atPath:self.url.path) }
    func setEnabled(_ enabled:Bool) throws {
        if !enabled {
            if self.enabled { try FileManager.default.removeItem(at:self.url) }
            return
        }
        let executable="/Applications/VK03 Panel.app/Contents/MacOS/VK03Panel"
        guard FileManager.default.isExecutableFile(atPath:executable) else {
            throw NSError(domain:"VK03Panel",code:1,userInfo:[NSLocalizedDescriptionKey:"请先将 VK03 Panel 安装到应用程序文件夹。"])
        }
        let logDirectory=FileManager.default.urls(for:.applicationSupportDirectory,in:.userDomainMask)[0].appendingPathComponent("VK03Panel")
        let job:[String:Any]=[
            "Label":"local.codex.VK03Panel",
            "ProgramArguments":[executable,"--background"],
            "RunAtLoad":true,
            "LimitLoadToSessionType":"Aqua",
            "ProcessType":"Background",
            "StandardOutPath":logDirectory.appendingPathComponent("startup.log").path,
            "StandardErrorPath":logDirectory.appendingPathComponent("startup-error.log").path
        ]
        try FileManager.default.createDirectory(at:self.url.deletingLastPathComponent(),withIntermediateDirectories:true)
        try PropertyListSerialization.data(fromPropertyList:job,format:.xml,options:0).write(to:self.url,options:.atomic)
        // Bootstrap from the user's checkbox. An already running instance remains the owner.
        let process=Process();process.executableURL=URL(fileURLWithPath:"/bin/launchctl")
        process.arguments=["bootstrap","gui/\(getuid())",self.url.path]
        process.standardOutput=FileHandle.nullDevice;process.standardError=FileHandle.nullDevice
        try? process.run()
    }
}
