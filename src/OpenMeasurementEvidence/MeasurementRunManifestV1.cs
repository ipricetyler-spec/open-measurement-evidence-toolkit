using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace OpenMeasurementEvidence
{
    internal sealed class MeasurementRunManifestV1
    {
        private readonly RunManifest analysisManifest;
        private readonly IList<string> limitations;

        internal string RunId { get { return analysisManifest.RunId; } }
        internal string CapturePolicyHash { get { return analysisManifest.CapturePolicyHash; } }
        internal string CaptureInvocationHash { get { return analysisManifest.CaptureInvocationHash; } }
        internal string ComparisonContextId { get { return analysisManifest.ComparisonContextId; } }
        internal string ChangedVariablesKey { get { return analysisManifest.ChangedVariablesKey; } }
        internal string CaptureSource { get { return analysisManifest.CaptureSource; } }
        internal string CaptureSourceVersion { get { return analysisManifest.CaptureSourceVersion; } }
        internal string CaptureSourceSha256 { get { return analysisManifest.CaptureSourceSha256; } }
        internal string CaptureSchemaVersion { get { return analysisManifest.CaptureSchemaVersion; } }
        internal string TimingDefinitionVersion { get { return analysisManifest.TimingDefinitionVersion; } }
        internal string TimingBasis { get { return analysisManifest.TimingBasis; } }
        internal string GameExecutable { get { return analysisManifest.GameExecutable; } }
        internal string GameBuild { get { return analysisManifest.GameBuild; } }
        internal string Scenario { get { return analysisManifest.Scenario; } }
        internal string GraphicsApi { get { return analysisManifest.GraphicsApi; } }
        internal string DisplayMode { get { return analysisManifest.DisplayMode; } }
        internal string GameSettingsHash { get { return analysisManifest.GameSettingsHash; } }
        internal string FrameGenerationState { get { return analysisManifest.FrameGenerationState; } }
        internal string FrameGenerationTechnology { get { return analysisManifest.FrameGenerationTechnology; } }
        internal string PresentRuntime { get { return analysisManifest.ExpectedPresentRuntime; } }
        internal string PresentMode { get { return analysisManifest.ExpectedPresentMode; } }
        internal DateTimeOffset CreatedUtc { get; private set; }
        internal string AntiCheatStatus { get; private set; }
        internal string WindowsBuild { get; private set; }
        internal string Gpu { get; private set; }
        internal string GpuDriver { get; private set; }
        internal decimal RefreshHz { get; private set; }
        internal string Hags { get; private set; }
        internal string PowerPlan { get; private set; }
        internal string GameSettingsRelativePath { get; private set; }
        internal string ConfigurationRole { get; private set; }
        internal string CsvRelativePath { get; private set; }
        internal string CsvSha256 { get; private set; }
        internal IEnumerable<string> Limitations { get { return limitations; } }

        internal MeasurementRunManifestV1(
            MeasurementRunManifestV1Validator.ConstructionProof proof,
            RunManifest analysisManifest, DateTimeOffset createdUtc, string antiCheatStatus,
            string windowsBuild, string gpu, string gpuDriver, decimal refreshHz, string hags,
            string powerPlan, string gameSettingsRelativePath, string configurationRole,
            string csvRelativePath, string csvSha256, IEnumerable<string> limitations)
        {
            if (!MeasurementRunManifestV1Validator.IsConstructionProofValid(proof))
                throw new InvalidOperationException("Only the manifest validator can construct this typed result.");
            if (analysisManifest == null) throw new ArgumentNullException("analysisManifest");
            if (limitations == null) throw new ArgumentNullException("limitations");
            this.analysisManifest = CopyAnalysisManifest(analysisManifest);
            this.limitations = new List<string>(limitations).AsReadOnly();
            CreatedUtc = createdUtc;
            AntiCheatStatus = antiCheatStatus;
            WindowsBuild = windowsBuild;
            Gpu = gpu;
            GpuDriver = gpuDriver;
            RefreshHz = refreshHz;
            Hags = hags;
            PowerPlan = powerPlan;
            GameSettingsRelativePath = gameSettingsRelativePath;
            ConfigurationRole = configurationRole;
            CsvRelativePath = csvRelativePath;
            CsvSha256 = csvSha256;
        }

        private static RunManifest CopyAnalysisManifest(RunManifest source)
        {
            return new RunManifest
            {
                RunId = source.RunId,
                ProtocolVersion = source.ProtocolVersion,
                CaptureSource = source.CaptureSource,
                GameExecutable = source.GameExecutable,
                GameBuild = source.GameBuild,
                Scenario = source.Scenario,
                GraphicsApi = source.GraphicsApi,
                DisplayMode = source.DisplayMode,
                ComparisonContextId = source.ComparisonContextId,
                ConfigurationLabel = source.ConfigurationLabel,
                CaptureSchemaVersion = source.CaptureSchemaVersion,
                TimingBasis = source.TimingBasis,
                TargetApplication = source.TargetApplication,
                TargetProcessId = source.TargetProcessId,
                TargetSwapChainAddress = source.TargetSwapChainAddress,
                PlannedCaptureSeconds = source.PlannedCaptureSeconds,
                SlowFrameThresholdMs = source.SlowFrameThresholdMs,
                CaptureSequenceIndex = source.CaptureSequenceIndex,
                CaptureSourceVersion = source.CaptureSourceVersion,
                CaptureSourceSha256 = source.CaptureSourceSha256,
                CapturePolicyHash = source.CapturePolicyHash,
                CaptureInvocationHash = source.CaptureInvocationHash,
                SessionName = source.SessionName,
                NoTrackInput = source.NoTrackInput,
                GameSettingsHash = source.GameSettingsHash,
                ChangedVariablesKey = source.ChangedVariablesKey,
                TimingDefinitionVersion = source.TimingDefinitionVersion,
                FrameGenerationState = source.FrameGenerationState,
                FrameGenerationTechnology = source.FrameGenerationTechnology,
                RepetitionIndex = source.RepetitionIndex,
                PlannedRepetitions = source.PlannedRepetitions,
                ExpectedPresentRuntime = source.ExpectedPresentRuntime,
                ExpectedPresentMode = source.ExpectedPresentMode
            };
        }

        internal RunManifest CopyForEvidenceBinding(EvidenceBundleV1.ConsistencyBindingProof proof)
        {
            if (!EvidenceBundleV1.IsConsistencyProofValid(proof))
                throw new InvalidOperationException("Only the complete evidence binder can release an analyzer manifest.");
            return CopyAnalysisManifest(analysisManifest);
        }
    }

    internal static class MeasurementRunManifestV1Validator
    {
        internal sealed class ConstructionProof
        {
            internal ConstructionProof() { }
        }
        private static readonly ConstructionProof ValidConstructionProof = new ConstructionProof();

        internal static bool IsConstructionProofValid(ConstructionProof proof)
        {
            return object.ReferenceEquals(proof, ValidConstructionProof);
        }
        private static readonly string[] RootProperties = { "schemaVersion", "runId", "createdUtc", "capture", "game", "protocol", "environment", "configuration", "evidence", "limitations" };
        private static readonly string[] CaptureProperties = { "source", "sourceVersion", "sourceSha256", "captureSchemaVersion", "timingDefinitionVersion", "timingBasis", "targetProcessId", "targetSwapChainAddress", "sessionName", "noTrackInput", "capturePolicyHash", "captureInvocationHash" };
        private static readonly string[] GameProperties = { "executableBaseName", "buildId", "scenarioId", "graphicsApi", "antiCheatStatus" };
        private static readonly string[] ProtocolProperties = { "protocolVersion", "comparisonContextId", "warmupSeconds", "captureSeconds", "repetitionIndex", "plannedRepetitions", "captureSequenceIndex", "slowFrameThresholdMs" };
        private static readonly string[] EnvironmentProperties = { "windowsBuild", "gpu", "gpuDriver", "displayMode", "refreshHz", "hags", "powerPlan", "gameSettingsRelativePath", "gameSettingsHash", "presentRuntime", "presentMode", "frameGenerationState", "frameGenerationTechnology" };
        private static readonly string[] ConfigurationProperties = { "label", "role", "changedVariables" };
        private static readonly string[] EvidenceProperties = { "csvRelativePath", "csvSha256" };
        private static readonly HashSet<string> ReservedExecutableNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "CON", "PRN", "AUX", "NUL", "CLOCK$",
            "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
            "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9",
            "COM¹", "COM²", "COM³", "LPT¹", "LPT²", "LPT³"
        };

#if MEASUREMENT_TESTS
        internal static MeasurementRunManifestV1 ParseForTests(string json)
        {
            StrictJsonObject root = StrictJsonManifestParser.ParseForTests(json) as StrictJsonObject;
            if (root == null) throw new InvalidDataException("The run manifest root must be a JSON object.");
            return ValidateTree(root);
        }
#endif

        internal static MeasurementRunManifestV1 ParseVerifiedManifest(EvidencePathPolicy.VerifiedEvidenceFile evidence)
        {
            return ValidateTree(StrictJsonManifestParser.ParseVerifiedManifest(evidence));
        }

        private static MeasurementRunManifestV1 ValidateTree(StrictJsonObject root)
        {
            RequireExactProperties(root, "manifest", RootProperties);
            RequireInteger(root, "schemaVersion", 1, 1);

            string runId = RequireUuidV4(root, "runId");
            DateTimeOffset createdUtc = RequireUtcTimestamp(root, "createdUtc");
            StrictJsonObject capture = RequireObject(root, "capture");
            StrictJsonObject game = RequireObject(root, "game");
            StrictJsonObject protocol = RequireObject(root, "protocol");
            StrictJsonObject environment = RequireObject(root, "environment");
            StrictJsonObject configuration = RequireObject(root, "configuration");
            StrictJsonObject evidence = RequireObject(root, "evidence");
            StrictJsonArray limitations = RequireArray(root, "limitations");

            RequireExactProperties(capture, "capture", CaptureProperties);
            RequireExactProperties(game, "game", GameProperties);
            RequireExactProperties(protocol, "protocol", ProtocolProperties);
            RequireAllowedProperties(environment, "environment", EnvironmentProperties, new[] { "notes" });
            RequireExactProperties(configuration, "configuration", ConfigurationProperties);
            RequireExactProperties(evidence, "evidence", EvidenceProperties);

            string captureSource = RequireText(capture, "source", 1, 128);
            string captureSourceVersion = RequireText(capture, "sourceVersion", 1, 64);
            string captureSourceSha256 = RequireHash(capture, "sourceSha256");
            string captureSchemaVersion = RequireText(capture, "captureSchemaVersion", 1, 64);
            string timingDefinitionVersion = RequireText(capture, "timingDefinitionVersion", 1, 64);
            string timingBasis = RequireEnum(capture, "timingBasis", "DisplayedTime", "MsBetweenDisplayChange", "MsBetweenPresents");
            int targetProcessId = RequireInteger(capture, "targetProcessId", 1, int.MaxValue);
            string targetSwapChain = RequireText(capture, "targetSwapChainAddress", 1, 128);
            string sessionName = RequireText(capture, "sessionName", 8, 128);
            RequireAsciiSessionName(sessionName);
            bool noTrackInput = RequireBoolean(capture, "noTrackInput");
            if (!noTrackInput) throw new InvalidDataException("capture.noTrackInput must be true.");
            string capturePolicyHash = RequireHash(capture, "capturePolicyHash");
            string captureInvocationHash = RequireHash(capture, "captureInvocationHash");

            string executable = RequireExecutableBaseName(game, "executableBaseName");
            string gameBuild = RequireText(game, "buildId", 1, 256);
            string scenario = RequireText(game, "scenarioId", 1, 256);
            string graphicsApi = RequireEnum(game, "graphicsApi", "DX9", "DX11", "DX12", "OpenGL", "Vulkan", "Other", "Unknown");
            string antiCheatStatus = RequireEnum(game, "antiCheatStatus", "not-applicable", "validated-compatible", "experimental", "unknown-blocked");

            string protocolVersion = RequireText(protocol, "protocolVersion", 1, 64);
            string comparisonContextId = RequireHash(protocol, "comparisonContextId");
            int warmupSeconds = RequireInteger(protocol, "warmupSeconds", 0, 3600);
            int captureSeconds = RequireInteger(protocol, "captureSeconds", 30, 7200);
            int repetitionIndex = RequireInteger(protocol, "repetitionIndex", 1, 20);
            int plannedRepetitions = RequireInteger(protocol, "plannedRepetitions", 3, 20);
            if (repetitionIndex > plannedRepetitions) throw new InvalidDataException("protocol.repetitionIndex exceeds plannedRepetitions.");
            int captureSequenceIndex = RequireInteger(protocol, "captureSequenceIndex", 1, 100);
            decimal slowFrameThreshold = RequireDecimal(protocol, "slowFrameThresholdMs", 0m, 1000m, true);

            string windowsBuild = RequireText(environment, "windowsBuild", 1, 128);
            string gpu = RequireText(environment, "gpu", 1, 256);
            string gpuDriver = RequireText(environment, "gpuDriver", 1, 128);
            string displayMode = RequireText(environment, "displayMode", 1, 128);
            decimal refreshHz = RequireDecimal(environment, "refreshHz", 0m, 1000m, true);
            string hags = RequireEnum(environment, "hags", "on", "off", "unknown");
            string powerPlan = RequireText(environment, "powerPlan", 1, 256);
            string settingsRelativePath = EvidencePathPolicy.NormalizeRelativePath(RequireText(environment, "gameSettingsRelativePath", 1, 512), ".json");
            string gameSettingsHash = RequireHash(environment, "gameSettingsHash");
            string presentRuntime = RequireText(environment, "presentRuntime", 1, 128);
            string presentMode = RequireText(environment, "presentMode", 1, 256);
            string frameGenerationState = RequireEnum(environment, "frameGenerationState", "off", "on", "mixed", "unknown");
            string frameGenerationTechnology = RequireEnum(environment, "frameGenerationTechnology", "none", "intel-xess-fg", "amd-afmf", "nvidia-dlss-fg", "unknown");
            if (frameGenerationState != "off" || frameGenerationTechnology != "none")
                throw new InvalidDataException("Open Measurement Evidence manifest v1 accepts only independently configured frame generation off/none.");
            StrictJsonValue notesValue;
            if (environment.TryGetValue("notes", out notesValue)) RequireTextValue(notesValue, "environment.notes", 0, 2000, true);

            string configurationLabel = RequireText(configuration, "label", 1, 256);
            string configurationRole = RequireEnum(configuration, "role", "baseline", "candidate");
            StrictJsonArray changedVariables = RequireArray(configuration, "changedVariables");
            RequireUniqueTextArray(changedVariables, "configuration.changedVariables", 0, 10, 1, 256);
            if (changedVariables.Values.Count != 0)
                throw new InvalidDataException("Open Measurement Evidence manifest v1 accepts only an empty changedVariables array.");
            if (!string.Equals(configurationRole, "baseline", StringComparison.Ordinal))
                throw new InvalidDataException("Open Measurement Evidence manifest v1 accepts only baseline repeatability captures.");

            string csvRelativePath = EvidencePathPolicy.NormalizeRelativePath(RequireText(evidence, "csvRelativePath", 1, 512), ".csv");
            string csvSha256 = RequireHash(evidence, "csvSha256");
            IList<string> limitationValues = RequireUniqueTextArray(limitations, "limitations", 0, 50, 1, 500);

            CaptureCommandIdentity captureIdentity = BuildCaptureIdentity(timingBasis, targetProcessId, csvRelativePath, sessionName, captureSeconds);
            string expectedPolicyHash = captureIdentity.ComputePolicyHash();
            string expectedInvocationHash = captureIdentity.ComputeInvocationHash();
            RequireHashMatch("capture.capturePolicyHash", expectedPolicyHash, capturePolicyHash);
            RequireHashMatch("capture.captureInvocationHash", expectedInvocationHash, captureInvocationHash);

            string expectedContext = ComputeComparisonContext(
                captureSource, captureSourceVersion, captureSourceSha256, captureSchemaVersion,
                timingDefinitionVersion, timingBasis, expectedPolicyHash, executable, gameBuild,
                scenario, graphicsApi, antiCheatStatus, protocolVersion, warmupSeconds,
                captureSeconds, plannedRepetitions, slowFrameThreshold, windowsBuild, gpu,
                gpuDriver, displayMode, refreshHz, hags, powerPlan, gameSettingsHash,
                presentRuntime, presentMode, frameGenerationState, frameGenerationTechnology);
            RequireHashMatch("protocol.comparisonContextId", expectedContext, comparisonContextId);

            RunManifest analysis = new RunManifest
            {
                RunId = runId,
                ProtocolVersion = protocolVersion,
                CaptureSource = captureSource,
                GameExecutable = executable,
                GameBuild = gameBuild,
                Scenario = scenario,
                GraphicsApi = graphicsApi,
                DisplayMode = displayMode,
                ComparisonContextId = expectedContext,
                ConfigurationLabel = configurationLabel,
                CaptureSchemaVersion = captureSchemaVersion,
                TimingBasis = timingBasis,
                TargetApplication = executable,
                TargetProcessId = targetProcessId,
                TargetSwapChainAddress = targetSwapChain,
                PlannedCaptureSeconds = captureSeconds,
                SlowFrameThresholdMs = (double)slowFrameThreshold,
                CaptureSequenceIndex = captureSequenceIndex,
                CaptureSourceVersion = captureSourceVersion,
                CaptureSourceSha256 = captureSourceSha256,
                CapturePolicyHash = expectedPolicyHash,
                CaptureInvocationHash = expectedInvocationHash,
                SessionName = sessionName,
                NoTrackInput = true,
                GameSettingsHash = gameSettingsHash,
                ChangedVariablesKey = "open-measurement-evidence-baseline-empty-v1",
                TimingDefinitionVersion = timingDefinitionVersion,
                FrameGenerationState = frameGenerationState,
                FrameGenerationTechnology = frameGenerationTechnology,
                RepetitionIndex = repetitionIndex,
                PlannedRepetitions = plannedRepetitions,
                ExpectedPresentRuntime = presentRuntime,
                ExpectedPresentMode = presentMode
            };
            analysis.Validate();

            return new MeasurementRunManifestV1(
                ValidConstructionProof, analysis, createdUtc, antiCheatStatus, windowsBuild, gpu, gpuDriver,
                refreshHz, hags, powerPlan, settingsRelativePath, configurationRole,
                csvRelativePath, csvSha256, limitationValues);
        }

        internal static string ComputeComparisonContext(
            string captureSource, string captureSourceVersion, string captureSourceSha256,
            string captureSchemaVersion, string timingDefinitionVersion, string timingBasis,
            string capturePolicyHash, string executable, string gameBuild, string scenario,
            string graphicsApi, string antiCheatStatus, string protocolVersion, int warmupSeconds,
            int captureSeconds, int plannedRepetitions, decimal slowFrameThresholdMs,
            string windowsBuild, string gpu, string gpuDriver, string displayMode, decimal refreshHz,
            string hags, string powerPlan, string gameSettingsHash, string presentRuntime,
            string presentMode, string frameGenerationState, string frameGenerationTechnology)
        {
            return CanonicalEvidenceHash.Compute(new[]
            {
                Pair("manifest.schemaVersion", "1"),
                Pair("capture.policyHash", CanonicalEvidenceHash.NormalizeHash("capturePolicyHash", capturePolicyHash)),
                Pair("capture.schemaVersion", Normalize(captureSchemaVersion, 64)),
                Pair("capture.source", Normalize(captureSource, 128)),
                Pair("capture.sourceSha256", CanonicalEvidenceHash.NormalizeHash("captureSourceSha256", captureSourceSha256)),
                Pair("capture.sourceVersion", Normalize(captureSourceVersion, 64)),
                Pair("capture.timingBasis", timingBasis),
                Pair("capture.timingDefinitionVersion", Normalize(timingDefinitionVersion, 64)),
                Pair("environment.displayMode", Normalize(displayMode, 128)),
                Pair("environment.frameGenerationState", frameGenerationState),
                Pair("environment.frameGenerationTechnology", frameGenerationTechnology),
                Pair("environment.gameSettingsHash", CanonicalEvidenceHash.NormalizeHash("gameSettingsHash", gameSettingsHash)),
                Pair("environment.gpu", Normalize(gpu, 256)),
                Pair("environment.gpuDriver", Normalize(gpuDriver, 128)),
                Pair("environment.hags", hags),
                Pair("environment.powerPlan", Normalize(powerPlan, 256)),
                Pair("environment.presentMode", Normalize(presentMode, 256)),
                Pair("environment.presentRuntime", Normalize(presentRuntime, 128)),
                Pair("environment.refreshHz", CanonicalDecimal(refreshHz)),
                Pair("environment.windowsBuild", Normalize(windowsBuild, 128)),
                Pair("game.antiCheatStatus", antiCheatStatus),
                Pair("game.buildId", Normalize(gameBuild, 256)),
                Pair("game.executableBaseName", Normalize(executable, 260)),
                Pair("game.graphicsApi", graphicsApi),
                Pair("game.scenarioId", Normalize(scenario, 256)),
                Pair("protocol.captureSeconds", CanonicalEvidenceHash.InvariantInteger(captureSeconds)),
                Pair("protocol.plannedRepetitions", CanonicalEvidenceHash.InvariantInteger(plannedRepetitions)),
                Pair("protocol.protocolVersion", Normalize(protocolVersion, 64)),
                Pair("protocol.slowFrameThresholdMs", CanonicalDecimal(slowFrameThresholdMs)),
                Pair("protocol.warmupSeconds", CanonicalEvidenceHash.InvariantInteger(warmupSeconds))
            });
        }

        private static CaptureCommandIdentity BuildCaptureIdentity(string timingBasis, int processId, string outputRelativePath, string sessionName, int captureSeconds)
        {
            return new CaptureCommandIdentity
            {
                ProtocolIdentifier = CaptureCommandIdentity.EvidenceProtocolIdentifier,
                TimingBasis = timingBasis,
                ProcessId = processId,
                OutputRelativePath = outputRelativePath,
                SessionName = sessionName,
                PlannedTimedSeconds = captureSeconds,
                NoTrackInput = true,
                TrackFrameType = true,
                TrackDisplay = true,
                TrackGpu = false,
                TerminateAfterTimed = true,
                ExcludeDropped = false,
                StopExistingSession = false,
                RestartAsAdmin = false,
                OutputStdout = false,
                MultiCsv = false,
                UseV1Metrics = false,
                UseV2Metrics = true
            };
        }

        private static void RequireExactProperties(StrictJsonObject value, string path, IEnumerable<string> expected)
        {
            RequireAllowedProperties(value, path, expected, new string[0]);
        }

        private static void RequireAllowedProperties(StrictJsonObject value, string path, IEnumerable<string> required, IEnumerable<string> optional)
        {
            HashSet<string> requiredSet = new HashSet<string>(required, StringComparer.Ordinal);
            HashSet<string> allowed = new HashSet<string>(requiredSet, StringComparer.Ordinal);
            allowed.UnionWith(optional);
            foreach (string name in value.PropertyNames)
                if (!allowed.Contains(name)) throw new InvalidDataException(path + " contains an unknown property.");
            foreach (string name in requiredSet)
            {
                StrictJsonValue ignored;
                if (!value.TryGetValue(name, out ignored)) throw new InvalidDataException(path + " is missing a required property.");
            }
        }

        private static StrictJsonObject RequireObject(StrictJsonObject parent, string name)
        {
            StrictJsonObject result = parent.GetRequired(name) as StrictJsonObject;
            if (result == null) throw new InvalidDataException(name + " must be a JSON object.");
            return result;
        }

        private static StrictJsonArray RequireArray(StrictJsonObject parent, string name)
        {
            StrictJsonArray result = parent.GetRequired(name) as StrictJsonArray;
            if (result == null) throw new InvalidDataException(name + " must be a JSON array.");
            return result;
        }

        private static string RequireText(StrictJsonObject parent, string name, int minimum, int maximum)
        {
            return RequireTextValue(parent.GetRequired(name), name, minimum, maximum, false);
        }

        private static string RequireTextValue(StrictJsonValue value, string name, int minimum, int maximum, bool allowEmpty)
        {
            StrictJsonString text = value as StrictJsonString;
            if (text == null) throw new InvalidDataException(name + " must be a JSON string.");
            if ((!allowEmpty && text.Value.Length < minimum) || text.Value.Length > maximum)
                throw new InvalidDataException(name + " length is outside the supported range.");
            if (!string.Equals(text.Value, text.Value.Trim(), StringComparison.Ordinal))
                throw new InvalidDataException(name + " must not contain leading or trailing whitespace.");
            if (text.Value.Any(ch => char.IsControl(ch))) throw new InvalidDataException(name + " contains a control character.");
            string normalized = text.Value.Normalize(System.Text.NormalizationForm.FormC);
            if (normalized.Length < minimum || normalized.Length > maximum) throw new InvalidDataException(name + " normalized length is outside the supported range.");
            return normalized;
        }

        private static string RequireHash(StrictJsonObject parent, string name)
        {
            return CanonicalEvidenceHash.NormalizeHash(name, RequireText(parent, name, 64, 64));
        }

        private static string RequireEnum(StrictJsonObject parent, string name, params string[] allowed)
        {
            string value = RequireText(parent, name, 1, 256);
            if (!allowed.Contains(value, StringComparer.Ordinal)) throw new InvalidDataException(name + " is not an allowed value.");
            return value;
        }

        private static bool RequireBoolean(StrictJsonObject parent, string name)
        {
            StrictJsonBoolean value = parent.GetRequired(name) as StrictJsonBoolean;
            if (value == null) throw new InvalidDataException(name + " must be a JSON boolean.");
            return value.Value;
        }

        private static int RequireInteger(StrictJsonObject parent, string name, int minimum, int maximum)
        {
            StrictJsonNumber value = parent.GetRequired(name) as StrictJsonNumber;
            if (value == null || decimal.Truncate(value.Value) != value.Value || value.Value < minimum || value.Value > maximum)
                throw new InvalidDataException(name + " must be an integer in the supported range.");
            return decimal.ToInt32(value.Value);
        }

        private static decimal RequireDecimal(StrictJsonObject parent, string name, decimal minimum, decimal maximum, bool exclusiveMinimum)
        {
            StrictJsonNumber value = parent.GetRequired(name) as StrictJsonNumber;
            if (value == null || (exclusiveMinimum ? value.Value <= minimum : value.Value < minimum) || value.Value > maximum)
                throw new InvalidDataException(name + " must be a number in the supported range.");
            return value.Value;
        }

        private static string RequireUuidV4(StrictJsonObject parent, string name)
        {
            string raw = RequireText(parent, name, 36, 36);
            Guid value;
            if (!Guid.TryParseExact(raw, "D", out value) || !string.Equals(value.ToString("D"), raw, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException(name + " must be a canonical UUID.");
            byte[] network = GuidToNetworkBytes(value);
            if ((network[6] >> 4) != 4 || (network[8] & 0xC0) != 0x80)
                throw new InvalidDataException(name + " must be an RFC 4122 version-4 UUID.");
            return value.ToString("D");
        }

        private static byte[] GuidToNetworkBytes(Guid value)
        {
            byte[] bytes = value.ToByteArray();
            return new[] { bytes[3], bytes[2], bytes[1], bytes[0], bytes[5], bytes[4], bytes[7], bytes[6], bytes[8], bytes[9], bytes[10], bytes[11], bytes[12], bytes[13], bytes[14], bytes[15] };
        }

        private static DateTimeOffset RequireUtcTimestamp(StrictJsonObject parent, string name)
        {
            string raw = RequireText(parent, name, 20, 40);
            DateTimeOffset value;
            string[] formats = { "yyyy-MM-dd'T'HH:mm:ss'Z'", "yyyy-MM-dd'T'HH:mm:ss.FFFFFFF'Z'" };
            if (!DateTimeOffset.TryParseExact(raw, formats, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out value) || value.Offset != TimeSpan.Zero)
                throw new InvalidDataException(name + " must be an ISO-8601 UTC timestamp ending in Z.");
            return value;
        }

        private static string RequireExecutableBaseName(StrictJsonObject parent, string name)
        {
            return NormalizeExecutableBaseName(name, RequireText(parent, name, 5, 255));
        }

        internal static string NormalizeExecutableBaseName(string name, string value)
        {
            value = CanonicalEvidenceHash.NormalizeText(name, value, 255);
            if (!value.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) || value.Any(ch => ch == '\\' || ch == '/' || ch == ':' || ch == '*' || ch == '?' || ch == '"' || ch == '<' || ch == '>' || ch == '|'))
                throw new InvalidDataException(name + " must be a base executable name ending in .exe.");
            string baseName = value.Substring(0, value.Length - 4);
            if (baseName.EndsWith(" ", StringComparison.Ordinal) || baseName.EndsWith(".", StringComparison.Ordinal) || ReservedExecutableNames.Contains(baseName))
                throw new InvalidDataException(name + " is not a valid Windows executable base name.");
            return value;
        }

        private static void RequireAsciiSessionName(string value)
        {
            if (value.Any(ch => !((ch >= 'A' && ch <= 'Z') || (ch >= 'a' && ch <= 'z') || (ch >= '0' && ch <= '9') || ch == '.' || ch == '_' || ch == '-')))
                throw new InvalidDataException("capture.sessionName contains an unsupported character.");
        }

        private static IList<string> RequireUniqueTextArray(StrictJsonArray array, string name, int minimumItems, int maximumItems, int minimumLength, int maximumLength)
        {
            if (array.Values.Count < minimumItems || array.Values.Count > maximumItems) throw new InvalidDataException(name + " item count is outside the supported range.");
            HashSet<string> unique = new HashSet<string>(StringComparer.Ordinal);
            List<string> result = new List<string>();
            foreach (StrictJsonValue item in array.Values)
            {
                string value = RequireTextValue(item, name + " item", minimumLength, maximumLength, false);
                if (!unique.Add(value)) throw new InvalidDataException(name + " contains a duplicate item.");
                result.Add(value);
            }
            return result;
        }

        private static void RequireHashMatch(string name, string expected, string actual)
        {
            if (!string.Equals(expected, actual, StringComparison.Ordinal)) throw new InvalidDataException(name + " does not match the validator-computed identity.");
        }

        private static string Normalize(string value, int maximum)
        {
            return CanonicalEvidenceHash.NormalizeText("comparison context value", value, maximum);
        }

        private static string CanonicalDecimal(decimal value)
        {
            return value.ToString("G29", CultureInfo.InvariantCulture);
        }

        private static KeyValuePair<string, string> Pair(string key, string value)
        {
            return new KeyValuePair<string, string>(key, value);
        }
    }
}
