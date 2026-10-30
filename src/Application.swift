import AppKit

final class PanelApplication:NSObject,NSApplicationDelegate {
    private let preferences=UserDefaults.standard
    private let queue=DispatchQueue(label:"local.codex.VK03Panel.transport",qos:.utility)
    private let reader=MetricsReader()
    private let renderer=PanelRenderer()
    private let system=SystemState()
    private let touch=PanelTouch()
    private let startup=LoginStartup()
    private var panel:PanelTransport?
    private var timer:DispatchSourceTimer?
    private var statusItem:NSStatusItem?
    private var window:NSWindow?
    private var preview:NSImageView?
    private var statusLabel:NSTextField?
    private var rotations:NSPopUpButton?
    private var loginCheckbox:NSButton?
    private var appearanceObservation:NSKeyValueObservation?
    private var frames=0
    private var previousError=""
    private var lastStatsLaunch=Date.distantPast
    private var lockTimer:Timer?
    private var pressed:TouchAction?
    private var downPoint=CGPoint.zero
    private var touchCount=0
    private var wakeObserver:NSObjectProtocol?
    private var lastInteractionDraw=Date.distantPast
    private let dataDirectory=FileManager.default.urls(for:.applicationSupportDirectory,in:.userDomainMask)[0].appendingPathComponent("VK03Panel",isDirectory:true)
    private enum TouchAction:Equatable { case mute,quieter,volume,louder,previous,play,next,lock }

