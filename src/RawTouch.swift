import Foundation
import CUSB

// Used only when macOS has not attached a HID service to the dedicated screen.
// Claims its otherwise unowned interface; never detaches a driver or resets USB.
final class RawTouchReader {
    var onReport:(([UInt8])->Void)?
    var onState:((Bool,String)->Void)?
    private let queue=DispatchQueue(label:"local.codex.VK03Panel.raw-touch",qos:.utility)
    private let lock=NSLock()
    private var cancelled=false
    private var shouldStop:Bool { self.lock.lock();defer { self.lock.unlock() };return self.cancelled }
    func start() { self.queue.async { [weak self] in self?.run() } }
    func stop() { self.lock.lock();self.cancelled=true;self.lock.unlock() }
    private func state(_ connected:Bool,_ message:String) {
        DispatchQueue.main.async { [weak self] in guard let self,!self.shouldStop else { return };self.onState?(connected,message) }
    }
    private func run() {
        var context:OpaquePointer?
        guard libusb_init(&context)==0 else { self.state(false,"USB 初始化失败");return }
        defer { libusb_exit(context) }
        while !self.shouldStop {
            guard let device=libusb_open_device_with_vid_pid(context,0x374a,0xa401) else {
                self.state(false,"等待触屏 USB");Thread.sleep(forTimeInterval:2);continue
            }
            let active=libusb_kernel_driver_active(device,0)
            let claimed=active==0 ? libusb_claim_interface(device,0):Int32(LIBUSB_ERROR_BUSY.rawValue)
            if claimed != 0 {
                libusb_close(device);self.state(false,"等待触屏接口 \(claimed)");Thread.sleep(forTimeInterval:2);continue
            }
            self.state(true,"USB 直读")
            var bytes=[UInt8](repeating:0,count:64)
            var previous=[UInt8]()
            while !self.shouldStop {
                var length:Int32=0
                let result=bytes.withUnsafeMutableBufferPointer { libusb_interrupt_transfer(device,0x81,$0.baseAddress,64,&length,200) }
                if result==Int32(LIBUSB_ERROR_TIMEOUT.rawValue) { continue }
                if result != 0 { self.state(false,"USB 重连 \(result)");break }
                guard length>=31 else { continue }
                var report=Array(bytes.prefix(Int(length)))
                if report.count==31 { report.insert(1,at:0) }
                guard report != previous else { continue }
                previous=report
                DispatchQueue.main.async { [weak self] in guard let self,!self.shouldStop else { return };self.onReport?(report) }
            }
            _=libusb_release_interface(device,0);libusb_close(device)
            if !self.shouldStop { Thread.sleep(forTimeInterval:1) }
        }
    }
}
