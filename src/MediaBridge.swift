import Foundation

// Event-driven MediaRemote compatibility helper; one idle process, no polling CLI.
final class MediaBridge {
    var onUpdate:(([String:Any])->Void)?
    private let queue=DispatchQueue(label:"local.codex.VK03Panel.media",qos:.utility)
    private var process:Process?
    private var output:Pipe?
    private var parentPipe:Pipe?
    private var buffer=Data()
    private var attempted=Date.distantPast
    private var stopped=false

    private var paths:(String,String)? {
        guard let resources=Bundle.main.resourceURL,let frameworks=Bundle.main.privateFrameworksURL else { return nil }
        let script=resources.appendingPathComponent("mediaremote-adapter.pl").path
        let framework=frameworks.appendingPathComponent("MediaRemoteAdapter.framework").path
        guard FileManager.default.fileExists(atPath:script),FileManager.default.fileExists(atPath:framework) else { return nil }
        return (script,framework)
    }
    func ensureRunning() {
        self.queue.async { [weak self] in
            guard let self,!self.stopped,self.process?.isRunning != true,Date().timeIntervalSince(self.attempted)>10,let (script,framework)=self.paths else { return }
            self.attempted=Date();self.buffer.removeAll(keepingCapacity:true)
            let process=Process(),output=Pipe(),parent=Pipe()
            process.executableURL=URL(fileURLWithPath:"/usr/bin/perl")
            process.arguments=[script,framework,"stream","--no-diff","--micros","--debounce=180"]
            var env=ProcessInfo.processInfo.environment;env["VK03_PARENT_PIPE"]="1";process.environment=env
            process.standardOutput=output;process.standardError=FileHandle.nullDevice;process.standardInput=parent
            output.fileHandleForReading.readabilityHandler={ [weak self] handle in
                let bytes=handle.availableData
                guard !bytes.isEmpty else { handle.readabilityHandler=nil;return }
                self?.queue.async { self?.consume(bytes) }
            }
            process.terminationHandler={ [weak self] finished in
                self?.queue.async {
                    guard let self,self.process === finished else { return }
                    self.output?.fileHandleForReading.readabilityHandler=nil;self.process=nil;self.output=nil;self.parentPipe=nil
                }
            }
            do { try process.run();self.process=process;self.output=output;self.parentPipe=parent }
            catch { output.fileHandleForReading.readabilityHandler=nil }
        }
    }
    private func consume(_ bytes:Data) {
        self.buffer.append(bytes)
        if self.buffer.count>8_000_000 { self.buffer.removeAll();return }
        while let end=self.buffer.firstIndex(of:10) {
            let line=self.buffer[..<end];self.buffer.removeSubrange(...end)
            if let value=try? JSONSerialization.jsonObject(with:line) as? [String:Any],let payload=value["payload"] as? [String:Any] { self.onUpdate?(payload) }
        }
    }
    func send(_ command:Int32) {
        guard [2,4,5].contains(command) else { return }
        self.queue.async { [weak self] in
            guard let self,let (script,framework)=self.paths else { return }
            let commandProcess=Process();commandProcess.executableURL=URL(fileURLWithPath:"/usr/bin/perl")
            commandProcess.arguments=[script,framework,"send",String(command)]
            commandProcess.standardOutput=FileHandle.nullDevice;commandProcess.standardError=FileHandle.nullDevice
            try? commandProcess.run()
        }
    }
    func restart() {
        self.queue.async {
            if self.process?.isRunning==true { self.process?.terminate() }
            self.attempted = .distantPast
        }
    }
    func stop() {
        self.queue.sync {
            self.stopped=true;self.output?.fileHandleForReading.readabilityHandler=nil
            try? self.parentPipe?.fileHandleForWriting.close()
            if self.process?.isRunning==true { self.process?.terminate() }
            self.parentPipe=nil
        }
    }
}
