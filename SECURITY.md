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
- Every HTTP request and SignalR connection must present a per-session **launch token**, generated at startup and valid only for the lifetime of that process. In packaged (`Production`) builds a missing token is a fatal startup error — the API fails closed rather than falling back to a shared development token.
- The WebView enforces a strict `Content-Security-Policy` (declared in `tauri.conf.json` and reinforced by a `SecurityHeadersMiddleware` response header), restricting script execution and network origins to the local API.
- Media streaming uses short-lived, HMAC-signed **stream tokens** scoped to a single torrent and file index, so a `<video>` URL never exposes the launch token.
- File downloads are validated for **containment** against the requesting torrent's own download directory, preventing path traversal outside the torrent's folder.
- A single-instance lock prevents a second copy of the API from running against the same data directory.

Potential risk areas include:

- A process running as the same OS user could call the local API if it observes the token from the process environment.
- Torrent file content is not scanned for malware — HarborTorrent downloads files as directed by the torrent metadata.
- Saved file paths are derived from torrent metadata. Downloads are contained to the torrent's own directory, but file content is not otherwise sanitised or scanned.

---

## Reporting a Vulnerability

If you discover a security vulnerability in HarborTorrent, please report it responsibly:

1. **Do not open a public GitHub issue** for security vulnerabilities.
2. Open a [GitHub Security Advisory](https://github.com/Kampila45/HarborTorrent/security/advisories/new) using the private reporting feature.
3. Include a clear description of the vulnerability, steps to reproduce it, and the potential impact.

Reported vulnerabilities will be reviewed and acknowledged within **7 days**. A fix will be targeted within **30 days** for confirmed issues, or the timeline will be communicated if a fix requires more time.
