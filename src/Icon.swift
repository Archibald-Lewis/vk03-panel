import AppKit
import ImageIO
let folder=URL(fileURLWithPath:CommandLine.arguments[1],isDirectory:true)
try FileManager.default.createDirectory(at:folder,withIntermediateDirectories:true)
for size in [16,32,64,128,256,512,1024] {
 let c=CGContext(data:nil,width:size,height:size,bitsPerComponent:8,bytesPerRow:size*4,space:CGColorSpace(name:CGColorSpace.sRGB)!,bitmapInfo:CGImageAlphaInfo.premultipliedLast.rawValue)!
 c.scaleBy(x:CGFloat(size)/512,y:CGFloat(size)/512)
 let tile=CGPath(roundedRect:CGRect(x:24,y:24,width:464,height:464),cornerWidth:105,cornerHeight:105,transform:nil)
 c.addPath(tile);c.clip()
 let gradient=CGGradient(colorsSpace:CGColorSpace(name:CGColorSpace.sRGB),colors:[NSColor(calibratedRed:0.21,green:0.68,blue:1,alpha:1).cgColor,NSColor(calibratedRed:0.08,green:0.31,blue:0.92,alpha:1).cgColor] as CFArray,locations:[0,1])!
 c.drawLinearGradient(gradient,start:CGPoint(x:40,y:480),end:CGPoint(x:430,y:40),options:[])
 c.setStrokeColor(NSColor.white.withAlphaComponent(0.9).cgColor);c.setLineWidth(16)
 c.addPath(CGPath(roundedRect:CGRect(x:114,y:109,width:284,height:294),cornerWidth:31,cornerHeight:31,transform:nil));c.strokePath()
 for (i,width) in [114,164,136].enumerated() {
  c.setFillColor(NSColor.white.withAlphaComponent(0.9-Double(i)*0.2).cgColor)
  c.addPath(CGPath(roundedRect:CGRect(x:155,y:CGFloat(300-i*61),width:CGFloat(width),height:20),cornerWidth:10,cornerHeight:10,transform:nil));c.fillPath()
 }
 let image=c.makeImage()!
 let files:[String]
 switch size {
 case 16: files=["icon_16x16.png"]
 case 32: files=["icon_16x16@2x.png","icon_32x32.png"]
 case 64: files=["icon_32x32@2x.png"]
 case 128: files=["icon_128x128.png"]
 case 256: files=["icon_128x128@2x.png","icon_256x256.png"]
 case 512: files=["icon_256x256@2x.png","icon_512x512.png"]
 default: files=["icon_512x512@2x.png"]
 }
 for name in files {
  let dest=CGImageDestinationCreateWithURL(folder.appendingPathComponent(name) as CFURL,"public.png" as CFString,1,nil)!
  CGImageDestinationAddImage(dest,image,nil);CGImageDestinationFinalize(dest)
 }
}
