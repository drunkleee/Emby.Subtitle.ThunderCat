# ThunderCat Subtitles for Emby

<div align="center">

![ThunderCat Logo](https://img.shields.io/badge/Emby-Plugin-green.svg) ![License](https://img.shields.io/badge/License-MIT-blue.svg)

[**English**](./README.md) | [**中文说明**](./README_CN.md)

</div>

**ThunderCat Subtitles** is a high-performance, lightweight Emby plugin that integrates **SubtitleCat** and **Thunder (Xunlei)** subtitle providers. Engineered for stability and accuracy, it features a zero-dependency architecture and smart media ID extraction.

## ✨ Key Features

-   **🚀 Zero Dependencies**:  
    -   Built with pure .NET Standard 2.0. No `HtmlAgilityPack` or `System.Text.Json` required.
    -   Eliminates "Assembly Not Found" errors common in Emby plugins.
    -   Uses highly optimized Regex for HTML and JSON parsing.

-   **🎯 Smart Media ID Extraction**:  
    -   Automatically detects and extracts unique media identifiers (e.g., `ABC-123`, `STUDIO-001`, `EP-01`) from complex filenames.
    -   Filters out noise to ensure precise subtitle matching for specific media releases.

-   **🌏 Dual Provider Integration**:  
    -   **SubtitleCat**: Excellent multi-language support with intelligent Jaccard similarity sorting.
    -   **Thunder (Xunlei)**: High-speed delivery infrastructure, optimized for Asian content.

-   **💎 Native Integration**:  
    -   Seamlessly integrates with Emby's subtitle search engine.
    -   Supports manual search, automated downloads, and scheduled tasks.
    -   Base64 encoding ensures compatibility with all Emby versions (fixes URL path issues).

## 📦 Installation

1.  Download the latest `Emby.Subtitle.ThunderCat.dll` from the [Releases](https://github.com/drunkleee/Emby.Subtitle.ThunderCat/releases) page.
2.  Copy the DLL file to your Emby server's `plugins` folder:
    -   **Linux/Docker**: `/var/lib/emby/plugins` (or your mapped config volume)
    -   **Windows**: `%AppData%\Emby-Server\programdata\plugins`
3.  Restart Emby Server.

## ⚙️ Usage

1.  Go to **Emby Dashboard** -> **Plugins**.
2.  Verify **ThunderCat Subtitles** is listed.
3.  Go to **Library** -> **Metadata** -> **Subtitle Downloads**.
4.  Enable **ThunderCat** and configure your preferred languages.

## 🛠 Build from Source

Requirements:
-   .NET SDK 6.0 or later

```bash
git clone https://github.com/drunkleee/Emby.Subtitle.ThunderCat.git
cd Emby.Subtitle.ThunderCat
dotnet build -c Release
```

## 📄 License

MIT License © 2026 drunkleee
