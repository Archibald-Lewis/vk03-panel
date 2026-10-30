# Local metrics interface

The panel reads JSON files from:

```text
~/Library/Application Support/Stats/VK03Panel/
```

The companion Stats patch writes these files from the same reader callbacks that update Stats' own UI. Stock Stats does not write this directory.

| File | Fields | Units |
|---|---|---|
| `cpu-temperature.json` | `timestamp`, `celsius` | Unix seconds, °C |
| `cpu-frequency.json` | `timestamp`, `ghz` | Unix seconds, GHz |
| `cpu-load.json` | `timestamp`, `usage` | Unix seconds, fraction 0–1 |
| `gpu.json` | `timestamp`, `usage` | Unix seconds, fraction 0–1 |
| `memory.json` | `timestamp`, `used`, `total` | Unix seconds, bytes |
| `gpu-info.json` | `model` | Display name |
| `hardware.json` | `cpu`, `ram` | Hardware description from Stats |

Numeric samples must be finite and between 0 and 30 seconds old. Missing, invalid, future-dated or stale samples are ignored. Static hardware descriptions are not subject to this freshness window. Readings are available only when the relevant Stats module and reader are active. The current connected indicator also requires CPU load and frequency.

The app writes its own local diagnostics to:

```text
~/Library/Application Support/VK03Panel/
```

This includes metrics, a dashboard preview, touch diagnostic coordinates and current media metadata. Artwork is cached in a subdirectory. These files can include hardware models and song names; they are not repository content.

The optional login item is:

```text
~/Library/LaunchAgents/local.codex.VK03Panel.plist
```

It is created only by enabling login startup in settings. Disabling the checkbox removes it; quitting stops the app. Building the project does neither.

Album lookup uses `https://itunes.apple.com/search` with artist and album names. The app downloads matching artwork and keeps up to 32 cache entries. It has no project-operated upload or telemetry endpoint.