    func applicationDidFinishLaunching(_ notification:Notification) {
        let others=NSRunningApplication.runningApplications(withBundleIdentifier:"local.codex.VK03Panel").filter { $0.processIdentifier != getpid() }
        if !others.isEmpty { NSApp.terminate(nil);return }
        NSApp.setActivationPolicy(.accessory)
        let main=NSMenu(),appItem=NSMenuItem(),appMenu=NSMenu()
        let quitItem=NSMenuItem(title:"退出副屏监控",action:#selector(self.quit),keyEquivalent:"q");quitItem.target=self
        appMenu.addItem(quitItem);appItem.submenu=appMenu;main.addItem(appItem);NSApp.mainMenu=main
        self.preferences.register(defaults:["rotation":90])
        try? FileManager.default.createDirectory(at:self.dataDirectory,withIntermediateDirectories:true)
        let item=NSStatusBar.system.statusItem(withLength:NSStatusItem.squareLength)
        if let icon=NSImage(systemSymbolName:"display",accessibilityDescription:"VK03 副屏监控")?.withSymbolConfiguration(.init(pointSize:16,weight:.medium)) {
            icon.isTemplate=true;item.button?.image=icon
        }
        item.button?.toolTip="VK03 副屏监控"
        let menu=NSMenu()
        let settings=NSMenuItem(title:"副屏设置…",action:#selector(self.showWindow),keyEquivalent:"");settings.target=self;menu.addItem(settings)
        menu.addItem(.separator())
        let quit=NSMenuItem(title:"退出副屏监控",action:#selector(self.quit),keyEquivalent:"");quit.target=self;menu.addItem(quit)
        item.menu=menu;self.statusItem=item
        self.makeWindow()
        self.appearanceObservation=NSApp.observe(\.effectiveAppearance,options:[.initial,.new]) { [weak self] _,_ in self?.applyAppearance() }
        DistributedNotificationCenter.default().addObserver(self,selector:#selector(self.applyAppearance),name:NSNotification.Name("AppleInterfaceThemeChangedNotification"),object:nil)
        self.touch.onTouch={ [weak self] phase,point in self?.handleTouch(phase,point) }
        self.touch.start()
        self.wakeObserver=NSWorkspace.shared.notificationCenter.addObserver(forName:NSWorkspace.didWakeNotification,object:nil,queue:.main) { [weak self] _ in
            self?.system.didWake()
        }
        if !CommandLine.arguments.contains("--background") { self.showWindow() }
        let timer=DispatchSource.makeTimerSource(queue:self.queue)
        timer.schedule(deadline:.now(),repeating:1,leeway:.milliseconds(80));timer.setEventHandler { [weak self] in self?.update() }
        self.timer=timer;timer.resume()
    }

    private func makeWindow() {
        let window=NSWindow(contentRect:NSRect(x:0,y:0,width:1000,height:500),styleMask:[.titled,.closable,.miniaturizable],backing:.buffered,defer:false)
        window.title="VK03 副屏监控";window.isReleasedWhenClosed=false;window.center()
        let content=window.contentView!
        let glass=NSGlassEffectView(frame:NSRect(x:20,y:438,width:960,height:46));glass.cornerRadius=18;glass.style = .regular
        let controls=NSView(frame:NSRect(x:0,y:0,width:960,height:46));glass.contentView=controls;content.addSubview(glass)
        let picker=NSPopUpButton(frame:NSRect(x:12,y:9,width:158,height:28));picker.addItems(withTitles:["方向  0°","方向  90°","方向  180°","方向  270°"])
        picker.selectItem(at:[0,90,180,270].firstIndex(of:self.preferences.integer(forKey:"rotation")) ?? 1)
        picker.target=self;picker.action=#selector(self.rotate);controls.addSubview(picker);self.rotations=picker
        let label=NSTextField(labelWithString:"正在连接副屏…");label.frame=NSRect(x:190,y:12,width:400,height:22)
        label.font = .systemFont(ofSize:12);label.textColor = .secondaryLabelColor;controls.addSubview(label);self.statusLabel=label
        let theme=NSTextField(labelWithString:"外观跟随系统");theme.frame=NSRect(x:823,y:12,width:120,height:22)
        theme.font = .systemFont(ofSize:12);theme.textColor = .secondaryLabelColor;controls.addSubview(theme)
        let image=NSImageView(frame:NSRect(x:20,y:64,width:960,height:360));image.imageScaling = .scaleProportionallyUpOrDown
        content.addSubview(image);self.preview=image
        let checkbox=NSButton(checkboxWithTitle:"登录后自动启动",target:self,action:#selector(self.toggleStartup));checkbox.frame=NSRect(x:22,y:20,width:180,height:24)
        checkbox.state=self.startup.enabled ? .on:.off;content.addSubview(checkbox);self.loginCheckbox=checkbox
        let footer=NSTextField(labelWithString:"关闭窗口后继续运行 · 音量可拖动 · 按住 1.2 秒锁屏")
        footer.frame=NSRect(x:415,y:21,width:565,height:20);footer.alignment = .right
        footer.font = .systemFont(ofSize:12);footer.textColor = .secondaryLabelColor;content.addSubview(footer)
        self.window=window
    }

    @objc private func applyAppearance() {
        let dark=NSApp.effectiveAppearance.bestMatch(from:[.aqua,.darkAqua]) == .darkAqua
        self.queue.async { self.renderer.darkMode=dark }
    }

    private func update() {
        autoreleasepool {
            let metrics=self.reader.read()
            if !metrics.hasStats,Date().timeIntervalSince(self.lastStatsLaunch)>15 {
                self.lastStatsLaunch=Date()
                DispatchQueue.main.async {
                    if NSRunningApplication.runningApplications(withBundleIdentifier:"eu.exelban.Stats").isEmpty {
                        let config=NSWorkspace.OpenConfiguration();config.activates=false
                        NSWorkspace.shared.openApplication(at:URL(fileURLWithPath:"/Applications/Stats.app"),configuration:config)
                    }
                }
            }
            self.system.refresh();self.renderer.system=self.system.snapshot()
            let (image,pixels)=self.renderer.render(metrics,rotation:self.preferences.integer(forKey:"rotation"))
            var status=metrics.hasStats ? "已连接 · Stats 实时数据":"已连接 · 等待 Stats 数据"
            do {
                if self.panel==nil { let connected=try PanelTransport();try connected.initialize(intermediate:true);self.panel=connected }
                try self.panel?.send(pixels);self.frames += 1;self.previousError=""
            } catch {
                self.panel=nil;status="等待副屏 · 自动重连"
                let message=String(describing:error)
                if message != self.previousError { fputs(message+"\n",stderr);self.previousError=message }
            }
            let encoder=JSONEncoder();encoder.dateEncodingStrategy = .iso8601
            if let encoded=try? encoder.encode(metrics) { try? encoded.write(to:self.dataDirectory.appendingPathComponent("metrics.json"),options:.atomic) }
            if self.frames % 15==1 { PanelRenderer.save(image,to:self.dataDirectory.appendingPathComponent("preview.png")) }
            if self.frames % 3==0 {
                let media=self.renderer.system
                let state:[String:Any]=["title":media.title,"artist":media.artist,"album":media.album,"playing":media.playing,
                    "artwork":media.artwork != nil,"artworkSource":media.artworkSource,"timestamp":Date().timeIntervalSince1970]
                if let bytes=try? JSONSerialization.data(withJSONObject:state) { try? bytes.write(to:self.dataDirectory.appendingPathComponent("media.json"),options:.atomic) }
            }
            DispatchQueue.main.async {
                if self.window?.isVisible==true { self.preview?.image=NSImage(cgImage:image,size:NSSize(width:image.width,height:image.height)) }
                self.statusLabel?.stringValue=status+(self.touch.connected ? " · 触屏已连接":" · 等待触屏")
            }
        }
    }

    private func logicalPoint(_ point:TouchPoint)->CGPoint {
        switch self.preferences.integer(forKey:"rotation") {
        case 270:return CGPoint(x:(1-point.y)*959,y:point.x*359)
        case 180:return CGPoint(x:(1-point.y)*959,y:point.x*359)
        default:return CGPoint(x:point.y*959,y:(1-point.x)*359)
        }
    }
    private func action(at point:CGPoint)->TouchAction? {
        for (rect,action) in [(PanelLayout.mute,TouchAction.mute),(PanelLayout.quieter,.quieter),(PanelLayout.volume,.volume),
            (PanelLayout.louder,.louder),(PanelLayout.previous,.previous),(PanelLayout.play,.play),(PanelLayout.next,.next),(PanelLayout.lock,.lock)] {
            if rect.contains(point) { return action }
        }
        return nil
    }
    private func handleTouch(_ phase:PanelTouch.Phase,_ raw:TouchPoint) {
        let point=self.logicalPoint(raw)
        switch phase {
        case .down:
            self.pressed=self.action(at:point);self.downPoint=point
            self.drawInteraction(at:point,force:true)
            if self.pressed == .volume { self.changeVolume(at:point) }
            if self.pressed == .lock,self.system.snapshot().canLock {
                let start=Date()
                self.lockTimer?.invalidate()
                self.lockTimer=Timer.scheduledTimer(withTimeInterval:0.08,repeats:true) { [weak self] timer in
                    guard let self,self.pressed == .lock else { timer.invalidate();return }
                    let progress=Date().timeIntervalSince(start)/1.2
                    self.queue.async { self.renderer.lockProgress=progress }
                    if progress>=1 { timer.invalidate();self.pressed=nil;self.queue.async { self.renderer.lockProgress=0 };self.system.lockScreen() }
                }
            }
        case .move:
            if self.pressed == .volume { self.changeVolume(at:point);self.drawInteraction(at:point) }
            else if hypot(point.x-self.downPoint.x,point.y-self.downPoint.y)>44 || self.action(at:point) != self.pressed {
                self.pressed=nil;self.lockTimer?.invalidate();self.queue.async { self.renderer.lockProgress=0 }
            }
        case .up:
            self.lockTimer?.invalidate();self.queue.async { self.renderer.lockProgress=0 }
            if let action=self.pressed,self.action(at:point)==action {
                switch action {
                case .mute:self.system.toggleMute()
                case .quieter:self.system.adjustVolume(-0.0625)
                case .louder:self.system.adjustVolume(0.0625)
                case .previous:self.system.media(5)
                case .play:self.system.media(2)
                case .next:self.system.media(4)
                case .volume,.lock:break
                }
            }
            self.touchCount += 1
            let diagnostic:[String:Any]=["timestamp":Date().timeIntervalSince1970,"x":point.x,"y":point.y,"taps":self.touchCount,
                "action":self.pressed.map { String(describing:$0) } ?? "none","volume":self.system.snapshot().volume ?? -1]
            if let bytes=try? JSONSerialization.data(withJSONObject:diagnostic) { try? bytes.write(to:self.dataDirectory.appendingPathComponent("touch.json"),options:.atomic) }
            self.pressed=nil
            self.drawInteraction(at:point,force:true)
        case .cancel:
            self.lockTimer?.invalidate();self.pressed=nil
            self.queue.async { self.renderer.lockProgress=0;self.renderer.pressedRect=nil }
        }
    }
    private func drawInteraction(at point:CGPoint,force:Bool=false) {
        guard force || Date().timeIntervalSince(self.lastInteractionDraw)>0.2 else { return }
        self.lastInteractionDraw=Date()
        let selected=self.pressed == nil ? nil:[PanelLayout.mute,PanelLayout.quieter,PanelLayout.volume,PanelLayout.louder,
            PanelLayout.previous,PanelLayout.play,PanelLayout.next,PanelLayout.lock].first { $0.contains(point) }
        self.queue.async { self.renderer.pressedRect=selected;self.update() }
    }
    private func changeVolume(at point:CGPoint) { self.system.setVolume(Double((point.x-PanelLayout.slider.minX)/PanelLayout.slider.width)) }

    @objc private func toggleStartup() {
        do { try self.startup.setEnabled(self.loginCheckbox?.state == .on) }
        catch {
            self.loginCheckbox?.state=self.startup.enabled ? .on:.off
            let alert=NSAlert();alert.messageText="无法更新登录启动设置";alert.informativeText=error.localizedDescription
            if let window=self.window { alert.beginSheetModal(for:window) }
        }
    }
    @objc private func rotate() { self.preferences.set([0,90,180,270][self.rotations?.indexOfSelectedItem ?? 1],forKey:"rotation") }
    @objc private func showWindow() { self.window?.makeKeyAndOrderFront(nil);NSApp.activate(ignoringOtherApps:true) }
    @objc private func quit() { NSApp.terminate(nil) }
    func applicationShouldHandleReopen(_ sender:NSApplication,hasVisibleWindows:Bool)->Bool { self.showWindow();return true }
    func applicationWillTerminate(_ notification:Notification) {
        self.lockTimer?.invalidate();self.timer?.cancel();self.touch.onTouch=nil
        self.touch.stop()
        if let wakeObserver { NSWorkspace.shared.notificationCenter.removeObserver(wakeObserver) }
        self.system.stop()
        self.queue.sync { self.panel?.turnOff();self.panel=nil }
    }
}
