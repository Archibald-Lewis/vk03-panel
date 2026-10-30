import AppKit
import CoreText
import ImageIO

// All coordinates here are in the physical landscape panel's 960 × 360 space.
enum PanelLayout {
    static let mute = CGRect(x:22,y:250,width:86,height:90)
    static let quieter = CGRect(x:108,y:250,width:84,height:90)
    static let volume = CGRect(x:192,y:250,width:180,height:90)
    static let louder = CGRect(x:372,y:250,width:100,height:90)
    static let previous = CGRect(x:488,y:250,width:90,height:90)
    static let play = CGRect(x:578,y:250,width:90,height:90)
    static let next = CGRect(x:668,y:250,width:90,height:90)
    static let lock = CGRect(x:774,y:250,width:164,height:90)
    static let media = CGRect(x:22,y:144,width:208,height:88)
    static let slider = CGRect(x:204,y:288,width:152,height:10)
}

final class PanelRenderer {
    var darkMode = false
    var system = SystemSnapshot()
    var lockProgress = 0.0
    var pressedRect:CGRect?
    private var symbols = [String:CGImage]()
    private let time:DateFormatter = { let f=DateFormatter();f.dateFormat="HH:mm";return f }()
    private let seconds:DateFormatter = { let f=DateFormatter();f.dateFormat="ss";return f }()
    private let date:DateFormatter = { let f=DateFormatter();f.locale=Locale(identifier:"zh_CN");f.dateFormat="M月d日 EEEE";return f }()

    func render(_ data:Metrics,rotation:Int) -> (CGImage,[UInt8]) {
        let landscape=rotation==90 || rotation==270
        let width=landscape ? 960 : 360, height=landscape ? 360 : 960
        let context=CGContext(data:nil,width:width,height:height,bitsPerComponent:8,bytesPerRow:width*4,
            space:CGColorSpace(name:CGColorSpace.sRGB)!,
            bitmapInfo:CGImageAlphaInfo.premultipliedLast.rawValue | CGBitmapInfo.byteOrder32Big.rawValue)!
        context.translateBy(x:0,y:CGFloat(height));context.scaleBy(x:1,y:-1)
        if !landscape { context.translateBy(x:360,y:0);context.rotate(by:.pi/2) }
        self.draw(data,in:context)
        let image=context.makeImage()!
        let pixels=context.data!.assumingMemoryBound(to:UInt8.self)
        var bgr=[UInt8](repeating:0,count:360*960*3)
        for y in 0..<960 { for x in 0..<360 {
            let sx:Int,sy:Int
            switch rotation {
            case 90:sx=y;sy=359-x
            case 180:sx=359-x;sy=959-y
            case 270:sx=959-y;sy=x
            default:sx=x;sy=y
            }
            let s=(sy*width+sx)*4,d=(y*360+x)*3
            bgr[d]=pixels[s+2];bgr[d+1]=pixels[s+1];bgr[d+2]=pixels[s]
        }}
        return (image,bgr)
    }

