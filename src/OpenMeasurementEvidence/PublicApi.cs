using System;
using System.Collections.Generic;
using System.IO;

namespace OpenMeasurementEvidence
{
    /// <summary>Stable entry points for strict validation and offline analysis.</summary>
    public static class MeasurementEvidence
    {
        /// <summary>Validates a version 1 measurement-run manifest. Invalid input throws <see cref="InvalidDataException"/>.</summary>
        public static void ValidateManifestJson(string json)
        {
            MeasurementRunManifestV1Validator.ParseForTests(json);
        }

        /// <summary>Validates a version 1 game-settings snapshot. Invalid input throws <see cref="InvalidDataException"/>.</summary>
        public static void ValidateSettingsSnapshotJson(string json)
        {
            GameSettingsSnapshotV1Validator.ParseForTests(json);
        }

        /// <summary>Computes a collision-resistant canonical SHA-256 identity for normalized key/value evidence.</summary>
        public static string ComputeCanonicalHash(IEnumerable<KeyValuePair<string, string>> values)
        {
            return CanonicalEvidenceHash.Compute(values);
        }

        /// <summary>Analyzes a PresentMon-compatible CSV against an explicitly validated run manifest.</summary>
        public static RunMetrics AnalyzeCsv(TextReader reader, RunManifest manifest)
        {
            if (manifest == null) throw new ArgumentNullException("manifest");
            manifest.Validate();
            return PresentMonCsvAnalyzer.Analyze(reader, ValidatedRunManifest.CreateForUnboundAnalysis(manifest));
        }

        /// <summary>Compares repeated baseline and candidate runs using the toolkit's conservative decision rules.</summary>
        public static ComparisonResult Compare(IList<RunMetrics> baseline, IList<RunMetrics> candidate)
        {
            return RunComparison.Compare(baseline, candidate);
        }
    }
}
