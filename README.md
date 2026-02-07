# ThunderCat Subtitles for Emby

![ThunderCat Logo](https://img.shields.io/badge/Emby-Plugin-green.svg) ![License](https://img.shields.io/badge/License-MIT-blue.svg)

**ThunderCat Subtitles** is a powerful Emby plugin that integrates **SubtitleCat** and **Thunder (Xunlei)** subtitle providers. It is designed to be lightweight, dependency-free, and optimized for finding Chinese subtitles for movies and TV shows, with special support for **JAV codes**.

## Features

-   **Dual Providers**:  
    -   **SubtitleCat**: High-quality multi-language subtitles.
    -   **Thunder (Xunlei)**: Fast download speeds, optimized for Chinese content.
-   **Smart Code Extraction**:  
    -   Automatically extracts unique codes (e.g., `LULU-421`, `ABC-123`, `FC2-PPV-1234567`) from full video titles.
    -   Ignores irrelevant text in titles to improve search accuracy.
-   **Language Optimization**:  
    -   Prioritizes **Chinese (Simplified/Traditional)** subtitles when selected.
    -   Automatically finds the correct language version on SubtitleCat.
-   **Zero External Dependencies**:  
    -   Implementing using standard .NET libraries and Regex.
    -   No valid external DLLs required (avoids `HtmlAgilityPack` or `System.Text.Json` loading issues in Emby).
-   **Native Emby Integration**:  
    -   Configurable via Emby Dashboard.
    -   Supports manual search and download.

## Installation

1.  Download the latest `Emby.Subtitle.ThunderCat.dll` from the [Releases](https://github.com/drunkleee/Emby.Subtitle.ThunderCat/releases) page.
2.  Copy the DLL file to your Emby server's `plugins` folder:
    -   **Linux/Docker**: `/var/lib/emby/plugins` (or your mapped config volume)
    -   **Windows**: `%AppData%\Emby-Server\programdata\plugins`
3.  Restart Emby Server.

## Configuration

1.  Go to **Emby Dashboard** -> **Plugins**.
2.  Click on **ThunderCat Subtitles**.
3.  Enable or disable specific providers as needed:
    -   [x] Enable SubtitleCat
    -   [x] Enable Thunder
4.  Save configuration.

## Usage

Just search for subtitles as usual in Emby. The plugin will automatically:
1.  Analyze the video filename/title.
2.  Extract any manageable codes (like `ABC-123`).
3.  Search both providers.
4.  Return matched subtitles sorted by relevance.

## Build from Source

Requirements:
-   .NET SDK 6.0 or later

```bash
git clone https://github.com/drunkleee/Emby.Subtitle.ThunderCat.git
cd Emby.Subtitle.ThunderCat
dotnet build -c Release
```

## Author

**drunkleee**

## License

MIT
