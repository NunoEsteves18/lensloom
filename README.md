# LensLoom

**LensLoom** is a modular .NET C# connector designed to automate post-capture photography workflows and session planning.

Instead of acting as another photo editor or heavy catalog viewer, LensLoom bridges local file systems, image metadata, and environmental data through three independent, composable workflows.

---

## Core Capabilities

### 1. Automated Ingestion & Triage (`ingest`)
- Monitors incoming folders or SD card mounts for new image files.
- Extracts EXIF metadata (capture timestamp, camera body, lens model, focal length, aperture, shutter speed, ISO).
- Computes SHA-256 hashes to prevent duplicate imports.
- Organizes files into a deterministic `Year/Month/Session` directory layout, separating `RAW` and `JPEG` assets.
- Generates a Markdown session summary with gear usage statistics.

### 2. Metadata & Color Cataloging (`catalog`)
- Scans a curated folder of selected photographs.
- Extracts dominant color palettes (HEX values and color temperature profiles) alongside camera EXIF settings.
- Outputs structured JSON and Markdown cards ready for portfolio galleries, personal websites, or social media captions.

### 3. Session & Golden Hour Planner (`planner`)
- Calculates sunrise, sunset, Golden Hour, and Blue Hour windows for saved locations.
- Enriches time windows with local weather forecasts (cloud cover percentage, wind speed, precipitation probability).
- Evaluates configurable scoring rules and dispatches alerts when optimal shooting conditions are detected.

---

## Architecture

LensLoom is organized around a shared domain core with isolated modules for each workflow:

```text
LensLoom/
├── LensLoom.sln
├── src/
│   ├── LensLoom.Core/          # Shared domain models, abstractions, and scoring/formatting rules
│   ├── LensLoom.Ingest/        # File watching, EXIF extraction, deduplication, and folder organization
│   ├── LensLoom.Catalog/       # Dominant color analysis and portfolio/caption card generation
│   ├── LensLoom.Planner/       # Solar calculations, weather API integration, and alert rules
│   └── LensLoom.Cli/           # Unified CLI entry point (ingest, catalog, planner commands)
└── tests/
    └── LensLoom.Core.Tests/    # Unit tests for domain logic, naming rules, and condition scoring
