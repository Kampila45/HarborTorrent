# HarborTorrent Changelog

## v1.1.0 (Latest)
- **Feature**: Added YTS (YIFY Movies) and The Pirate Bay (Apibay) integration to the global search engine.
- **Feature**: Replaced the generic search results table with a premium, glassmorphic responsive Card Grid layout.
- **Performance**: Integrated global client-side pagination across Downloads, Completed, Search, Peers, and Trackers tabs.
- **Fix**: Resolved dependency injection bug where the YTS search provider was being overwritten.
- **Fix**: Added public UDP trackers to trackerless magnet links to accelerate metadata resolution times from minutes to seconds.
- **Fix**: Changed local torrent search to be completely case-insensitive.
- **UI**: Added instant "search-as-you-type" functionality to the top navigation bar.

## v1.0.0
- Initial stable release.
- Core Torrent Engine integration (MonoTorrent).
- SignalR real-time event broadcasting.
- Support for adding torrents via Magnet Link, InfoHash, and Base64 encoded files.
- Basic Downloads and Completed tabs.
- Anchor Mode for global bandwidth throttling.
