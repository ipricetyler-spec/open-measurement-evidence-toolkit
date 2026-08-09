# Provenance Record

## Reviewed origin

The initial implementation was adapted and renamed from a privately developed predecessor controlled by the same project owner. The extraction was reviewed file by file before entering this repository. No third-party source files, binaries, packages, generated release artifacts, private UI, capture executable, signing material, or personal evidence were selected.

## Extraction allow-list

- `MeasurementAnalysis.cs`
- `MeasurementEvidenceValidation.cs`
- `StrictJsonManifestParser.cs`
- `MeasurementRunManifestV1.cs`
- `MeasurementEvidenceBundleV1.cs`
- `MeasurementAnalysisTests.cs`
- `MEASUREMENT-RUN-MANIFEST-V1.schema.json`
- `GAME-SETTINGS-SNAPSHOT-V1.schema.json`

The published `examples/` files are synthetic fixtures adapted from the test harness. They contain no live performance data or personal identifiers.

The extraction applies vendor-neutral namespaces and terminology and adds a public facade, project files, documentation, and automation. The predecessor remains private and unchanged.

## Third-party boundary

The implementation uses only .NET platform APIs. It parses documented CSV field names and serialized values commonly produced by PresentMon-compatible exporters, but contains no PresentMon source or binary. See [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md).

## Remaining legal gate

The repository should be published only after confirming licensing authority for distribution under the selected license:

- Apache-2.0 (selected August 9, 2026).
