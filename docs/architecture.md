# Architecture

The project has four bounded layers:

1. Strict JSON parsing rejects duplicates, unsupported syntax, excessive nesting, oversized strings, excessive node counts, and non-finite or out-of-range numbers.
2. Versioned validators enforce exact manifest and settings-snapshot structures, normalized identifiers, canonical hashes, and comparison-context consistency.
3. Held-file validation constrains local Windows evidence paths and verifies the opened handle before parsing.
4. Offline analysis parses PresentMon-compatible CSV evidence and compares compatible repeated runs using conservative thresholds.

The library does not start capture sessions or decide how a machine should be configured. Capture provenance and system state remain external claims recorded in the evidence.

## Trust limits

A valid bundle proves structural and internal consistency, not that the capture producer, measured workload, hardware, or operator was honest. Hashes detect changes relative to recorded identities; they are not signatures and do not establish authorship.

