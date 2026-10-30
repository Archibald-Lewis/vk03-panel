import Foundation
import IOKit
import IOKit.hid
import CUSB

struct PanelError: Error, CustomStringConvertible {
    let description: String
    init(_ message: String) { self.description = message }
}

// MS912C HID/video protocol, checked against codehz/msdisplay's documented
// Myth.cool transport. Uses the existing vendor HID device and interface 3;
// does not detach system drivers, reset USB devices, or flash firmware.
final class PanelTransport {
    private var manager: IOHIDManager
    private var hid: IOHIDDevice!
    private var closed = false
    private var context: OpaquePointer?
    private var handle: OpaquePointer?
    private var claimed = false
    private var enabled = false
    private var frameID: UInt8 = 1
    private(set) var videoPort: UInt8 = 0
    private(set) var model = ""
    let width = 360
    let height = 960

    init() throws {
        self.manager = IOHIDManagerCreate(kCFAllocatorDefault, 0)
        var complete = false
        defer { if !complete { self.close() } }
        IOHIDManagerSetDeviceMatching(self.manager,
            [kIOHIDVendorIDKey: 0x345f, kIOHIDProductIDKey: 0x9132] as CFDictionary)
        guard IOHIDManagerOpen(self.manager, 0) == kIOReturnSuccess,
              let devices = IOHIDManagerCopyDevices(self.manager) as? Set<IOHIDDevice>,
              let device = devices.first else { throw PanelError("VK03 display not connected") }
        self.hid = device
        guard IOHIDDeviceOpen(device, 0) == kIOReturnSuccess else { throw PanelError("Cannot open display HID") }
        guard libusb_init(&self.context) == 0 else { throw PanelError("Cannot initialize USB transport") }
        guard let opened = libusb_open_device_with_vid_pid(self.context, 0x345f, 0x9132) else {
            throw PanelError("Cannot open USB video device")
        }
        self.handle = opened
        let result = libusb_claim_interface(opened, 3)
        guard result == 0 else { throw PanelError("Cannot claim video interface: \(result)") }
        self.claimed = true
        _ = libusb_clear_halt(opened, 0x04)

        let chip = try self.read(0xf000)
        guard Array(chip.prefix(3)) == [0xb7, 0x16, 0x0a] else {
            throw PanelError("Unexpected display chip; initialization stopped")
        }
        self.videoPort = try self.read(0x0030)[1]
        var edid = [UInt8]()
        for offset in stride(from: 0, to: 128, by: 4) {
            edid += try self.read(UInt16(0xc000 + offset))
        }
        guard edid.prefix(8).elementsEqual([0,255,255,255,255,255,255,0]),
              edid.reduce(0, { $0 + Int($1) }) % 256 == 0 else { throw PanelError("Invalid panel EDID") }
        let w = Int(edid[56]) + ((Int(edid[58]) & 0xf0) << 4)
        let h = Int(edid[59]) + ((Int(edid[61]) & 0xf0) << 4)
        guard w == self.width, h == self.height else {
            throw PanelError("Unsupported panel size \(w)x\(h)")
        }
        for offset in stride(from: 54, to: 126, by: 18) where edid[offset+3] == 0xfc {
            self.model = String(bytes: edid[(offset+5)..<(offset+18)], encoding: .ascii)?
                .trimmingCharacters(in: .whitespacesAndNewlines) ?? ""
        }
        guard self.model == "MCVKC03", [5,6].contains(self.videoPort) else {
            throw PanelError("Unexpected panel \(self.model), port \(self.videoPort)")
        }
        complete = true
    }

    deinit { self.close() }

    private func close() {
        guard !self.closed else { return }
        self.closed = true
        if let handle {
            if self.claimed { _ = libusb_release_interface(handle, 3) }
            libusb_close(handle)
        }
        if let context { libusb_exit(context) }
        if let hid { IOHIDDeviceClose(hid, 0) }
        IOHIDManagerClose(self.manager, 0)
    }

    private func set(_ bytes: [UInt8]) throws {
        var report = Array(bytes.prefix(8))
        report += [UInt8](repeating: 0, count: 8-report.count)
        let r = report.withUnsafeBufferPointer {
            IOHIDDeviceSetReport(self.hid, kIOHIDReportTypeFeature, 0, $0.baseAddress!, 8)
        }
        guard r == kIOReturnSuccess else { throw PanelError(String(format: "HID set failed: %08x", r)) }
    }

