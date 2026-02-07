# Contributing to ThunderCat Subtitles

## 🚀 Release Process (发布流程)

To publish a new version of the plugin, follow these steps strictly to trigger the automated CI/CD pipeline.

### 1. Update Version Number
Update the `<Version>` tag in `Emby.Subtitle.OneOneFiveMaster/Emby.Subtitle.OneOneFiveMaster.csproj`.

```xml
<Version>1.2.0</Version>
```

### 2. Update Documentation
Add release notes to:
- `README.md` (English)
- `README_CN.md` (Chinese)

### 3. Commit and Tag
Run the following git commands:

```bash
# Stage changes
git add Emby.Subtitle.OneOneFiveMaster.csproj README.md README_CN.md

# Commit with changelog
git commit -m "chore: Bump version to 1.2.0"

# Tag the release (Must start with 'v')
git tag v1.2.0

# Push to GitHub (Code + Tags)
git push origin main --tags
```

### 4. Verify Build
Go to the [GitHub Actions](https://github.com/drunkleee/Emby.Subtitle.ThunderCat/actions) page.
- The **Publish Release** workflow should trigger automatically.
- Once finished, a new Release draft will be created with the `Emby.Subtitle.ThunderCat.zip` artifact attached.

---

## ⚠️ CI/CD Configuration Notes

The GitHub Action (`.github/workflows/release.yml`) is configured with specific settings to ensure successful builds. **Do not modify these unless you know what you are doing.**

### Critical Settings:

1.  **Permissions**:
    The workflow requires write access to create releases.
    ```yaml
    permissions:
      contents: write
    ```

2.  **Emby Nuget Source**:
    Emby packages are hosted on MyGet, not Nuget.org. The restore command explicitly includes this source to avoid "Too many retries" errors.
    ```yaml
    run: dotnet restore --source "https://api.nuget.org/v3/index.json" --source "https://www.myget.org/F/emby/api/v3/index.json"
    ```