    private func draw(_ data:Metrics,in c:CGContext) {
        func color(_ r:CGFloat,_ g:CGFloat,_ b:CGFloat,_ a:CGFloat=1)->CGColor {
            NSColor(srgbRed:r,green:g,blue:b,alpha:a).cgColor
        }
        let dark=self.darkMode
        let ink=dark ? color(0.96,0.97,1) : color(0.10,0.14,0.22)
        let secondary=dark ? color(0.61,0.68,0.79) : color(0.39,0.45,0.55)
        let accent=dark ? color(0.31,0.68,1) : color(0.02,0.44,0.95)
        let cyan=dark ? color(0.25,0.84,0.83) : color(0.02,0.60,0.61)
        let purple=dark ? color(0.72,0.62,1) : color(0.52,0.36,0.91)
        let backdrop: [CGColor] = dark ? [color(0.035,0.07,0.13),color(0.10,0.14,0.22),color(0.045,0.09,0.16)] :
            [color(0.79,0.88,0.98),color(0.92,0.94,0.98),color(0.79,0.89,0.94)]
        let gradient=CGGradient(colorsSpace:CGColorSpace(name:CGColorSpace.sRGB),colors:backdrop as CFArray,locations:[0,0.5,1])!
        c.drawLinearGradient(gradient,start:.zero,end:CGPoint(x:960,y:360),options:[])
        // Broad, translucent color pools give the glass its depth without busy decoration.
        for (center,tint) in [(CGPoint(x:300,y:-30),accent),(CGPoint(x:860,y:380),cyan)] {
            let g=CGGradient(colorsSpace:CGColorSpace(name:CGColorSpace.sRGB),colors:[tint.copy(alpha:dark ? 0.11:0.09)!,tint.copy(alpha:0)!] as CFArray,locations:[0,1])!
            c.drawRadialGradient(g,startCenter:center,startRadius:0,endCenter:center,endRadius:410,options:[])
        }
        func round(_ rect:CGRect,_ radius:CGFloat,_ fill:CGColor) {
            c.setFillColor(fill);c.addPath(CGPath(roundedRect:rect,cornerWidth:radius,cornerHeight:radius,transform:nil));c.fillPath()
        }
        func card(_ rect:CGRect,_ radius:CGFloat=25) {
            let path=CGPath(roundedRect:rect,cornerWidth:radius,cornerHeight:radius,transform:nil)
            c.saveGState();c.setShadow(offset:CGSize(width:0,height:3),blur:12,color:color(0,0,0,dark ? 0.15:0.055))
            c.setFillColor(dark ? color(0.16,0.20,0.28,0.80):color(1,1,1,0.68));c.addPath(path);c.fillPath();c.restoreGState()
            c.setStrokeColor(dark ? color(1,1,1,0.12):color(1,1,1,0.88));c.setLineWidth(1);c.addPath(path);c.strokePath()
            c.saveGState();c.addPath(path);c.clip()
            let g=CGGradient(colorsSpace:CGColorSpace(name:CGColorSpace.sRGB),colors:[color(1,1,1,dark ? 0.055:0.25),color(1,1,1,0)] as CFArray,locations:[0,1])!
            c.drawLinearGradient(g,start:rect.origin,end:CGPoint(x:rect.minX,y:rect.maxY),options:[]);c.restoreGState()
        }
        func text(_ value:String,_ x:CGFloat,_ y:CGFloat,_ size:CGFloat,_ tint:CGColor?=nil,_ weight:NSFont.Weight = .regular,_ mono:Bool=false,_ maxWidth:CGFloat=1000) {
            let font=mono ? NSFont.monospacedDigitSystemFont(ofSize:size,weight:weight):NSFont.systemFont(ofSize:size,weight:weight)
            let attrs:[NSAttributedString.Key:Any]=[.font:font,.foregroundColor:NSColor(cgColor:tint ?? ink)!]
            var line=CTLineCreateWithAttributedString(NSAttributedString(string:value,attributes:attrs))
            if CTLineGetTypographicBounds(line,nil,nil,nil)>Double(maxWidth) {
                let ellipsis=CTLineCreateWithAttributedString(NSAttributedString(string:"…",attributes:attrs))
                line=CTLineCreateTruncatedLine(line,Double(maxWidth),.end,ellipsis) ?? line
            }
            c.saveGState();c.translateBy(x:x,y:y+font.ascender);c.scaleBy(x:1,y:-1)
            c.textMatrix = .identity;c.textPosition = .zero;CTLineDraw(line,c);c.restoreGState()
        }
        func image(_ image:CGImage,_ rect:CGRect) {
            c.saveGState();c.translateBy(x:rect.minX,y:rect.maxY);c.scaleBy(x:1,y:-1)
            c.draw(image,in:CGRect(origin:.zero,size:rect.size));c.restoreGState()
        }
        func symbol(_ name:String,_ center:CGPoint,_ size:CGFloat,_ tint:CGColor) {
            let key="\(name)-\(size)"
            var cached=self.symbols[key]
            if cached==nil,let icon=NSImage(systemSymbolName:name,accessibilityDescription:nil)?.withSymbolConfiguration(.init(pointSize:size,weight:.medium)),
               let raw=icon.cgImage(forProposedRect:nil,context:nil,hints:nil) { self.symbols[key]=raw;cached=raw }
            guard let raw=cached else { return }
            let ratio=CGFloat(raw.width)/CGFloat(raw.height)
            let w=ratio>1 ? size:size*ratio,h=ratio>1 ? size/ratio:size
            c.saveGState();c.translateBy(x:center.x-w/2,y:center.y+h/2);c.scaleBy(x:1,y:-1)
            c.clip(to:CGRect(x:0,y:0,width:w,height:h),mask:raw);c.setFillColor(tint);c.fill(CGRect(x:0,y:0,width:w,height:h));c.restoreGState()
        }
        func bar(_ fraction:Double,_ rect:CGRect,_ tint:CGColor) {
            round(rect,rect.height/2,dark ? color(1,1,1,0.10):color(0.25,0.35,0.50,0.10))
            var filled=rect;filled.size.width *= min(1,max(0,fraction))
            if filled.width>0 { round(filled,rect.height/2,tint) }
        }
        let temperature=data.temperature.map { String(format:"%.0f°",$0) } ?? "—"
        let frequency=data.frequency.map { String(format:"%.2f",$0) } ?? "—"
        let cpu=data.cpu.map { String(format:"%.0f%%",$0*100) } ?? "—"
        let gpu=data.gpu.map { String(format:"%.0f%%",$0*100) } ?? "—"
        let used=data.memoryUsed.map { String(format:"%.1f",$0) } ?? "—"
        let capacity=data.memoryTotal.map { String(format:"%.0f",$0) } ?? "—"
        let memoryFraction=(data.memoryUsed ?? 0)/max(1,data.memoryTotal ?? 1)

        symbol("desktopcomputer",CGPoint(x:34,y:30),14,secondary)
        text("VK03",48,20,12,secondary,.semibold)
        round(CGRect(x:186,y:25,width:5,height:5),2.5,data.hasStats ? cyan:secondary)
        text(data.source,196,19,11,secondary)
        text(self.time.string(from:data.date),22,49,59,ink,.light,true)
        text(self.seconds.string(from:data.date),201,85,16,secondary,.regular,true)
        text(self.date.string(from:data.date),26,120,13,secondary)

        card(PanelLayout.media,22)
        let art=CGRect(x:34,y:156,width:64,height:64)
        c.saveGState();c.addPath(CGPath(roundedRect:art,cornerWidth:13,cornerHeight:13,transform:nil));c.clip()
        if let artwork=self.system.artwork { image(artwork,art) }
        else { round(art,13,accent.copy(alpha:0.13)!);symbol("music.note",CGPoint(x:66,y:188),29,accent) }
        c.restoreGState()
        text(self.system.title.isEmpty ? "暂无播放":self.system.title,109,158,14,ink,.medium,false,108)
        text(self.system.artist.isEmpty ? "音乐与播客":self.system.artist,109,182,11,secondary,.regular,false,108)
        let elapsed=self.system.elapsed,duration=self.system.duration
        bar(duration>0 ? elapsed/duration:0,CGRect(x:110,y:213,width:105,height:3),accent)

        card(CGRect(x:248,y:20,width:266,height:212))
        symbol("cpu",CGPoint(x:273,y:46),16,accent)
        text("CPU",288,36,12,secondary,.semibold)
        text(data.cpuModel.isEmpty ? "等待 Stats":data.cpuModel,270,66,20,ink,.semibold,false,224)
        if let cores=data.physicalCores,let threads=data.logicalCores { text("\(cores) 核心 · \(threads) 线程",271,95,12,secondary) }
        text(temperature,267,113,55,ink,.light,true)
        text(frequency,413,123,25,accent,.medium,true)
        text("GHz",454,157,12,secondary)
        text("温度",273,178,11,secondary)
        text("负载",271,204,11,secondary)
        text(cpu,461,202,13,secondary,.medium,true)
        bar(data.cpu ?? 0,CGRect(x:310,y:210,width:137,height:5),accent)

        card(CGRect(x:532,y:20,width:194,height:212))
        symbol("memorychip",CGPoint(x:557,y:46),16,purple)
        text("内存",574,36,12,secondary,.semibold)
        text(capacity,552,69,43,ink,.light,true)
        text("GB",618,93,15,secondary)
        text(data.memoryModules,555,122,13,secondary,.medium)
        text(data.memorySpeed,555,146,13,secondary)
        text("已用 \(used) GB",555,181,13,ink,.medium)
        bar(memoryFraction,CGRect(x:555,y:210,width:148,height:5),purple)

        card(CGRect(x:744,y:20,width:194,height:212))
        symbol("display",CGPoint(x:769,y:46),16,cyan)
        text("GPU",786,36,12,secondary,.semibold)
        let model=data.gpuModel.replacingOccurrences(of:"NVIDIA ",with:"")
        text(model.contains("GeForce") ? "GeForce":"显卡",767,78,13,secondary)
        text(model.isEmpty ? "等待 Stats":model.replacingOccurrences(of:"GeForce ",with:""),765,102,23,ink,.semibold,false,154)
        text(gpu,764,143,40,ink,.light,true)
        text("利用率",767,189,11,secondary)
        bar(data.gpu ?? 0,CGRect(x:767,y:210,width:148,height:5),cyan)

        card(CGRect(x:22,y:250,width:450,height:90),28)
        card(CGRect(x:488,y:250,width:270,height:90),28)
        card(PanelLayout.lock,28)
        if let pressed=self.pressedRect { round(pressed.insetBy(dx:5,dy:5),23,accent.copy(alpha:0.17)!) }
        symbol(self.system.muted ? "speaker.slash.fill":"speaker.wave.2.fill",CGPoint(x:65,y:287),29,self.system.muted ? accent:ink)
        text(self.system.muted ? "已静音":"静音",49,316,11,secondary)
        round(CGRect(x:116,y:260,width:68,height:68),22,dark ? color(1,1,1,0.07):color(1,1,1,0.48))
        symbol("minus",CGPoint(x:150,y:294),28,ink)
        bar(self.system.muted ? 0:(self.system.volume ?? 0),PanelLayout.slider,accent)
        if let volume=self.system.volume {
            let x=204+CGFloat(self.system.muted ? 0:volume)*152
            round(CGRect(x:x-9,y:284,width:18,height:18),9,dark ? color(0.9,0.95,1):color(1,1,1))
        }
        text(self.system.volume.map { String(format:"音量 %.0f%%",$0*100) } ?? "音量 —",247,315,12,secondary,.medium,true)
        round(CGRect(x:392,y:260,width:68,height:68),22,dark ? color(1,1,1,0.07):color(1,1,1,0.48))
        symbol("plus",CGPoint(x:426,y:294),28,ink)
        symbol("backward.end.fill",CGPoint(x:533,y:294),28,ink)
        round(CGRect(x:589,y:260,width:68,height:68),28,accent.copy(alpha:dark ? 0.25:0.13)!)
        symbol(self.system.playing ? "pause.fill":"play.fill",CGPoint(x:623,y:294),32,accent)
        symbol("forward.end.fill",CGPoint(x:713,y:294),28,ink)
        if self.lockProgress>0 {
            var progress=PanelLayout.lock;progress.size.width *= CGFloat(min(1,self.lockProgress))
            c.saveGState();c.addPath(CGPath(roundedRect:PanelLayout.lock,cornerWidth:28,cornerHeight:28,transform:nil));c.clip()
            round(progress,0,accent.copy(alpha:0.22)!);c.restoreGState()
        }
        symbol("lock.fill",CGPoint(x:856,y:280),28,self.system.canLock ? ink:secondary)
        text("按住锁屏",824,313,15,self.system.canLock ? ink:secondary,.medium)

    }

    static func save(_ image:CGImage,to url:URL) {
        guard let destination=CGImageDestinationCreateWithURL(url as CFURL,"public.png" as CFString,1,nil) else { return }
        CGImageDestinationAddImage(destination,image,nil);CGImageDestinationFinalize(destination)
    }
}
