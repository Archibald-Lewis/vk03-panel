import AppKit
import CoreAudio
import ImageIO

struct SystemSnapshot {
    var volume: Double?
    var muted = false
    var title = ""
    var artist = ""
    var album = ""
    var artworkSource = ""
    var artworkLink:URL?
    var playing = false
    var duration = 0.0
    var elapsed = 0.0
    var artwork: CGImage?
    var canLock = false
}

final class SystemState {
    private let lock = NSLock()
    private var value = SystemSnapshot()
    private var lastArtwork: Data?
    private var albumKey=""
    private let coverLookup=AlbumArtwork()
    private var restoreVolume = 0.5
    private let mediaBridge=MediaBridge()
    private var mediaTimestamp=Date()

    init() { self.mediaBridge.onUpdate={ [weak self] payload in self?.receiveMedia(payload) } }
    func stop() { self.mediaBridge.stop() }
    func didWake() { self.mediaBridge.restart() }

    func snapshot() -> SystemSnapshot {
        self.lock.lock();defer { self.lock.unlock() }
        var result=self.value
        if result.playing { result.elapsed=min(result.duration,max(0,result.elapsed+Date().timeIntervalSince(self.mediaTimestamp))) }
        return result
    }

    private func outputDevice() -> AudioDeviceID? {
        var address=AudioObjectPropertyAddress(mSelector:kAudioHardwarePropertyDefaultOutputDevice,
            mScope:kAudioObjectPropertyScopeGlobal,mElement:kAudioObjectPropertyElementMain)
        var device:AudioDeviceID=0;var size=UInt32(MemoryLayout.size(ofValue:device))
        guard AudioObjectGetPropertyData(AudioObjectID(kAudioObjectSystemObject),&address,0,nil,&size,&device)==noErr,
              device != 0 else { return nil }
        return device
    }

    private func volume(_ device:AudioDeviceID) -> Double? {
        for element:UInt32 in [0,1,2] {
            var address=AudioObjectPropertyAddress(mSelector:kAudioDevicePropertyVolumeScalar,
                mScope:kAudioDevicePropertyScopeOutput,mElement:element)
            var value:Float32=0;var size=UInt32(MemoryLayout<Float32>.size)
            if AudioObjectHasProperty(device,&address),
               AudioObjectGetPropertyData(device,&address,0,nil,&size,&value)==noErr { return Double(value) }
        }
        return nil
    }

    private func mute(_ device:AudioDeviceID) -> Bool? {
        var address=AudioObjectPropertyAddress(mSelector:kAudioDevicePropertyMute,
            mScope:kAudioDevicePropertyScopeOutput,mElement:0)
        var value:UInt32=0;var size=UInt32(MemoryLayout<UInt32>.size)
        guard AudioObjectHasProperty(device,&address),
              AudioObjectGetPropertyData(device,&address,0,nil,&size,&value)==noErr else { return nil }
        return value != 0
    }

    func refresh() {
        if let device=self.outputDevice() {
            let volume=self.volume(device)
            let muted=self.mute(device) ?? ((volume ?? 1) < 0.001)
            self.lock.lock();self.value.volume=volume;self.value.muted=muted;self.value.canLock=VKCanLockScreen();self.lock.unlock()
        }
        self.mediaBridge.ensureRunning()
    }

