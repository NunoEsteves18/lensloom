# LensLoom

**LensLoom** is a modular .NET C# connector built to streamline post-capture photography workflows and session planning.

Instead of acting as another photo viewer or editor, LensLoom connects local storage, image metadata, and weather/sunlight data into three independent commands:

- **Ingest:** Watches incoming folders or SD cards, reads EXIF metadata, separates RAW and JPEG files into a clean `Year/Month/Session` structure without duplicates, and generates a session summary.
- **Catalog:** Analyzes selected photographs to extract dominant color palettes and camera settings, producing structured Markdown/JSON cards ready for portfolios or social media captions.
- **Planner:** Cross-references Golden Hour / Blue Hour times with local cloud cover and wind forecasts for saved locations, sending alerts when conditions look promising.
