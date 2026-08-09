using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace OpenMeasurementEvidence
{
    public sealed class RunManifest
    {
        public string RunId;
        public string ProtocolVersion;
        public string CaptureSource;
        public string GameExecutable;
        public string GameBuild;
        public string Scenario;
        public string GraphicsApi;
        public string DisplayMode;
        public string ComparisonContextId;
        public string ConfigurationLabel;
        public string CaptureSchemaVersion;
        public string TimingBasis;
        public string TargetApplication;
        public int TargetProcessId;
        public string TargetSwapChainAddress;
        public int PlannedCaptureSeconds;
        public double SlowFrameThresholdMs;
        public int CaptureSequenceIndex;
        public string CaptureSourceVersion;
        public string CaptureSourceSha256;
        public string CapturePolicyHash;
        public string CaptureInvocationHash;
        public string SessionName;
        public bool NoTrackInput;
        public string GameSettingsHash;
        public string ChangedVariablesKey;
        public string TimingDefinitionVersion;
        public string FrameGenerationState;
        public string FrameGenerationTechnology;
        public int RepetitionIndex;
        public int PlannedRepetitions;
        public string ExpectedPresentRuntime;
        public string ExpectedPresentMode;

        public string ProtocolKey
        {
            get
            {
                return string.Concat(new[]
                {
                    ProtocolVersion, CaptureSource, GameExecutable, GameBuild,
                    Scenario, GraphicsApi, DisplayMode, ComparisonContextId,
                    CaptureSchemaVersion, TimingBasis, TargetApplication,
                    PlannedCaptureSeconds.ToString(CultureInfo.InvariantCulture),
                    SlowFrameThresholdMs.ToString("R", CultureInfo.InvariantCulture),
                    CaptureSourceVersion, CaptureSourceSha256, CapturePolicyHash,
                    GameSettingsHash, ChangedVariablesKey, TimingDefinitionVersion,
                    FrameGenerationState, FrameGenerationTechnology, PlannedRepetitions.ToString(CultureInfo.InvariantCulture),
                    ExpectedPresentRuntime, ExpectedPresentMode
                }.Select(LengthPrefix));
            }
        }

        public void Validate()
        {
            Guid parsedRunId;
            if (!Guid.TryParse(RunId, out parsedRunId)) throw new InvalidDataException("RunId must be a GUID.");
            Require("ProtocolVersion", ProtocolVersion);
            Require("CaptureSource", CaptureSource);
            Require("GameExecutable", GameExecutable);
            Require("GameBuild", GameBuild);
            Require("Scenario", Scenario);
            Require("GraphicsApi", GraphicsApi);
            Require("DisplayMode", DisplayMode);
            Require("ComparisonContextId", ComparisonContextId);
            Require("ConfigurationLabel", ConfigurationLabel);
            Require("CaptureSchemaVersion", CaptureSchemaVersion);
            Require("TimingBasis", TimingBasis);
            Require("TargetApplication", TargetApplication);
            Require("TargetSwapChainAddress", TargetSwapChainAddress);
            Require("CaptureSourceVersion", CaptureSourceVersion);
            Require("SessionName", SessionName);
            Require("ChangedVariablesKey", ChangedVariablesKey);
            Require("TimingDefinitionVersion", TimingDefinitionVersion);
            Require("FrameGenerationState", FrameGenerationState);
            Require("FrameGenerationTechnology", FrameGenerationTechnology);
            Require("ExpectedPresentRuntime", ExpectedPresentRuntime);
            Require("ExpectedPresentMode", ExpectedPresentMode);
            RequireSha256("ComparisonContextId", ComparisonContextId);
            RequireSha256("CaptureSourceSha256", CaptureSourceSha256);
            RequireSha256("CapturePolicyHash", CapturePolicyHash);
            RequireSha256("CaptureInvocationHash", CaptureInvocationHash);
            RequireSha256("GameSettingsHash", GameSettingsHash);
            if (!NoTrackInput) throw new InvalidDataException("The frame-pacing protocol requires input tracking to be disabled.");
            if (!string.Equals(GameExecutable, TargetApplication, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("GameExecutable and TargetApplication must identify the same executable.");
            if (TargetProcessId <= 0) throw new InvalidDataException("TargetProcessId must be positive.");
            if (PlannedCaptureSeconds < 10 || PlannedCaptureSeconds > 7200) throw new InvalidDataException("PlannedCaptureSeconds is outside the supported range.");
            if (!IsFinitePositive(SlowFrameThresholdMs) || SlowFrameThresholdMs > 1000) throw new InvalidDataException("SlowFrameThresholdMs is outside the supported range.");
            if (CaptureSequenceIndex < 1 || CaptureSequenceIndex > 100) throw new InvalidDataException("CaptureSequenceIndex is outside the supported range.");
            if (PlannedRepetitions < 3 || PlannedRepetitions > 20) throw new InvalidDataException("PlannedRepetitions is outside the supported range.");
            if (RepetitionIndex < 1 || RepetitionIndex > PlannedRepetitions) throw new InvalidDataException("RepetitionIndex is outside the planned repetition range.");
            if (TimingBasis != "DisplayedTime" && TimingBasis != "MsBetweenDisplayChange" && TimingBasis != "MsBetweenPresents")
                throw new InvalidDataException("TimingBasis is not supported by this analyzer version.");
            if (FrameGenerationState != "off" && FrameGenerationState != "on" && FrameGenerationState != "unknown" && FrameGenerationState != "mixed")
                throw new InvalidDataException("FrameGenerationState is not supported by this analyzer version.");
            if (FrameGenerationTechnology != "none" && FrameGenerationTechnology != "intel-xess-fg" && FrameGenerationTechnology != "amd-afmf" && FrameGenerationTechnology != "nvidia-dlss-fg" && FrameGenerationTechnology != "unknown")
                throw new InvalidDataException("FrameGenerationTechnology is not supported by this analyzer version.");
            if (FrameGenerationState == "off" && FrameGenerationTechnology != "none")
                throw new InvalidDataException("Frame generation off requires technology none.");
            if ((FrameGenerationState == "on" || FrameGenerationState == "mixed") && FrameGenerationTechnology == "none")
                throw new InvalidDataException("Enabled or mixed frame generation requires an identified technology.");
            if (FrameGenerationState == "unknown" && FrameGenerationTechnology != "unknown")
                throw new InvalidDataException("Unknown frame-generation state requires unknown technology.");
        }

        private static void Require(string name, string value)
        {
            if (string.IsNullOrWhiteSpace(value)) throw new InvalidDataException(name + " is required.");
            if (value.Length > 1024) throw new InvalidDataException(name + " exceeds the supported length.");
            if (value.IndexOf('\0') >= 0 || value.IndexOf('\r') >= 0 || value.IndexOf('\n') >= 0)
                throw new InvalidDataException(name + " contains unsupported control characters.");
        }

        private static void RequireSha256(string name, string value)
        {
            Require(name, value);
            if (value.Length != 64 || value.Any(ch => !Uri.IsHexDigit(ch))) throw new InvalidDataException(name + " must be a 64-character SHA-256 value.");
        }

        private static string LengthPrefix(string value)
        {
            string normalized = (value ?? string.Empty).Trim().ToUpperInvariant();
            return normalized.Length.ToString(CultureInfo.InvariantCulture) + ":" + normalized;
        }

        private static bool IsFinitePositive(double value)
        {
            return value > 0 && !double.IsNaN(value) && !double.IsInfinity(value);
        }
    }

    internal sealed class ValidatedRunManifest
    {
        private readonly RunManifest value;
        internal RunManifest Value { get { return value; } }

        private ValidatedRunManifest(RunManifest value)
        {
            this.value = value;
        }

        internal static ValidatedRunManifest CreateAfterEvidenceBinding(MeasurementRunManifestV1 manifest, EvidenceBundleV1.ConsistencyBindingProof proof)
        {
            if (manifest == null) throw new ArgumentNullException("manifest");
            if (!EvidenceBundleV1.IsConsistencyProofValid(proof))
                throw new InvalidOperationException("Analyzer access requires a complete verified evidence bundle.");
            RunManifest copy = manifest.CopyForEvidenceBinding(proof);
            copy.Validate();
            return new ValidatedRunManifest(copy);
        }

#if MEASUREMENT_TESTS
        internal static ValidatedRunManifest CreateForUnboundAnalysis(RunManifest value)
        {
            if (value == null) throw new ArgumentNullException("value");
            value.Validate();
            return new ValidatedRunManifest(value);
        }
#endif

        // Bound construction is reachable only through the complete evidence binder.
    }

    public sealed class RunMetrics
    {
        public string RunId;
        public string ProtocolKey;
        public string ConfigurationLabel;
        public string TimingColumn;
        public string CaptureTopologyKey;
        public string PresentRuntime;
        public string PresentMode;
        public int CaptureSequenceIndex;
        public int RepetitionIndex;
        public int PlannedRepetitions;
        public int PlannedCaptureSeconds;
        public string FrameGenerationState;
        public string FrameGenerationTechnology;
        public int CapturedRows;
        public int ValidFrameCount;
        public int UndisplayedFrameCount;
        public int InvalidFrameRowCount;
        public int ApplicationFrameLabelCount;
        public int GeneratedFrameCount;
        public int UnknownFrameTypeCount;
        public double DurationSeconds;
        public double AverageFps;
        public double MedianFrameTimeMs;
        public double P95FrameTimeMs;
        public double P99FrameTimeMs;
        public double P99EquivalentFps;
        public double P999EquivalentFps;
        public double MedianAbsoluteDeviationMs;
        public double InterquartileRangeMs;
        public int SlowFrameCount;
        public double SlowFrameThresholdMs;
        public double SlowFramesPerThousand;
        public double UndisplayedFramesPerThousand;
        public bool UndisplayedMetricAvailable;
        public double GeneratedFramesPerThousand;
        public bool FrameTypeColumnPresent;
        public IList<string> Warnings = new List<string>();
    }

    public enum ComparisonVerdict
    {
        Improved,
        NoMaterialDifference,
        Worse,
        Inconclusive
    }

    public sealed class ComparisonResult
    {
        public ComparisonVerdict Verdict;
        public double BaselineMedianFps;
        public double CandidateMedianFps;
        public double FpsChangePercent;
        public double BaselineMedianP99Ms;
        public double CandidateMedianP99Ms;
        public double P99ChangePercent;
        public double DecisionThresholdPercent;
        public bool EligibleForAutomaticKeep;
        public IList<string> Reasons = new List<string>();
    }

    internal sealed class CaptureCsvTopology
    {
        internal string Application { get; private set; }
        internal int ProcessId { get; private set; }
        internal string SwapChainAddress { get; private set; }
        internal string PresentRuntime { get; private set; }
        internal string PresentMode { get; private set; }
        internal int DataRowCount { get; private set; }
        internal bool DisplayedTimeColumnPresent { get; private set; }
        internal bool FrameTypeColumnPresent { get; private set; }
        internal string ObservedGeneratedTechnology { get; private set; }

        internal CaptureCsvTopology(string application, int processId, string swapChainAddress,
            string presentRuntime, string presentMode, int dataRowCount,
            bool displayedTimeColumnPresent, bool frameTypeColumnPresent,
            string observedGeneratedTechnology)
        {
            Application = application;
            ProcessId = processId;
            SwapChainAddress = swapChainAddress;
            PresentRuntime = presentRuntime;
            PresentMode = presentMode;
            DataRowCount = dataRowCount;
            DisplayedTimeColumnPresent = displayedTimeColumnPresent;
            FrameTypeColumnPresent = frameTypeColumnPresent;
            ObservedGeneratedTechnology = observedGeneratedTechnology;
        }
    }

    internal static class PresentMonCsvAnalyzer
    {
        private const int MinimumFrames = 30;
        private const double MinimumDurationSeconds = 10.0;
        private const int MinimumFramesForP99Equivalent = 1000;
        private const int MinimumFramesForP999Equivalent = 10000;
        private const int MaximumRecords = 250001;
        private const int MaximumColumns = 256;
        private const int MaximumFieldCharacters = 65536;
        private const long MaximumCells = 5000000;
        private const long MaximumTotalCharacters = 33554432;
        private const double MaximumFrameIntervalMs = 60000.0;

        internal static CaptureCsvTopology InspectSingleTargetTopology(TextReader reader,
            string expectedApplication, int expectedProcessId)
        {
            if (reader == null) throw new ArgumentNullException("reader");
            if (String.IsNullOrWhiteSpace(expectedApplication) || expectedApplication != expectedApplication.Trim())
                throw new ArgumentException("The expected application name is invalid.", "expectedApplication");
            if (expectedProcessId <= 0) throw new ArgumentOutOfRangeException("expectedProcessId");
            IList<IList<string>> records = ReadRecords(reader);
            if (records.Count < 2) throw new InvalidDataException("The CSV does not contain a header and frame rows.");
            Dictionary<string, int> header = BuildHeader(records[0]);
            RequireColumns(header, "Application", "ProcessID", "SwapChainAddress", "PresentRuntime", "PresentMode", "DisplayedTime", "FrameType");
            int applicationIndex = header["Application"];
            int processIndex = header["ProcessID"];
            int swapChainIndex = header["SwapChainAddress"];
            int runtimeIndex = header["PresentRuntime"];
            int modeIndex = header["PresentMode"];
            int frameTypeIndex = header["FrameType"];
            int largestIndex = Math.Max(Math.Max(applicationIndex, processIndex),
                Math.Max(swapChainIndex, Math.Max(runtimeIndex, Math.Max(modeIndex, frameTypeIndex))));
            string topologyKey = null;
            string swapChain = null;
            string runtime = null;
            string mode = null;
            string generatedTechnology = "none";
            int applicationLabels = 0;
            int generated = 0;
            int unknown = 0;
            int dataRows = 0;
            for (int rowIndex = 1; rowIndex < records.Count; rowIndex++)
            {
                IList<string> row = records[rowIndex];
                if (row.Count == 1 && string.IsNullOrWhiteSpace(row[0])) continue;
                if (row.Count <= largestIndex)
                    throw new InvalidDataException("A capture row is missing required target/topology fields.");
                if (!string.Equals(row[applicationIndex], expectedApplication, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("The CSV contains a different application than the bound target.");
                int processId;
                if (!int.TryParse(row[processIndex], NumberStyles.None, CultureInfo.InvariantCulture, out processId) ||
                    processId != expectedProcessId)
                    throw new InvalidDataException("The CSV contains a different or invalid target process ID.");
                string currentSwapChain = row[swapChainIndex];
                string currentRuntime = row[runtimeIndex];
                string currentMode = row[modeIndex];
                if (String.IsNullOrWhiteSpace(currentSwapChain) || String.IsNullOrWhiteSpace(currentRuntime) ||
                    String.IsNullOrWhiteSpace(currentMode))
                    throw new InvalidDataException("The CSV contains an empty topology field.");
                string currentKey = LengthPrefixedKey(currentSwapChain, currentRuntime, currentMode);
                if (topologyKey == null)
                {
                    topologyKey = currentKey;
                    swapChain = currentSwapChain;
                    runtime = currentRuntime;
                    mode = currentMode;
                }
                else if (!string.Equals(topologyKey, currentKey, StringComparison.Ordinal))
                    throw new InvalidDataException("The capture contains multiple swap-chain/runtime/mode topologies.");
                ClassifyFrameType(row[frameTypeIndex], ref applicationLabels, ref generated,
                    ref unknown, ref generatedTechnology);
                dataRows++;
            }
            if (dataRows < MinimumFrames)
                throw new InvalidDataException("The capture does not contain enough target rows to establish topology.");
            if (unknown > 0)
                throw new InvalidDataException("The capture contains an unavailable or unsupported frame type.");
            return new CaptureCsvTopology(expectedApplication, expectedProcessId, swapChain,
                runtime, mode, dataRows, true, true, generatedTechnology);
        }

        public static RunMetrics Analyze(TextReader reader, ValidatedRunManifest validatedManifest)
        {
            if (reader == null) throw new ArgumentNullException("reader");
            if (validatedManifest == null) throw new ArgumentNullException("validatedManifest");
            RunManifest manifest = validatedManifest.Value;

            IList<IList<string>> records = ReadRecords(reader);
            if (records.Count < 2) throw new InvalidDataException("The CSV does not contain a header and frame rows.");

            Dictionary<string, int> header = BuildHeader(records[0]);
            RequireColumns(header, "Application", "ProcessID", "SwapChainAddress", "PresentRuntime", "PresentMode");
            string timingColumn = manifest.TimingBasis;
            if (!header.ContainsKey(timingColumn)) throw new InvalidDataException("The required timing column is missing: " + timingColumn);
            int timingIndex = header[timingColumn];
            int displayedIndex = FindColumn(header, "DisplayedTime");
            int frameTypeIndex = FindColumn(header, "FrameType");
            int applicationIndex = header["Application"];
            int processIndex = header["ProcessID"];
            int swapChainIndex = header["SwapChainAddress"];
            int runtimeIndex = header["PresentRuntime"];
            int presentModeIndex = header["PresentMode"];

            List<double> frameTimes = new List<double>();
            int undisplayed = 0;
            int invalid = 0;
            int applicationLabels = 0;
            int generated = 0;
            int unknownType = 0;
            int frameTypeRows = 0;
            string observedGeneratedTechnology = "none";
            string observedRuntime = null;
            string observedPresentMode = null;

            for (int rowIndex = 1; rowIndex < records.Count; rowIndex++)
            {
                IList<string> row = records[rowIndex];
                if (row.Count == 1 && string.IsNullOrWhiteSpace(row[0])) continue;
                RequireTarget(row, applicationIndex, processIndex, swapChainIndex, runtimeIndex, presentModeIndex, manifest, ref observedRuntime, ref observedPresentMode);

                if (frameTypeIndex >= 0)
                {
                    frameTypeRows++;
                    ClassifyFrameType(frameTypeIndex >= row.Count ? null : row[frameTypeIndex], ref applicationLabels, ref generated, ref unknownType, ref observedGeneratedTechnology);
                }

                if (displayedIndex >= 0)
                {
                    if (displayedIndex >= row.Count) invalid++;
                    else if (IsLiteralNa(row[displayedIndex])) undisplayed++;
                    else
                    {
                        double displayedValue;
                        if (!TryPositiveFinite(row[displayedIndex], out displayedValue)) invalid++;
                    }
                }

                double frameTime;
                if (timingIndex >= row.Count || IsLiteralNa(row[timingIndex]))
                {
                    if (timingColumn == "DisplayedTime") continue;
                    invalid++;
                    continue;
                }
                if (!TryPositiveFinite(row[timingIndex], out frameTime)) { invalid++; continue; }

                frameTimes.Add(frameTime);
            }

            if (invalid > 0) throw new InvalidDataException("The selected timing column contains " + invalid + " malformed or unavailable row(s).");

            if (frameTimes.Count < MinimumFrames)
                throw new InvalidDataException("At least " + MinimumFrames + " valid frame intervals are required; found " + frameTimes.Count + ".");

            frameTimes.Sort();
            double totalMs = frameTimes.Sum();
            if (totalMs / 1000.0 < MinimumDurationSeconds) throw new InvalidDataException("At least " + MinimumDurationSeconds.ToString("0", CultureInfo.InvariantCulture) + " seconds of valid timing data are required.");
            double median = Percentile(frameTimes, 0.50);
            double p95 = Percentile(frameTimes, 0.95);
            double p99 = Percentile(frameTimes, 0.99);
            double p999 = frameTimes.Count >= MinimumFramesForP999Equivalent ? Percentile(frameTimes, 0.999) : double.NaN;
            double mad = Median(frameTimes.Select(value => Math.Abs(value - median)).OrderBy(value => value).ToList());
            double iqr = Percentile(frameTimes, 0.75) - Percentile(frameTimes, 0.25);
            double slowFrameThreshold = manifest.SlowFrameThresholdMs;

            RunMetrics result = new RunMetrics();
            result.RunId = manifest.RunId;
            result.ProtocolKey = manifest.ProtocolKey;
            result.ConfigurationLabel = manifest.ConfigurationLabel;
            result.TimingColumn = timingColumn;
            result.PresentRuntime = observedRuntime;
            result.PresentMode = observedPresentMode;
            result.CaptureSequenceIndex = manifest.CaptureSequenceIndex;
            result.RepetitionIndex = manifest.RepetitionIndex;
            result.PlannedRepetitions = manifest.PlannedRepetitions;
            result.PlannedCaptureSeconds = manifest.PlannedCaptureSeconds;
            result.FrameGenerationState = manifest.FrameGenerationState;
            result.FrameGenerationTechnology = manifest.FrameGenerationTechnology;
            result.CaptureTopologyKey = LengthPrefixedKey(manifest.TargetApplication, observedRuntime, observedPresentMode, timingColumn, manifest.CaptureSchemaVersion, frameTypeIndex >= 0 ? "FrameType" : "NoFrameType");
            result.CapturedRows = records.Count - 1;
            result.ValidFrameCount = frameTimes.Count;
            result.UndisplayedFrameCount = undisplayed;
            result.InvalidFrameRowCount = invalid;
            result.ApplicationFrameLabelCount = applicationLabels;
            result.GeneratedFrameCount = generated;
            result.UnknownFrameTypeCount = unknownType;
            result.DurationSeconds = totalMs / 1000.0;
            result.AverageFps = totalMs > 0 ? frameTimes.Count * 1000.0 / totalMs : 0;
            result.MedianFrameTimeMs = median;
            result.P95FrameTimeMs = p95;
            result.P99FrameTimeMs = p99;
            result.P99EquivalentFps = frameTimes.Count >= MinimumFramesForP99Equivalent && p99 > 0 ? 1000.0 / p99 : double.NaN;
            result.P999EquivalentFps = !double.IsNaN(p999) && p999 > 0 ? 1000.0 / p999 : double.NaN;
            result.MedianAbsoluteDeviationMs = mad;
            result.InterquartileRangeMs = iqr;
            result.SlowFrameThresholdMs = slowFrameThreshold;
            result.SlowFrameCount = frameTimes.Count(value => value > slowFrameThreshold);
            result.SlowFramesPerThousand = result.SlowFrameCount * 1000.0 / frameTimes.Count;
            result.UndisplayedMetricAvailable = displayedIndex >= 0;
            result.UndisplayedFramesPerThousand = displayedIndex >= 0 ? undisplayed * 1000.0 / Math.Max(1, records.Count - 1) : double.NaN;
            result.GeneratedFramesPerThousand = frameTypeRows > 0 ? generated * 1000.0 / frameTypeRows : double.NaN;
            result.FrameTypeColumnPresent = frameTypeIndex >= 0;

            if (displayedIndex < 0)
                result.Warnings.Add("DisplayedTime was unavailable; undisplayed-frame counts cannot be established.");
            if (frameTypeIndex < 0)
                result.Warnings.Add("FrameType was unavailable; application and generated frame labels cannot be separated.");
            if (generated > 0)
                result.Warnings.Add("Generated frames are present; displayed FPS must not be described as rendered FPS.");
            if (unknownType > 0)
                result.Warnings.Add("An unsupported or unavailable frame type was observed; comparison eligibility is blocked.");
            if (frameTimes.Count < MinimumFramesForP99Equivalent)
                result.Warnings.Add("P99-equivalent FPS is unavailable because the capture contains fewer than 1,000 valid intervals.");
            if (frameTimes.Count < MinimumFramesForP999Equivalent)
                result.Warnings.Add("P99.9-equivalent FPS is unavailable because the capture contains fewer than 10,000 valid intervals.");
            if (manifest.FrameGenerationState == "off" && generated > 0)
                throw new InvalidDataException("Generated frames were observed while the manifest declares frame generation off.");
            if (manifest.FrameGenerationState == "on" && (frameTypeIndex < 0 || generated == 0))
                throw new InvalidDataException("The manifest declares frame generation on, but generated frames were not verified.");
            if (generated > 0 && !string.Equals(observedGeneratedTechnology, manifest.FrameGenerationTechnology, StringComparison.Ordinal))
                throw new InvalidDataException("The observed generated-frame technology does not match the manifest.");

            return result;
        }

        private static void ClassifyFrameType(string value, ref int applicationLabels, ref int generated, ref int unknown, ref string observedGeneratedTechnology)
        {
            string token = value ?? string.Empty;
            if (token.Equals("Application", StringComparison.Ordinal)) applicationLabels++;
            else if (token.Equals("Intel XeSS-FG", StringComparison.Ordinal))
            {
                RequireSingleGeneratedTechnology(ref observedGeneratedTechnology, "intel-xess-fg");
                generated++;
            }
            else if (token.Equals("AMD AFMF", StringComparison.Ordinal))
            {
                RequireSingleGeneratedTechnology(ref observedGeneratedTechnology, "amd-afmf");
                generated++;
            }
            else if (token.Length == 0) unknown++;
            else throw new InvalidDataException("Unsupported FrameType token for the expected capture schema: " + token);
        }

        private static void RequireSingleGeneratedTechnology(ref string observed, string current)
        {
            if (observed != "none" && observed != current)
                throw new InvalidDataException("A capture cannot combine multiple generated-frame technologies.");
            observed = current;
        }

        private static Dictionary<string, int> BuildHeader(IList<string> fields)
        {
            Dictionary<string, int> header = new Dictionary<string, int>(StringComparer.Ordinal);
            for (int i = 0; i < fields.Count; i++)
            {
                string name = fields[i] ?? string.Empty;
                if (name.Length == 0) throw new InvalidDataException("The CSV contains an empty column name.");
                if (!string.Equals(name, name.Trim(), StringComparison.Ordinal) || name.IndexOf('\uFEFF') >= 0)
                    throw new InvalidDataException("CSV column names must match the pinned schema exactly and must not contain a BOM.");
                if (header.ContainsKey(name)) throw new InvalidDataException("The CSV contains a duplicate column: " + name);
                header.Add(name, i);
            }
            return header;
        }

        private static void RequireColumns(Dictionary<string, int> header, params string[] names)
        {
            foreach (string name in names)
                if (!header.ContainsKey(name)) throw new InvalidDataException("The required capture column is missing: " + name);
        }

        private static void RequireTarget(IList<string> row, int applicationIndex, int processIndex, int swapChainIndex, int runtimeIndex, int presentModeIndex, RunManifest manifest, ref string observedRuntime, ref string observedPresentMode)
        {
            int largestIndex = Math.Max(Math.Max(applicationIndex, processIndex), Math.Max(swapChainIndex, Math.Max(runtimeIndex, presentModeIndex)));
            if (row.Count <= largestIndex) throw new InvalidDataException("A capture row is missing required target/topology fields.");
            if (!string.Equals(row[applicationIndex], manifest.TargetApplication, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("The CSV contains a different application than the manifest target.");
            int processId;
            if (!int.TryParse(row[processIndex], NumberStyles.Integer, CultureInfo.InvariantCulture, out processId) || processId != manifest.TargetProcessId)
                throw new InvalidDataException("The CSV contains a different or invalid process ID.");
            if (!string.Equals(row[swapChainIndex], manifest.TargetSwapChainAddress, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("The CSV contains a different swap chain than the manifest target.");
            string runtime = row[runtimeIndex];
            string mode = row[presentModeIndex];
            if (runtime.Length == 0 || mode.Length == 0) throw new InvalidDataException("The CSV contains an empty presentation runtime or mode.");
            if (!string.Equals(runtime, manifest.ExpectedPresentRuntime, StringComparison.Ordinal)) throw new InvalidDataException("The CSV presentation runtime does not match the manifest.");
            if (!string.Equals(mode, manifest.ExpectedPresentMode, StringComparison.Ordinal)) throw new InvalidDataException("The CSV presentation mode does not match the manifest.");
            if (observedRuntime == null) observedRuntime = runtime;
            else if (!string.Equals(observedRuntime, runtime, StringComparison.Ordinal)) throw new InvalidDataException("The CSV mixes presentation runtimes.");
            if (observedPresentMode == null) observedPresentMode = mode;
            else if (!string.Equals(observedPresentMode, mode, StringComparison.Ordinal)) throw new InvalidDataException("The CSV mixes presentation modes.");
        }

        private static bool IsLiteralNa(string text)
        {
            string token = text ?? string.Empty;
            return token.Equals("NA", StringComparison.Ordinal);
        }

        private static string LengthPrefixedKey(params string[] values)
        {
            return string.Concat(values.Select(value =>
            {
                string normalized = (value ?? string.Empty).Trim().ToUpperInvariant();
                return normalized.Length.ToString(CultureInfo.InvariantCulture) + ":" + normalized;
            }));
        }

        private static int FindColumn(Dictionary<string, int> header, string name)
        {
            int index;
            return header.TryGetValue(name, out index) ? index : -1;
        }

        private static bool TryPositiveFinite(string text, out double value)
        {
            if (!double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value)) return false;
            return value > 0 && value <= MaximumFrameIntervalMs && !double.IsNaN(value) && !double.IsInfinity(value);
        }

        internal static double Percentile(IList<double> sorted, double percentile)
        {
            if (sorted == null || sorted.Count == 0) throw new ArgumentException("A non-empty sorted sample is required.");
            if (percentile < 0 || percentile > 1) throw new ArgumentOutOfRangeException("percentile");
            if (sorted.Count == 1) return sorted[0];
            double position = percentile * (sorted.Count - 1);
            int lower = (int)Math.Floor(position);
            int upper = (int)Math.Ceiling(position);
            if (lower == upper) return sorted[lower];
            double fraction = position - lower;
            return sorted[lower] + ((sorted[upper] - sorted[lower]) * fraction);
        }

        internal static double Median(IList<double> sorted)
        {
            return Percentile(sorted, 0.50);
        }

        private static IList<IList<string>> ReadRecords(TextReader reader)
        {
            List<IList<string>> records = new List<IList<string>>();
            List<string> fields = new List<string>();
            System.Text.StringBuilder field = new System.Text.StringBuilder();
            bool quoted = false;
            bool quoteClosed = false;
            long totalCharacters = 0;
            long totalCells = 0;
            long totalRawCharacters = 0;
            while (true)
            {
                int next = reader.Read();
                if (next < 0)
                {
                    if (quoted) throw new InvalidDataException("The CSV ends inside a quoted field.");
                    if (field.Length > 0 || fields.Count > 0)
                    {
                        fields.Add(field.ToString());
                        AddRecord(records, fields, ref totalCells);
                    }
                    break;
                }
                CountRawCharacter(ref totalRawCharacters);
                char ch = (char)next;
                if (quoted)
                {
                    if (ch == '"')
                    {
                        if (reader.Peek() == '"') { reader.Read(); CountRawCharacter(ref totalRawCharacters); AppendBounded(field, '"', ref totalCharacters); }
                        else { quoted = false; quoteClosed = true; }
                    }
                    else AppendBounded(field, ch, ref totalCharacters);
                    continue;
                }
                if (quoteClosed && ch != ',' && ch != '\r' && ch != '\n' && !char.IsWhiteSpace(ch))
                    throw new InvalidDataException("Unexpected characters follow a quoted CSV field.");
                if (ch == '"' && field.Length == 0) { quoted = true; quoteClosed = false; continue; }
                if (ch == ',')
                {
                    fields.Add(field.ToString());
                    if (fields.Count > MaximumColumns) throw new InvalidDataException("The CSV exceeds the supported column limit.");
                    field.Length = 0;
                    quoteClosed = false;
                    continue;
                }
                if (ch == '\r' || ch == '\n')
                {
                    if (ch == '\r' && reader.Peek() == '\n') { reader.Read(); CountRawCharacter(ref totalRawCharacters); }
                    fields.Add(field.ToString());
                    field.Length = 0;
                    if (!(fields.Count == 1 && fields[0].Length == 0)) AddRecord(records, fields, ref totalCells);
                    fields = new List<string>();
                    quoteClosed = false;
                    continue;
                }
                AppendBounded(field, ch, ref totalCharacters);
            }
            return records;
        }

        private static void AppendBounded(System.Text.StringBuilder field, char value, ref long totalCharacters)
        {
            field.Append(value);
            totalCharacters++;
            if (field.Length > MaximumFieldCharacters) throw new InvalidDataException("A CSV field exceeds the supported size limit.");
            if (totalCharacters > MaximumTotalCharacters) throw new InvalidDataException("The CSV exceeds the supported total character budget.");
        }

        private static void CountRawCharacter(ref long totalRawCharacters)
        {
            totalRawCharacters++;
            if (totalRawCharacters > MaximumTotalCharacters) throw new InvalidDataException("The CSV exceeds the supported total input budget.");
        }

        private static void AddRecord(List<IList<string>> records, List<string> fields, ref long totalCells)
        {
            if (fields.Count > MaximumColumns) throw new InvalidDataException("The CSV exceeds the supported column limit.");
            if (records.Count >= MaximumRecords) throw new InvalidDataException("The CSV exceeds the supported frame-row limit.");
            totalCells += fields.Count;
            if (totalCells > MaximumCells) throw new InvalidDataException("The CSV exceeds the supported cell budget.");
            records.Add(fields);
        }
    }

    internal static class RunComparison
    {
        public static ComparisonResult Compare(IList<RunMetrics> baseline, IList<RunMetrics> candidate)
        {
            if (baseline == null || candidate == null) throw new ArgumentNullException(baseline == null ? "baseline" : "candidate");
            ComparisonResult result = new ComparisonResult();
            result.Verdict = ComparisonVerdict.Inconclusive;
            result.EligibleForAutomaticKeep = false;
            if (baseline.Any(run => run == null) || candidate.Any(run => run == null))
            {
                result.Reasons.Add("Null run records are not valid comparison evidence.");
                return result;
            }
            if (baseline.Count < 3 || candidate.Count < 3)
            {
                result.Reasons.Add("At least three successful baseline and candidate runs are required.");
                return result;
            }
            if (baseline.Count != candidate.Count)
            {
                result.Reasons.Add("Baseline and candidate groups must contain the same number of successful repetitions.");
                return result;
            }

            if (baseline.Select(run => run.RunId).Concat(candidate.Select(run => run.RunId)).Any(string.IsNullOrWhiteSpace) ||
                baseline.Select(run => run.RunId).Concat(candidate.Select(run => run.RunId)).Distinct(StringComparer.OrdinalIgnoreCase).Count() != baseline.Count + candidate.Count)
            {
                result.Reasons.Add("Every capture must have a unique non-empty run ID.");
                return result;
            }

            string key = baseline[0].ProtocolKey;
            if (string.IsNullOrWhiteSpace(key) || baseline.Any(run => run.ProtocolKey != key) || candidate.Any(run => run.ProtocolKey != key))
            {
                result.Reasons.Add("The runs do not share the same measurement protocol and scenario identity.");
                return result;
            }
            string topology = baseline[0].CaptureTopologyKey;
            string timingBasis = baseline[0].TimingColumn;
            if (string.IsNullOrWhiteSpace(topology) || string.IsNullOrWhiteSpace(timingBasis) ||
                baseline.Any(run => run.CaptureTopologyKey != topology || run.TimingColumn != timingBasis) ||
                candidate.Any(run => run.CaptureTopologyKey != topology || run.TimingColumn != timingBasis))
            {
                result.Reasons.Add("The runs do not share one capture topology and timing basis.");
                return result;
            }

            string baselineLabel = baseline[0].ConfigurationLabel;
            string candidateLabel = candidate[0].ConfigurationLabel;
            if (string.IsNullOrWhiteSpace(baselineLabel) || string.IsNullOrWhiteSpace(candidateLabel) ||
                baseline.Any(run => !string.Equals(run.ConfigurationLabel, baselineLabel, StringComparison.Ordinal)) ||
                candidate.Any(run => !string.Equals(run.ConfigurationLabel, candidateLabel, StringComparison.Ordinal)) ||
                string.Equals(baselineLabel, candidateLabel, StringComparison.Ordinal))
            {
                result.Reasons.Add("Baseline and candidate runs must use two distinct, internally consistent configuration labels.");
                return result;
            }

            IList<RunMetrics> allRuns = baseline.Concat(candidate).ToList();
            if (allRuns.Any(run => !HasValidDecisionMetrics(run)))
            {
                result.Reasons.Add("One or more runs contains invalid or non-finite decision metrics.");
                return result;
            }
            int plannedRepetitions = baseline[0].PlannedRepetitions;
            if (plannedRepetitions < 3 || baseline.Count != plannedRepetitions ||
                allRuns.Any(run => run.PlannedRepetitions != plannedRepetitions) ||
                !HasCompleteRepetitions(baseline, plannedRepetitions) || !HasCompleteRepetitions(candidate, plannedRepetitions))
            {
                result.Reasons.Add("The comparison does not contain every planned repetition exactly once for each configuration.");
                return result;
            }
            IList<RunMetrics> orderedRuns = allRuns.OrderBy(run => run.CaptureSequenceIndex).ToList();
            if (orderedRuns.Select(run => run.CaptureSequenceIndex).Distinct().Count() != orderedRuns.Count ||
                orderedRuns.Any(run => run.CaptureSequenceIndex <= 0) ||
                orderedRuns.Select((run, index) => run.CaptureSequenceIndex == index + 1).Any(valid => !valid) ||
                orderedRuns.Skip(1).Where((run, index) => string.Equals(run.ConfigurationLabel, orderedRuns[index].ConfigurationLabel, StringComparison.Ordinal)).Any())
            {
                result.Reasons.Add("Captures must have unique sequence indices and alternate baseline/candidate configurations.");
                return result;
            }
            double shortestDuration = allRuns.Min(run => run.DurationSeconds);
            double longestDuration = allRuns.Max(run => run.DurationSeconds);
            if (shortestDuration < 30.0 || longestDuration / shortestDuration > 1.05 ||
                allRuns.Any(run => run.PlannedCaptureSeconds < 30 || Math.Abs(run.DurationSeconds - run.PlannedCaptureSeconds) / run.PlannedCaptureSeconds > 0.05))
            {
                result.Reasons.Add("Capture durations differ by more than five percent or are too short.");
                return result;
            }

            List<double> baselineFps = baseline.Select(run => run.AverageFps).OrderBy(value => value).ToList();
            List<double> candidateFps = candidate.Select(run => run.AverageFps).OrderBy(value => value).ToList();
            List<double> baselineP99 = baseline.Select(run => run.P99FrameTimeMs).OrderBy(value => value).ToList();
            List<double> candidateP99 = candidate.Select(run => run.P99FrameTimeMs).OrderBy(value => value).ToList();

            result.BaselineMedianFps = PresentMonCsvAnalyzer.Median(baselineFps);
            result.CandidateMedianFps = PresentMonCsvAnalyzer.Median(candidateFps);
            result.BaselineMedianP99Ms = PresentMonCsvAnalyzer.Median(baselineP99);
            result.CandidateMedianP99Ms = PresentMonCsvAnalyzer.Median(candidateP99);
            result.FpsChangePercent = PercentChange(result.BaselineMedianFps, result.CandidateMedianFps);
            result.P99ChangePercent = PercentChange(result.BaselineMedianP99Ms, result.CandidateMedianP99Ms);

            double fpsNoise = Math.Max(RelativeMad(baselineFps), RelativeMad(candidateFps));
            double p99Noise = Math.Max(RelativeMad(baselineP99), RelativeMad(candidateP99));
            result.DecisionThresholdPercent = Math.Max(3.0, Math.Max(fpsNoise, p99Noise) * 200.0);

            if (result.DecisionThresholdPercent > 10.0 || Math.Max(RelativeRange(baselineFps), RelativeRange(candidateFps)) > 0.10 || Math.Max(RelativeRange(baselineP99), RelativeRange(candidateP99)) > 0.10)
            {
                result.Reasons.Add("Run-to-run variation is too high for a confident decision.");
                return result;
            }

            bool fpsBetter = result.FpsChangePercent >= result.DecisionThresholdPercent;
            bool fpsWorse = result.FpsChangePercent <= -result.DecisionThresholdPercent;
            bool p99Better = result.P99ChangePercent <= -result.DecisionThresholdPercent;
            bool p99Worse = result.P99ChangePercent >= result.DecisionThresholdPercent;
            double baselineSlowRate = PresentMonCsvAnalyzer.Median(baseline.Select(run => run.SlowFramesPerThousand).OrderBy(value => value).ToList());
            double candidateSlowRate = PresentMonCsvAnalyzer.Median(candidate.Select(run => run.SlowFramesPerThousand).OrderBy(value => value).ToList());
            double baselineUndisplayedRate = PresentMonCsvAnalyzer.Median(baseline.Select(run => run.UndisplayedFramesPerThousand).OrderBy(value => value).ToList());
            double candidateUndisplayedRate = PresentMonCsvAnalyzer.Median(candidate.Select(run => run.UndisplayedFramesPerThousand).OrderBy(value => value).ToList());
            bool reliabilityRegression = candidateSlowRate > baselineSlowRate + 1.0 || candidateUndisplayedRate > baselineUndisplayedRate + 1.0;
            double generatedRateSpread = allRuns.Max(run => run.GeneratedFramesPerThousand) - allRuns.Min(run => run.GeneratedFramesPerThousand);
            if (generatedRateSpread > 5.0)
            {
                result.Reasons.Add("Generated-frame shares are not consistent across the comparison.");
                return result;
            }

            if (reliabilityRegression || fpsWorse || p99Worse)
            {
                result.Verdict = ComparisonVerdict.Worse;
                if (reliabilityRegression) result.Reasons.Add("Slow-frame or undisplayed-frame rates regressed.");
                if (fpsWorse) result.Reasons.Add("Median run FPS decreased beyond the repeatability threshold.");
                if (p99Worse) result.Reasons.Add("P99 frame time increased beyond the repeatability threshold.");
                return result;
            }
            if ((fpsBetter || p99Better) && !(fpsWorse || p99Worse))
            {
                result.Verdict = ComparisonVerdict.Improved;
                if (fpsBetter) result.Reasons.Add("Median run FPS improved beyond the repeatability threshold.");
                if (p99Better) result.Reasons.Add("P99 frame time improved beyond the repeatability threshold.");
                result.Reasons.Add("This is an experimental summary, not an automatic Keep decision; stability, thermal, visual-quality, crash, and WHEA gates remain required.");
                return result;
            }
            if (!fpsBetter && !fpsWorse && !p99Better && !p99Worse)
            {
                result.Verdict = ComparisonVerdict.NoMaterialDifference;
                result.Reasons.Add("Observed changes did not exceed the repeatability threshold.");
                return result;
            }

            result.Reasons.Add("The metrics disagree, so the result is inconclusive.");
            return result;
        }

        private static double PercentChange(double baseline, double candidate)
        {
            return baseline == 0 ? 0 : ((candidate - baseline) / baseline) * 100.0;
        }

        private static bool HasValidDecisionMetrics(RunMetrics run)
        {
            return run != null && run.ValidFrameCount >= 1000 && IsFinitePositive(run.DurationSeconds) && IsFinitePositive(run.AverageFps) &&
                IsFinitePositive(run.P99FrameTimeMs) && IsFiniteNonNegative(run.SlowFramesPerThousand) &&
                IsFiniteNonNegative(run.UndisplayedFramesPerThousand) && IsFiniteNonNegative(run.GeneratedFramesPerThousand) &&
                run.InvalidFrameRowCount == 0 && run.UnknownFrameTypeCount == 0 &&
                run.UndisplayedMetricAvailable && run.FrameTypeColumnPresent && run.FrameGenerationState == "off" &&
                run.FrameGenerationTechnology == "none" && run.GeneratedFrameCount == 0;
        }

        private static bool HasCompleteRepetitions(IEnumerable<RunMetrics> runs, int planned)
        {
            IList<int> indices = runs.Select(run => run.RepetitionIndex).OrderBy(value => value).ToList();
            return indices.Count == planned && indices.Select((value, index) => value == index + 1).All(valid => valid);
        }

        private static bool IsFinitePositive(double value)
        {
            return value > 0 && !double.IsNaN(value) && !double.IsInfinity(value);
        }

        private static bool IsFiniteNonNegative(double value)
        {
            return value >= 0 && !double.IsNaN(value) && !double.IsInfinity(value);
        }

        private static double RelativeMad(IList<double> sorted)
        {
            double median = PresentMonCsvAnalyzer.Median(sorted);
            if (median == 0) return 0;
            List<double> deviations = sorted.Select(value => Math.Abs(value - median)).OrderBy(value => value).ToList();
            return PresentMonCsvAnalyzer.Median(deviations) / Math.Abs(median);
        }

        private static double RelativeRange(IList<double> sorted)
        {
            double median = PresentMonCsvAnalyzer.Median(sorted);
            return median == 0 ? 0 : (sorted[sorted.Count - 1] - sorted[0]) / Math.Abs(median);
        }
    }
}