    private static func decodeArtwork(_ data:Data)->CGImage? {
        guard let source=CGImageSourceCreateWithData(data as CFData,nil),
              let image=CGImageSourceCreateThumbnailAtIndex(source,0,[kCGImageSourceCreateThumbnailFromImageAlways:true,kCGImageSourceThumbnailMaxPixelSize:160] as CFDictionary),
              let context=CGContext(data:nil,width:8,height:8,bitsPerComponent:8,bytesPerRow:32,space:CGColorSpaceCreateDeviceRGB(),bitmapInfo:CGImageAlphaInfo.premultipliedLast.rawValue) else { return nil }
        context.draw(image,in:CGRect(x:0,y:0,width:8,height:8))
        let pixels=context.data!.assumingMemoryBound(to:UInt8.self)
        return (0..<64).allSatisfy({ pixels[$0*4]<4 && pixels[$0*4+1]<4 && pixels[$0*4+2]<4 }) ? nil:image
    }
    private func receiveMedia(_ dictionary:[String:Any]) {
        self.lock.lock()
        self.value.playing=dictionary["playing"] as? Bool ?? false
        self.value.title=dictionary["title"] as? String ?? ""
        self.value.artist=dictionary["artist"] as? String ?? ""
        self.value.album=dictionary["album"] as? String ?? ""
        let artist=self.value.artist,album=self.value.album
        let key=AlbumArtwork.key(artist:artist,album:album),changed=key != self.albumKey
        if changed { self.albumKey=key;self.value.artwork=nil;self.value.artworkSource="";self.value.artworkLink=nil }
        self.value.duration=(dictionary["durationMicros"] as? NSNumber)?.doubleValue.mapMicroseconds ?? 0
        self.value.elapsed=(dictionary["elapsedTimeMicros"] as? NSNumber)?.doubleValue.mapMicroseconds ?? 0
        if let timestamp=(dictionary["timestampEpochMicros"] as? NSNumber)?.doubleValue { self.mediaTimestamp=Date(timeIntervalSince1970:timestamp/1_000_000) }
        else { self.mediaTimestamp=Date() }
        if let base64=dictionary["artworkData"] as? String,base64.count<7_000_000,let data=Data(base64Encoded:base64),data != self.lastArtwork {
            self.lastArtwork=data
            if let image=Self.decodeArtwork(data) { self.value.artwork=image;self.value.artworkSource="Now Playing";self.value.artworkLink=nil }
        }
        let needsCover=self.value.artwork==nil && !artist.isEmpty && !album.isEmpty
        self.lock.unlock()
        if needsCover {
            self.coverLookup.fetch(artist:artist,album:album) { [weak self] bytes,link in
                guard let self,let bytes,let image=Self.decodeArtwork(bytes) else { return }
                self.lock.lock();defer { self.lock.unlock() }
                guard self.albumKey==key,self.value.artwork==nil else { return }
                self.value.artwork=image;self.value.artworkSource="Apple 曲库";self.value.artworkLink=link
            }
        }
    }

    func setVolume(_ requested:Double) {
        guard let device=self.outputDevice() else { return }
        var scalar=Float32(min(1,max(0,requested)))
        for element:UInt32 in [0,1,2] {
            var address=AudioObjectPropertyAddress(mSelector:kAudioDevicePropertyVolumeScalar,
                mScope:kAudioDevicePropertyScopeOutput,mElement:element)
            var settable:DarwinBoolean=false
            if AudioObjectHasProperty(device,&address),
               AudioObjectIsPropertySettable(device,&address,&settable)==noErr,settable.boolValue {
                _=AudioObjectSetPropertyData(device,&address,0,nil,UInt32(MemoryLayout<Float32>.size),&scalar)
                if element==0 { break }
            }
        }
        if requested>0 { self.setMuted(false,device:device) }
        self.refresh()
    }

    private func setMuted(_ flag:Bool,device:AudioDeviceID) {
        var address=AudioObjectPropertyAddress(mSelector:kAudioDevicePropertyMute,
            mScope:kAudioDevicePropertyScopeOutput,mElement:0)
        var value:UInt32=flag ? 1 : 0
        var settable:DarwinBoolean=false
        if AudioObjectHasProperty(device,&address),AudioObjectIsPropertySettable(device,&address,&settable)==noErr,settable.boolValue {
            _=AudioObjectSetPropertyData(device,&address,0,nil,UInt32(MemoryLayout<UInt32>.size),&value)
        }
    }

    func adjustVolume(_ delta:Double) { self.setVolume((self.snapshot().volume ?? 0)+delta) }
    func toggleMute() {
        guard let device=self.outputDevice() else { return }
        let state=self.snapshot()
        if self.mute(device) != nil { self.setMuted(!state.muted,device:device) }
        else if state.muted { self.setVolume(self.restoreVolume) }
        else { self.restoreVolume=max(0.05,state.volume ?? 0.5);self.setVolume(0) }
        self.refresh()
    }
    func media(_ command:Int32) { self.mediaBridge.send(command) }
    func lockScreen() { _=VKLockScreen() }
}

private extension Double { var mapMicroseconds:Double { self/1_000_000 } }
