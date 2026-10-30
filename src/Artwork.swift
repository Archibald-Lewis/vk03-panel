import Foundation
import CryptoKit

// A cover-only fallback for systems where Now Playing publishes black artwork.
// Requires an exact album match and a compatible artist, and never reuses a cover
// for a different album while an asynchronous request is in flight.
final class AlbumArtwork {
    private let queue=DispatchQueue(label:"local.codex.VK03Panel.artwork",qos:.utility)
    private let session:URLSession={
        let config=URLSessionConfiguration.ephemeral
        config.timeoutIntervalForRequest=12;config.timeoutIntervalForResource=20
        return URLSession(configuration:config)
    }()
    private let directory=FileManager.default.urls(for:.cachesDirectory,in:.userDomainMask)[0].appendingPathComponent("VK03Panel/Artwork",isDirectory:true)
    private var inFlight=Set<String>()
    private var attempted=[String:Date]()
    static func normalized(_ value:String)->String {
        value.folding(options:[.caseInsensitive,.diacriticInsensitive],locale:Locale(identifier:"en_US_POSIX"))
            .unicodeScalars.filter { CharacterSet.alphanumerics.contains($0) }.map(String.init).joined()
    }
    static func key(artist:String,album:String)->String { self.normalized(artist)+"|"+self.normalized(album) }
    static func match(_ results:[[String:Any]],artist:String,album:String)->[String:Any]? {
        let wantedAlbum=self.normalized(album),wantedArtist=self.normalized(artist)
        guard !wantedAlbum.isEmpty,!wantedArtist.isEmpty else { return nil }
        return results.first {
            guard let name=$0["collectionName"] as? String,let performer=$0["artistName"] as? String else { return false }
            let foundArtist=self.normalized(performer)
            return self.normalized(name)==wantedAlbum && !foundArtist.isEmpty &&
                (foundArtist==wantedArtist || (foundArtist.count>3 && wantedArtist.contains(foundArtist)))
        }
    }
    func fetch(artist:String,album:String,completion:@escaping(Data?,URL?)->Void) {
        let key=Self.key(artist:artist,album:album)
        guard !artist.isEmpty,!album.isEmpty else { return }
        self.queue.async {
            let file=self.directory.appendingPathComponent(SHA256.hash(data:Data(key.utf8)).map { String(format:"%02x",$0) }.joined()+".json")
            if let bytes=try? Data(contentsOf:file),let saved=try? JSONDecoder().decode(Saved.self,from:bytes) {
                completion(saved.data,URL(string:saved.link));return
            }
            guard !self.inFlight.contains(key),Date().timeIntervalSince(self.attempted[key] ?? .distantPast)>120 else { return }
            self.inFlight.insert(key);self.attempted[key]=Date()
            var search=URLComponents(string:"https://itunes.apple.com/search")!
            search.queryItems=[URLQueryItem(name:"term",value:artist+" "+album),URLQueryItem(name:"entity",value:"album"),
                URLQueryItem(name:"country",value:"US"),URLQueryItem(name:"limit",value:"12")]
            self.session.dataTask(with:search.url!) { data,response,error in
                guard let data,error==nil,(response as? HTTPURLResponse)?.statusCode==200,
                      let object=try? JSONSerialization.jsonObject(with:data) as? [String:Any],
                      let results=object["results"] as? [[String:Any]],let match=Self.match(results,artist:artist,album:album),
                      let location=match["artworkUrl100"] as? String,let url=URL(string:location),url.scheme=="https",
                      url.host?.hasSuffix(".mzstatic.com")==true else { self.finish(key);completion(nil,nil);return }
                let link=(match["collectionViewUrl"] as? String) ?? ""
                self.session.dataTask(with:url) { bytes,response,error in
                    guard let bytes,bytes.count<2_000_000,error==nil,(response as? HTTPURLResponse)?.statusCode==200 else { self.finish(key);completion(nil,nil);return }
                    self.queue.async {
                        try? FileManager.default.createDirectory(at:self.directory,withIntermediateDirectories:true)
                        if let data=try? JSONEncoder().encode(Saved(data:bytes,link:link)) { try? data.write(to:file,options:.atomic) }
                        if let files=try? FileManager.default.contentsOfDirectory(at:self.directory,includingPropertiesForKeys:[.contentModificationDateKey]),files.count>32 {
                            let sorted=files.sorted { (try? $0.resourceValues(forKeys:[.contentModificationDateKey]).contentModificationDate) ?? .distantPast < (try? $1.resourceValues(forKeys:[.contentModificationDateKey]).contentModificationDate) ?? .distantPast }
                            for file in sorted.prefix(files.count-32) { try? FileManager.default.removeItem(at:file) }
                        }
                        self.inFlight.remove(key);completion(bytes,URL(string:link))
                    }
                }.resume()
            }.resume()
        }
    }
    private func finish(_ key:String) { self.queue.async { self.inFlight.remove(key) } }
    private struct Saved:Codable { let data:Data;let link:String }
}
