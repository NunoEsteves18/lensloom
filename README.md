# LensLoom

LensLoom is an early-stage, .NET photography workflow project. Its goal is to make the steps after a photo shoot easier by helping photographers ingest and organize image files, catalog useful photo metadata and color information, and plan sessions around important lighting conditions.

The project is being built as a modular command-line application in C#. The current implementation is focused on the first workflow: importing and organizing photo files. Cataloging, session planning, and AI-assisted features remain planned areas of development.

## Current status

### Implemented: photo ingestion

The `ingest` command currently:

- Scans a source folder for supported RAW and JPEG files, recursively by default.
- Reads available EXIF metadata, such as capture time and camera settings.
- Calculates SHA-256 file hashes to identify duplicate imports.
- Organizes imported files into a date- and session-based folder structure, separating RAW and JPEG assets.
- Writes a Markdown summary for the imported session.
- Reports successful imports, duplicates, and errors in the console.

### Planned: cataloging and session planning

The broader project direction includes:

- Cataloging selected photographs with metadata and color information.
- Planning photo sessions using sunrise, sunset, Golden Hour, and Blue Hour times.
- Bringing in local weather information to help assess shooting conditions.

These workflows are planned and may change as the project develops.

## AI integration

A key future goal is to add AI integration to LensLoom. The intention is to explore how AI could assist photographers—for example, by helping describe or tag images, create useful summaries or captions, or surface patterns across a catalog.

AI integration is a goal for future development; it is not part of the current implementation. The specific capabilities, providers, and design have not yet been decided.

## Project structure

The solution is organized into a shared core, workflow modules, a CLI, and tests:

```text
LensloomProject/
├── LensLoom.slnx
├── Program.cs                 # Current CLI entry point
├── src/
│   ├── LensLoom.Core/         # Shared models and abstractions
│   ├── LensLoom.Ingest/       # EXIF reading, deduplication, and file organization
│   ├── LensLoom.Catalog/      # Planned catalog workflow
│   ├── LensLoom.Planner/      # Planned session-planning workflow
│   └── LensLoom.Cli/          # CLI module in the solution structure
└── tests/
    └── LensLoom.Core.Tests/
```

## Requirements

- .NET 11 SDK

## Getting started

Restore and build the solution:

```bash
dotnet restore LensloomProject/LensLoom.slnx
dotnet build LensloomProject/LensLoom.slnx
```

Show the available command usage:

```bash
dotnet run --project LensloomProject/LensloomProject.csproj -- --help
```

Ingest photos by providing a source folder and a destination library folder:

```bash
dotnet run --project LensloomProject/LensloomProject.csproj -- ingest \
  --source "/path/to/photos" \
  --destination "/path/to/photo-library" \
  --session "Coastal Walk"
```

The session name is optional; when omitted, the current date is used. Use `--no-recursive` to scan only the top-level source folder.

## Project goals

LensLoom aims to grow into a practical, modular photography workflow assistant: starting with reliable local photo ingestion, then adding cataloging and session-planning workflows, with AI-assisted features considered as optional enhancements rather than core requirements.

## Contributing

This project is at an early stage. Ideas, feedback, and contributions are welcome. Please open an issue to discuss a significant feature or design change before submitting a pull request.

## License

LensLoom is licensed under the [MIT License](LICENSE).
