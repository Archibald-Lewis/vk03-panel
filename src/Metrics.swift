import Foundation

struct Metrics: Codable {
    var date = Date()
    var temperature: Double?
    var frequency: Double?
    var cpu: Double?
    var gpu: Double?
    var memoryUsed: Double?
    var memoryTotal: Double?
    var source = "Stats"
    var cpuModel = ""
    var physicalCores: Int?
    var logicalCores: Int?
    var memoryModules = ""
    var memorySpeed = ""
    var gpuModel = ""
    var hasStats: Bool { self.frequency != nil && self.cpu != nil }
}

// The panel never samples hardware. Every numeric metric is exported by Stats
// from the exact reader values used by its own popup and menu-bar widgets.
final class MetricsReader {
    private let directory = FileManager.default.urls(for:.applicationSupportDirectory,in:.userDomainMask)[0]
        .appendingPathComponent("Stats/VK03Panel",isDirectory:true)

    private func channel(_ name: String) -> [String:Double]? {
        guard let bytes = try? Data(contentsOf:self.directory.appendingPathComponent(name+".json")),
              let object = try? JSONSerialization.jsonObject(with:bytes) as? [String:Double],
              let timestamp = object["timestamp"],
              (0...30).contains(Date().timeIntervalSince1970-timestamp),
              object.values.allSatisfy({ $0.isFinite }) else { return nil }
        return object
    }

    func read() -> Metrics {
        var result = Metrics()
        result.temperature = self.channel("cpu-temperature")?["celsius"]
        result.frequency = self.channel("cpu-frequency")?["ghz"]
        result.cpu = self.channel("cpu-load")?["usage"]
        result.gpu = self.channel("gpu")?["usage"]
        if let bytes = try? Data(contentsOf:self.directory.appendingPathComponent("gpu-info.json")),
           let gpu = try? JSONSerialization.jsonObject(with:bytes) as? [String:Any] { result.gpuModel = gpu["model"] as? String ?? "" }
        if let memory = self.channel("memory") {
            result.memoryUsed = memory["used"].map { $0/1073741824 }
            result.memoryTotal = memory["total"].map { $0/1073741824 }
        }
        if let bytes = try? Data(contentsOf:self.directory.appendingPathComponent("hardware.json")),
           let hardware = try? JSONSerialization.jsonObject(with:bytes) as? [String:Any] {
            let cpu = hardware["cpu"] as? [String:Any]
            result.cpuModel = (cpu?["name"] as? String ?? "")
                .replacingOccurrences(of:"AMD ",with:"")
                .replacingOccurrences(of:" 8-Core Processor",with:"")
            result.physicalCores = cpu?["physicalCores"] as? Int
            result.logicalCores = cpu?["logicalCores"] as? Int
            if let dimms = (hardware["ram"] as? [String:Any])?["dimms"] as? [[String:Any]], !dimms.isEmpty {
                let sizes = dimms.compactMap { $0["size"] as? String }
                if Set(sizes).count == 1, let size = sizes.first { result.memoryModules = "\(sizes.count) × \(size)" }
                else { result.memoryModules = sizes.joined(separator:" + ") }
                result.memorySpeed = (dimms.first?["speed"] as? String ?? "").replacingOccurrences(of:"MHz",with:"MT/s")
            }
        }
        return result
    }
}
