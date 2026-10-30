import AppKit

do {
    if let index=CommandLine.arguments.firstIndex(of:"--demo"),CommandLine.arguments.count>index+1 {
        // Synthetic documentation preview: no hardware, account, or playback data is read.
        var metrics=Metrics()
        metrics.date=Date(timeIntervalSince1970:1793352600)
        metrics.source="DEMO";metrics.cpuModel="Ryzen 7 · Demo"
        metrics.physicalCores=8;metrics.logicalCores=16
        metrics.temperature=48;metrics.frequency=4.65;metrics.cpu=0.23
        metrics.gpu=0.18;metrics.gpuModel="GeForce RTX · Demo"
        metrics.memoryUsed=12.4;metrics.memoryTotal=32
        metrics.memoryModules="2 × 16 GB";metrics.memorySpeed="6000 MT/s"
        let renderer=PanelRenderer()
        renderer.darkMode=CommandLine.arguments.contains("--dark")
        renderer.system.volume=0.42;renderer.system.title="VK03 Panel"
        renderer.system.artist="演示数据";renderer.system.canLock=true
        let (image,_)=renderer.render(metrics,rotation:90)
        PanelRenderer.save(image,to:URL(fileURLWithPath:CommandLine.arguments[index+1]))
        print("Saved synthetic demo preview")
    } else if let index=CommandLine.arguments.firstIndex(of:"--touch-replay"),CommandLine.arguments.count>index+1 {
        let bytes=try Data(contentsOf:URL(fileURLWithPath:CommandLine.arguments[index+1]))
        let input=try JSONSerialization.jsonObject(with:bytes) as! [String:Any]
        let touch=PanelTouch()
        touch.onTouch={ phase,point in print("\(phase) x=\(Int(point.y*959)) y=\(Int((1-point.x)*359))") }
        for report in input["recent"] as! [[UInt8]] { touch.handle(report) }
    } else if let index=CommandLine.arguments.firstIndex(of:"--artwork-probe"),CommandLine.arguments.count>index+3 {
        let cover=AlbumArtwork(),done=DispatchSemaphore(value:0)
        cover.fetch(artist:CommandLine.arguments[index+1],album:CommandLine.arguments[index+2]) { bytes,link in
            if let bytes,let source=CGImageSourceCreateWithData(bytes as CFData,nil),let image=CGImageSourceCreateImageAtIndex(source,0,nil) {
                PanelRenderer.save(image,to:URL(fileURLWithPath:CommandLine.arguments[index+3]));print("Artwork loaded: \(bytes.count) bytes")
            } else { print("No exact album match") }
            done.signal()
        }
        if done.wait(timeout:.now()+25) == .timedOut { throw PanelError("Artwork request timed out") }
    } else if let index=CommandLine.arguments.firstIndex(of:"--render"), CommandLine.arguments.count > index+1 {
        let reader=MetricsReader()
        _=reader.read();Thread.sleep(forTimeInterval:0.3)
        let metrics=reader.read()
        let rotation=CommandLine.arguments.contains("--landscape") ? 90 : 0
        let renderer=PanelRenderer()
        renderer.darkMode=CommandLine.arguments.contains("--dark")
        let (image,_)=renderer.render(metrics,rotation:rotation)
        PanelRenderer.save(image,to:URL(fileURLWithPath:CommandLine.arguments[index+1]))
        print(String(data:try JSONEncoder().encode(metrics),encoding:.utf8)!)
    } else if CommandLine.arguments.contains("--pattern") {
        let panel = try PanelTransport()
        print("Display connected: \(panel.model), \(panel.width)x\(panel.height), MS912C port \(panel.videoPort)")
        try panel.initialize(intermediate: CommandLine.arguments.contains("--intermediate"))
        var bgr = [UInt8](repeating:0,count:panel.width*panel.height*3)
        for y in 0..<panel.height {
            for x in 0..<panel.width {
                let i=(y*panel.width+x)*3
                if x < 18 { bgr[i]=254; bgr[i+1]=254; bgr[i+2]=254 }
                else if y < panel.height/3 { bgr[i+2]=254 }
                else if y < 2*panel.height/3 { bgr[i+1]=254 }
                else { bgr[i]=254 }
            }
        }
        for _ in 0..<10 {
            try panel.send(bgr)
            Thread.sleep(forTimeInterval: 0.7)
        }
        try panel.diagnostics()
        Thread.sleep(forTimeInterval: 3)
        print("Color test frame accepted by the display")
    } else {
        let app=NSApplication.shared
        let delegate=PanelApplication()
        app.delegate=delegate
        withExtendedLifetime(delegate) { app.run() }
    }
} catch {
    fputs("\(error)\n",stderr)
    exit(1)
}
