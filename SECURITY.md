# Security Policy

## Supported Versions

| Version | Supported |
|---------|-----------|
| 1.0.x   | ✅ Yes    |

Only the latest release receives security fixes. Older versions are not maintained.

---

## Known Attack Surface

HarborTorrent is a local desktop application. Its security model assumes the following:

- The `.NET` API binds exclusively to the loopback interface (`127.0.0.1`) and is **not reachable from the network**.
- Every HTTP request and SignalR connection must present a per-session **launch token**, generated at startup and valid only for the lifetime of that process.
- Torrent metadata is rendered inside the WebView. Maliciously crafted torrent names or descriptions could attempt content injection. A `Content-Security-Policy` header restricts script execution to the local API origin.

Potential risk areas include:

- A process running as the same OS user could call the local API if it observes the token from the process environment.
- Torrent file content is not scanned for malware — HarborTorrent downloads files as directed by the torrent metadata.
- Saved file paths are constructed from torrent metadata and are not sanitised beyond what MonoTorrent provides.

---

## Reporting a Vulnerability

If you discover a security vulnerability in HarborTorrent, please report it responsibly:

1. **Do not open a public GitHub issue** for security vulnerabilities.
2. Open a [GitHub Security Advisory](https://github.com/Kampila45/HarborTorrent/security/advisories/new) using the private reporting feature.
3. Include a clear description of the vulnerability, steps to reproduce it, and the potential impact.

Reported vulnerabilities will be reviewed and acknowledged within **7 days**. A fix will be targeted within **30 days** for confirmed issues, or the timeline will be communicated if a fix requires more time.
