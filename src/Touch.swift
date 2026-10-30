import Foundation
import IOKit
import IOKit.hid

struct TouchPoint { let x:Double;let y:Double }

final class PanelTouch {
    enum Phase { case down,move,up,cancel }
    var onTouch:((Phase,TouchPoint)->Void)?
    private let manager=IOHIDManagerCreate(kCFAllocatorDefault,0)
    private var device:IOHIDDevice?
    private let buffer=UnsafeMutablePointer<UInt8>.allocate(capacity:64)
    private var active:TouchPoint?
    private var recent=[[UInt8]]()
    private var raw:RawTouchReader?
    private var watchdog:Timer?
    private var running=false
    private var backend="等待连接"
    private var lastError=""
    private var lastReport=Date.distantPast
    private var attempts=0
    private(set) var reports=0
    private(set) var connected=false
    private let directory=FileManager.default.urls(for:.applicationSupportDirectory,in:.userDomainMask)[0].appendingPathComponent("VK03Panel")

    init() { self.buffer.initialize(repeating:0,count:64) }
    deinit { self.stop();self.buffer.deallocate() }

    func start() {
        guard !self.running else { return };self.running=true
        IOHIDManagerSetDeviceMatching(self.manager,[kIOHIDVendorIDKey:0x374a,kIOHIDProductIDKey:0xa401] as CFDictionary)
        let context=Unmanaged.passUnretained(self).toOpaque()
        IOHIDManagerRegisterDeviceMatchingCallback(self.manager,{ context,_,_,device in
            guard let context else { return };Unmanaged<PanelTouch>.fromOpaque(context).takeUnretainedValue().attach(device)
        },context)
        IOHIDManagerRegisterDeviceRemovalCallback(self.manager,{ context,_,_,device in
            guard let context else { return };let object=Unmanaged<PanelTouch>.fromOpaque(context).takeUnretainedValue()
            if object.device==device { object.closeHID();object.cancelGesture();object.connected=false;object.connectIfNeeded() }
        },context)
        IOHIDManagerScheduleWithRunLoop(self.manager,CFRunLoopGetMain(),CFRunLoopMode.commonModes.rawValue)
        _=IOHIDManagerOpen(self.manager,0)
        self.connectIfNeeded()
        let timer=Timer(timeInterval:3,repeats:true) { [weak self] _ in self?.connectIfNeeded();self?.writeStatus() }
        RunLoop.main.add(timer,forMode:.common);self.watchdog=timer
        self.writeStatus()
    }
    func stop() {
        guard self.running else { return };self.running=false
        self.watchdog?.invalidate();self.watchdog=nil
        self.raw?.stop();self.raw=nil;self.cancelGesture();self.closeHID()
        IOHIDManagerUnscheduleFromRunLoop(self.manager,CFRunLoopGetMain(),CFRunLoopMode.commonModes.rawValue)
        IOHIDManagerClose(self.manager,0);self.connected=false
    }
    private func closeHID() {
        if let device {
            IOHIDDeviceUnscheduleFromRunLoop(device,CFRunLoopGetMain(),CFRunLoopMode.defaultMode.rawValue)
            IOHIDDeviceClose(device,IOOptionBits(kIOHIDOptionsTypeSeizeDevice))
        }
        self.device=nil
    }
    private func cancelGesture() {
        if let active { self.onTouch?(.cancel,active) };self.active=nil
    }
    private func connectIfNeeded() {
        guard self.running,self.device==nil else { return }
        if self.backend != "USB 直读",let devices=IOHIDManagerCopyDevices(self.manager) as? Set<IOHIDDevice>,let device=devices.first { self.attach(device) }
        if self.device==nil,self.raw==nil {
            let raw=RawTouchReader()
            raw.onReport={ [weak self] report in self?.receive(report) }
            raw.onState={ [weak self] connected,message in
                guard let self else { return }
                if self.device==nil {
                    if !connected,self.connected { self.cancelGesture() }
                    self.connected=connected;self.backend=connected ? "USB 直读":"等待连接";self.lastError=connected ? "":message;self.writeStatus()
                }
            }
            self.raw=raw;raw.start()
        }
    }
    private func attach(_ device:IOHIDDevice) {
        guard self.running,self.device==nil,self.backend != "USB 直读" else { return }
        self.attempts += 1
        let result=IOHIDDeviceOpen(device,IOOptionBits(kIOHIDOptionsTypeSeizeDevice))
        guard result==kIOReturnSuccess else { self.lastError=String(format:"HID %08x",result);return }
        self.raw?.stop();self.raw=nil;self.cancelGesture()
        self.device=device;self.connected=true;self.backend="HID";self.lastError=""
        IOHIDDeviceRegisterInputReportCallback(device,self.buffer,64,{ context,result,_,_,reportID,bytes,count in
            guard let context else { return };let object=Unmanaged<PanelTouch>.fromOpaque(context).takeUnretainedValue()
            guard result==kIOReturnSuccess else { object.closeHID();object.cancelGesture();object.connected=false;return }
            var data=Array(UnsafeBufferPointer(start:bytes,count:count));if count==31,reportID==1 { data.insert(1,at:0) }
            object.receive(data)
        },Unmanaged.passUnretained(self).toOpaque())
        IOHIDDeviceScheduleWithRunLoop(device,CFRunLoopGetMain(),CFRunLoopMode.defaultMode.rawValue)
        self.writeStatus()
    }
    private func receive(_ data:[UInt8]) {
        self.reports += 1;self.lastReport=Date();self.recent.append(data)
        if self.recent.count>16 { self.recent.removeFirst() }
        if self.reports<10 || (data.count>1 && data[1]&1==0) || self.reports%100==0,
           let bytes=try? JSONSerialization.data(withJSONObject:["backend":self.backend,"reports":self.reports,"recent":self.recent,"timestamp":Date().timeIntervalSince1970]) {
            try? bytes.write(to:self.directory.appendingPathComponent("touch-raw.json"),options:.atomic)
        }
        self.handle(data)
    }
    private func writeStatus() {
        let value:[String:Any]=["pid":getpid(),"connected":self.connected,"backend":self.backend,"reports":self.reports,"attempts":self.attempts,
            "lastReport":self.lastReport.timeIntervalSince1970,"error":self.lastError,"timestamp":Date().timeIntervalSince1970]
        if let bytes=try? JSONSerialization.data(withJSONObject:value) { try? bytes.write(to:self.directory.appendingPathComponent("touch-status.json"),options:.atomic) }
    }
    func handle(_ bytes:[UInt8]) {
        guard bytes.count>=31,bytes[0]==1 else { return }
        // Some firmware leaves count=0 and secondary slots stale. Use the primary
        // contact's tip flag, which also works on the unmodified USB reports.
        if bytes[1]&1 != 0 {
            let x=Int(bytes[3]) | Int(bytes[4])<<8,y=Int(bytes[5]) | Int(bytes[6])<<8
            guard x<=4096,y<=4096 else { return }
            let point=TouchPoint(x:Double(x)/4096,y:Double(y)/4096)
            self.onTouch?(self.active==nil ? .down:.move,point);self.active=point
        } else if let previous=self.active { self.active=nil;self.onTouch?(.up,previous) }
    }
}
