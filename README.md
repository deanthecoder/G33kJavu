[![Twitter URL](https://img.shields.io/twitter/url/https/twitter.com/deanthecoder.svg?style=social&label=Follow%20%40deanthecoder)](https://twitter.com/deanthecoder)

# G33kJávú
A cross-platform Avalonia-based duplicate code detector. (Work In Progress)

## Purpose
G33kJávú scans a folder and finds duplicated or near-duplicated code blocks across source files.  
It is designed to be fast on large trees by using fingerprinting and winnowing instead of brute-force file comparisons.

## Supported languages
- C#: `.cs`
- C/C++: `.c`, `.cpp`, `.h`
- Python: `.py`
- JavaScript: `.js`

## Quick start
```
dotnet run --project G33kJavu/G33kJavu.csproj
```

## How it works (high level)
- **Enumeration** – Recursively collects supported files, skipping generated/build folders.
- **Normalization** – Whitespace is collapsed, numbers are normalized, and strings can be ignored with heuristics.
- **Fingerprinting** – k-line shingles + winnowing create a compact set of fingerprints.
- **Matching** – Diagonal runs are expanded to maximal matching blocks and de-duplicated.

## UI highlights
- Folder tree on the left; duplicates list and diff view on the right.
- One-pane diff view with shared scrolling and match markers:
  - `≡` exact match
  - `~` match after normalization
  - `×` different
- Drag and drop a folder onto the window to scan.
- Reveal file in Finder/Explorer from the diff header.

## Settings
- **Ignore string content** – Replace string literals with `STR` (with heuristics to preserve code-like strings).
- **Ignore comments** – Drop comment-only lines (e.g. `//`, `/*`, `*`, `*/`).
- **k/w/min lines** – Control shingle length, winnowing window, and minimum report size.

## Status
- ✔ Cross-platform Avalonia UI
- ✔ Fingerprinting + winnowing pipeline
- ✔ Diff view with match markers
- ☐ Group identical files by content hash
- ☐ Export results

## License
See [LICENSE](LICENSE) for details.