    private func read(_ address: UInt16) throws -> [UInt8] {
        try self.set([0xb5, UInt8(address >> 8), UInt8(address & 0xff)])
        var response = [UInt8](repeating: 0, count: 8)
        var count = 8
        let r = response.withUnsafeMutableBufferPointer {
            IOHIDDeviceGetReport(self.hid, kIOHIDReportTypeFeature, 0, $0.baseAddress!, &count)
        }
        guard r == kIOReturnSuccess, count == 8 else { throw PanelError("HID read failed") }
        return Array(response[3..<7])
    }

    private func write(_ address: UInt16, _ value: UInt8) throws {
        try self.set([0xb6, UInt8(address >> 8), UInt8(address & 0xff), value])
    }

    private func command(_ bytes: [UInt8], status: UInt16, settle: Double = 0.02) throws {
        try self.set(bytes)
        Thread.sleep(forTimeInterval: settle)
        let deadline = Date().addingTimeInterval(1.5)
        while Date() < deadline {
            if try self.read(status)[0] == 0 { return }
            Thread.sleep(forTimeInterval: 0.004)
        }
        throw PanelError(String(format: "Panel command timeout: %04x", status))
    }

    private func screen(_ on: Bool) throws {
        let address: UInt16 = self.videoPort == 5 ? 0xf507 : 0xf005
        let mask: UInt8 = self.videoPort == 5 ? 0x02 : 0x10
        let inverted = self.videoPort == 5
        let old = try self.read(address)[0]
        let setBit = inverted ? !on : on
        let new = setBit ? old | mask : old & ~mask
        if new != old { try self.write(address, new) }
    }

    func initialize(intermediate: Bool = false, color: UInt8 = 0x11) throws {
        try self.command([0xa6,0x03,0x03], status: 0xc558)
        try self.write(0xdeee, 1)
        try self.command([0xa6,0x07,0x01], status: 0xc454, settle: 0.1)
        try self.command([0xa6,0x05,0x00], status: 0xc555)
        try self.screen(false)
        if intermediate {
            try self.command([0xa6,0x01,0x07,0x80,0x04,0x38,0x22,0], status: 0xc555)
            try self.command([0xa6,0x02,0x81,0,0x07,0x80,0x04,0x38], status: 0xc557)
            Thread.sleep(forTimeInterval: 0.05)
        }
        try self.command([0xa6,0x01,0x01,0x68,0x03,0xc0,color,0], status: 0xc555)
        try self.command([0xa6,0x02,0xa0,0,0x01,0x68,0x03,0xc0], status: 0xc557)
        try self.command([0xa6,0x04,0x01], status: 0xc555)
        self.enabled = false
    }

    func send(_ bgr: [UInt8]) throws {
        guard bgr.count == self.width*self.height*3, let handle else { throw PanelError("Invalid frame") }
        let header: [UInt8] = [0xff,0,0,0,0,UInt8(self.width >> 4),
            UInt8(((self.width & 15) << 4) | ((self.height >> 8) & 15)),UInt8(self.height & 255)]
        let bytes = header + bgr.map { $0 == 255 ? 254 : $0 } + [0xff,0xc0,0,0,0,0,0,0]
        try bytes.withUnsafeBytes { raw in
            let data = raw.bindMemory(to: UInt8.self).baseAddress!
            var offset = 0
            while offset < bytes.count {
                let n = min(0x80000,bytes.count-offset)
                var transferred: Int32 = 0
                let result = libusb_bulk_transfer(handle,0x04,
                    UnsafeMutablePointer(mutating: data.advanced(by: offset)),Int32(n),&transferred,8000)
                guard result == 0, transferred > 0 else {
                    throw PanelError("USB frame transfer failed: \(result) at \(offset), \(transferred) bytes")
                }
                offset += Int(transferred)
            }
            var transferred: Int32 = 0
            let result = libusb_bulk_transfer(handle,0x04,nil,0,&transferred,1000)
            guard result == 0 else { throw PanelError("USB frame delimiter failed: \(result)") }
        }
        try self.set([0xa6,0x00,self.frameID,100,0,0,0,0])
        self.frameID ^= 1
        if !self.enabled {
            try self.screen(true)
            try self.command([0xa6,0x05,0x01], status: 0xc555)
            self.enabled = true
        }
    }

    func turnOff() {
        try? self.screen(false)
        self.enabled = false
    }

    func diagnostics() throws {
        for address: UInt16 in [0x0030,0xc454,0xc550,0xc554,0xc558,0xf005,0xf507,0xdeee] {
            let bytes = try self.read(address)
            print(String(format: "%04x",address),bytes.map { String(format:"%02x",$0) }.joined(separator:" "))
        }
    }
}
