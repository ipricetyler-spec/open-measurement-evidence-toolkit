using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace OpenMeasurementEvidence
{
    internal sealed class GameSettingEvidenceV1
    {
        internal string Key { get; private set; }
        internal string Value { get; private set; }
        internal string Source { get; private set; }

        internal GameSettingEvidenceV1(string key, string value, string source)
        {
            Key = key;
            Value = value;
            Source = source;
        }
    }

    internal sealed class GameSettingsSnapshotV1
    {
        private readonly IList<GameSettingEvidenceV1> settings;

        internal string GameExecutable { get; private set; }
        internal string GameBuild { get; private set; }
        internal string GraphicsApi { get; private set; }
        internal string DisplayMode { get; private set; }
        internal decimal RefreshHz { get; private set; }
        internal string FrameGenerationState { get; private set; }
        internal string FrameGenerationTechnology { get; private set; }
        internal string FrameGenerationEvidenceSource { get; private set; }
        internal IEnumerable<GameSettingEvidenceV1> Settings { get { return settings; } }

        internal GameSettingsSnapshotV1(
            string gameExecutable, string gameBuild, string graphicsApi, string displayMode,
            decimal refreshHz, string frameGenerationState, string frameGenerationTechnology,
            string frameGenerationEvidenceSource, IEnumerable<GameSettingEvidenceV1> settings)
        {
            if (settings == null) throw new ArgumentNullException("settings");
            GameExecutable = gameExecutable;
            GameBuild = gameBuild;
            GraphicsApi = graphicsApi;
            DisplayMode = displayMode;
            RefreshHz = refreshHz;
            FrameGenerationState = frameGenerationState;
            FrameGenerationTechnology = frameGenerationTechnology;
            FrameGenerationEvidenceSource = frameGenerationEvidenceSource;
            this.settings = new List<GameSettingEvidenceV1>(settings).AsReadOnly();
        }
    }

    internal static class GameSettingsSnapshotV1Validator
    {
        private static readonly string[] RootProperties = { "schemaVersion", "game", "environment", "settings" };
        private static readonly string[] GameProperties = { "executableBaseName", "buildId", "graphicsApi" };
        private static readonly string[] EnvironmentProperties = { "displayMode", "refreshHz", "frameGenerationState", "frameGenerationTechnology", "frameGenerationEvidenceSource" };
        private static readonly string[] SettingProperties = { "key", "value", "source" };

        internal static GameSettingsSnapshotV1 ParseVerified(EvidencePathPolicy.VerifiedEvidenceFile evidence)
        {
            return ValidateTree(StrictJsonManifestParser.ParseVerifiedSettings(evidence));
        }

#if MEASUREMENT_TESTS
        internal static GameSettingsSnapshotV1 ParseForTests(string json)
        {
            StrictJsonObject root = StrictJsonManifestParser.ParseForTests(json) as StrictJsonObject;
            if (root == null) throw new InvalidDataException("The settings snapshot root must be a JSON object.");
            return ValidateTree(root);
        }
#endif

        internal static void RequireMatchesManifest(GameSettingsSnapshotV1 snapshot, MeasurementRunManifestV1 manifest)
        {
            if (snapshot == null) throw new ArgumentNullException("snapshot");
            if (manifest == null) throw new ArgumentNullException("manifest");
            if (!string.Equals(snapshot.GameExecutable, manifest.GameExecutable, StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(snapshot.GameBuild, manifest.GameBuild, StringComparison.Ordinal) ||
                !string.Equals(snapshot.GraphicsApi, manifest.GraphicsApi, StringComparison.Ordinal) ||
                !string.Equals(snapshot.DisplayMode, manifest.DisplayMode, StringComparison.Ordinal) ||
                snapshot.RefreshHz != manifest.RefreshHz ||
                !string.Equals(snapshot.FrameGenerationState, manifest.FrameGenerationState, StringComparison.Ordinal) ||
                !string.Equals(snapshot.FrameGenerationTechnology, manifest.FrameGenerationTechnology, StringComparison.Ordinal))
                throw new InvalidDataException("The settings snapshot does not match the run manifest environment and game identity.");
        }

        internal static void RequireMatchesContract(GameSettingsSnapshotV1 snapshot, EvidenceCaptureContractV1 contract)
        {
            if (snapshot == null) throw new ArgumentNullException("snapshot");
            if (contract == null) throw new ArgumentNullException("contract");
            IList<GameSettingEvidenceV1> observed = snapshot.Settings.ToList();
            IList<GameSettingEvidenceV1> expected = contract.RequiredSettings.ToList();
            if (observed.Count != expected.Count || observed.Where((item, index) =>
                !string.Equals(item.Key, expected[index].Key, StringComparison.Ordinal) ||
                !string.Equals(item.Value, expected[index].Value, StringComparison.Ordinal) ||
                !string.Equals(item.Source, expected[index].Source, StringComparison.Ordinal)).Any())
                throw new InvalidDataException("The settings snapshot does not match the exact allow-listed setting values and evidence sources for this game contract.");
            if (!string.Equals(snapshot.FrameGenerationEvidenceSource, contract.FrameGenerationEvidenceSource, StringComparison.Ordinal))
                throw new InvalidDataException("The frame-generation evidence source does not match the game contract.");
        }

        private static GameSettingsSnapshotV1 ValidateTree(StrictJsonObject root)
        {
            RequireExactProperties(root, "settings snapshot", RootProperties);
            RequireInteger(root, "schemaVersion", 1, 1);
            StrictJsonObject game = RequireObject(root, "game");
            StrictJsonObject environment = RequireObject(root, "environment");
            StrictJsonArray settings = RequireArray(root, "settings");
            RequireExactProperties(game, "settings.game", GameProperties);
            RequireExactProperties(environment, "settings.environment", EnvironmentProperties);

            string executable = MeasurementRunManifestV1Validator.NormalizeExecutableBaseName(
                "executableBaseName", RequireText(game, "executableBaseName", 5, 255));
            string build = RequireText(game, "buildId", 1, 256);
            string graphicsApi = RequireEnum(game, "graphicsApi", "DX9", "DX11", "DX12", "OpenGL", "Vulkan", "Other", "Unknown");
            string displayMode = RequireText(environment, "displayMode", 1, 128);
            decimal refreshHz = RequireDecimal(environment, "refreshHz", 0m, 1000m, true);
            string frameGenerationState = RequireEnum(environment, "frameGenerationState", "off", "on", "mixed", "unknown");
            string frameGenerationTechnology = RequireEnum(environment, "frameGenerationTechnology", "none", "intel-xess-fg", "amd-afmf", "nvidia-dlss-fg", "unknown");
            string frameGenerationEvidenceSource = RequireEnum(environment, "frameGenerationEvidenceSource", "verified-config", "verified-api");
            if (frameGenerationState != "off" || frameGenerationTechnology != "none")
                throw new InvalidDataException("Open Measurement Evidence settings snapshots accept only independently configured frame generation off/none.");

            if (settings.Values.Count < 1 || settings.Values.Count > 256)
                throw new InvalidDataException("The settings snapshot must contain between 1 and 256 setting entries.");
            List<GameSettingEvidenceV1> entries = new List<GameSettingEvidenceV1>();
            string previousKey = null;
            foreach (StrictJsonValue itemValue in settings.Values)
            {
                StrictJsonObject item = itemValue as StrictJsonObject;
                if (item == null) throw new InvalidDataException("Every settings entry must be a JSON object.");
                RequireExactProperties(item, "settings entry", SettingProperties);
                string key = RequireText(item, "key", 1, 128);
                string value = RequireText(item, "value", 1, 512);
                string source = RequireEnum(item, "source", "verified-config", "verified-api");
                if (previousKey != null && string.CompareOrdinal(previousKey, key) >= 0)
                    throw new InvalidDataException("Settings entries must use unique keys in strict ordinal order.");
                previousKey = key;
                entries.Add(new GameSettingEvidenceV1(key, value, source));
            }

            return new GameSettingsSnapshotV1(executable, build, graphicsApi, displayMode, refreshHz, frameGenerationState, frameGenerationTechnology, frameGenerationEvidenceSource, entries);
        }

        private static void RequireExactProperties(StrictJsonObject value, string path, IEnumerable<string> expected)
        {
            HashSet<string> required = new HashSet<string>(expected, StringComparer.Ordinal);
            foreach (string name in value.PropertyNames)
                if (!required.Contains(name)) throw new InvalidDataException(path + " contains an unknown property.");
            foreach (string name in required)
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
            StrictJsonString text = parent.GetRequired(name) as StrictJsonString;
            if (text == null) throw new InvalidDataException(name + " must be a JSON string.");
            if (text.Value.Length < minimum || text.Value.Length > maximum || !string.Equals(text.Value, text.Value.Trim(), StringComparison.Ordinal) || text.Value.Any(char.IsControl))
                throw new InvalidDataException(name + " is outside the supported normalized text contract.");
            string normalized = text.Value.Normalize(System.Text.NormalizationForm.FormC);
            if (normalized.Length < minimum || normalized.Length > maximum) throw new InvalidDataException(name + " normalized length is outside the supported range.");
            return normalized;
        }

        private static string RequireEnum(StrictJsonObject parent, string name, params string[] allowed)
        {
            string value = RequireText(parent, name, 1, 256);
            if (!allowed.Contains(value, StringComparer.Ordinal)) throw new InvalidDataException(name + " is not an allowed value.");
            return value;
        }

        private static int RequireInteger(StrictJsonObject parent, string name, int minimum, int maximum)
        {
            StrictJsonNumber number = parent.GetRequired(name) as StrictJsonNumber;
            int result;
            if (number == null || number.Value != decimal.Truncate(number.Value) || number.Value < minimum || number.Value > maximum || !int.TryParse(number.Value.ToString(CultureInfo.InvariantCulture), NumberStyles.Integer, CultureInfo.InvariantCulture, out result))
                throw new InvalidDataException(name + " must be an integer inside the supported range.");
            return result;
        }

        private static decimal RequireDecimal(StrictJsonObject parent, string name, decimal minimum, decimal maximum, bool exclusiveMinimum)
        {
            StrictJsonNumber number = parent.GetRequired(name) as StrictJsonNumber;
            if (number == null || number.Value < minimum || number.Value > maximum || (exclusiveMinimum && number.Value == minimum))
                throw new InvalidDataException(name + " must be a number inside the supported range.");
            return number.Value;
        }
    }

    internal sealed class EvidenceCaptureContractV1
    {
        internal string CaptureSource { get; private set; }
        internal string CaptureSourceVersion { get; private set; }
        internal string CaptureSourceSha256 { get; private set; }
        internal string CaptureSchemaVersion { get; private set; }
        internal string TimingDefinitionVersion { get; private set; }
        internal string AntiCheatStatus { get; private set; }
        internal string GameExecutable { get; private set; }
        internal string GameBuild { get; private set; }
        internal string Scenario { get; private set; }
        internal string GraphicsApi { get; private set; }
        internal string PresentRuntime { get; private set; }
        internal string PresentMode { get; private set; }
        internal string FrameGenerationEvidenceSource { get; private set; }
        private readonly IList<GameSettingEvidenceV1> requiredSettings;
        internal IEnumerable<GameSettingEvidenceV1> RequiredSettings { get { return requiredSettings; } }

        internal EvidenceCaptureContractV1(
            string source, string version, string sha256, string schemaVersion, string timingDefinitionVersion,
            string antiCheatStatus, string gameExecutable, string gameBuild, string scenario,
            string graphicsApi, string presentRuntime, string presentMode, string frameGenerationEvidenceSource,
            IEnumerable<GameSettingEvidenceV1> requiredSettings)
        {
            CaptureSource = CanonicalEvidenceHash.NormalizeText("capture contract source", source, 128);
            CaptureSourceVersion = CanonicalEvidenceHash.NormalizeText("capture contract version", version, 64);
            CaptureSourceSha256 = CanonicalEvidenceHash.NormalizeHash("capture contract SHA-256", sha256);
            CaptureSchemaVersion = CanonicalEvidenceHash.NormalizeText("capture contract schema", schemaVersion, 64);
            TimingDefinitionVersion = CanonicalEvidenceHash.NormalizeText("timing definition", timingDefinitionVersion, 64);
            if (antiCheatStatus != "not-applicable" && antiCheatStatus != "validated-compatible")
                throw new InvalidDataException("The capture contract must have an explicit safe anti-cheat status.");
            AntiCheatStatus = antiCheatStatus;
            GameExecutable = MeasurementRunManifestV1Validator.NormalizeExecutableBaseName(
                "contract game executable", gameExecutable);
            GameBuild = CanonicalEvidenceHash.NormalizeText("contract game build", gameBuild, 256);
            Scenario = CanonicalEvidenceHash.NormalizeText("contract scenario", scenario, 256);
            if (graphicsApi != "DX9" && graphicsApi != "DX11" && graphicsApi != "DX12" && graphicsApi != "OpenGL" && graphicsApi != "Vulkan" && graphicsApi != "Other" && graphicsApi != "Unknown")
                throw new InvalidDataException("The contract graphics API is not supported.");
            GraphicsApi = graphicsApi;
            PresentRuntime = CanonicalEvidenceHash.NormalizeText("contract present runtime", presentRuntime, 128);
            PresentMode = CanonicalEvidenceHash.NormalizeText("contract present mode", presentMode, 256);
            if (frameGenerationEvidenceSource != "verified-config" && frameGenerationEvidenceSource != "verified-api")
                throw new InvalidDataException("The contract frame-generation evidence source is not supported.");
            FrameGenerationEvidenceSource = frameGenerationEvidenceSource;
            if (requiredSettings == null) throw new ArgumentNullException("requiredSettings");
            List<GameSettingEvidenceV1> expectedSettings = requiredSettings.Select(item =>
            {
                if (item == null) throw new InvalidDataException("A contract setting cannot be null.");
                string key = CanonicalEvidenceHash.NormalizeText("contract setting key", item.Key, 128);
                string value = CanonicalEvidenceHash.NormalizeText("contract setting value", item.Value, 512);
                if (item.Source != "verified-config" && item.Source != "verified-api")
                    throw new InvalidDataException("Contract settings must use a verified configuration or API source.");
                return new GameSettingEvidenceV1(key, value, item.Source);
            }).ToList();
            List<string> keys = expectedSettings.Select(item => item.Key).ToList();
            if (keys.Count < 1 || keys.Count > 256 || keys.Distinct(StringComparer.Ordinal).Count() != keys.Count || !keys.SequenceEqual(keys.OrderBy(key => key, StringComparer.Ordinal), StringComparer.Ordinal))
                throw new InvalidDataException("Contract setting keys must be unique and in strict ordinal order.");
            this.requiredSettings = expectedSettings.AsReadOnly();
        }

        internal void RequireMatches(MeasurementRunManifestV1 manifest)
        {
            if (manifest == null) throw new ArgumentNullException("manifest");
            if (!string.Equals(manifest.CaptureSource, CaptureSource, StringComparison.Ordinal) ||
                !string.Equals(manifest.CaptureSourceVersion, CaptureSourceVersion, StringComparison.Ordinal) ||
                !string.Equals(manifest.CaptureSourceSha256, CaptureSourceSha256, StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(manifest.CaptureSchemaVersion, CaptureSchemaVersion, StringComparison.Ordinal) ||
                !string.Equals(manifest.TimingDefinitionVersion, TimingDefinitionVersion, StringComparison.Ordinal) ||
                !string.Equals(manifest.AntiCheatStatus, AntiCheatStatus, StringComparison.Ordinal) ||
                !string.Equals(manifest.GameExecutable, GameExecutable, StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(manifest.GameBuild, GameBuild, StringComparison.Ordinal) ||
                !string.Equals(manifest.Scenario, Scenario, StringComparison.Ordinal) ||
                !string.Equals(manifest.GraphicsApi, GraphicsApi, StringComparison.Ordinal) ||
                !string.Equals(manifest.PresentRuntime, PresentRuntime, StringComparison.Ordinal) ||
                !string.Equals(manifest.PresentMode, PresentMode, StringComparison.Ordinal))
                throw new InvalidDataException("The run manifest does not match the exact reviewed capture and anti-cheat contract.");
        }
    }

    internal sealed class EvidenceBundleV1 : IDisposable
    {
        // This token proves same-handle local consistency only. It is not proof that the
        // files came from a controlled capture launch or that another same-user process
        // could not have created them.
        internal sealed class ConsistencyBindingProof { internal ConsistencyBindingProof() { } }
        private static readonly ConsistencyBindingProof ValidConsistencyProof = new ConsistencyBindingProof();
        private readonly EvidencePathPolicy.VerifiedEvidenceFile csvEvidence;
        private readonly ValidatedRunManifest analyzerManifest;
        private readonly object stateLock = new object();
        private int state;

        internal MeasurementRunManifestV1 Manifest { get; private set; }
        internal GameSettingsSnapshotV1 Settings { get; private set; }

        private EvidenceBundleV1(MeasurementRunManifestV1 manifest, GameSettingsSnapshotV1 settings, EvidencePathPolicy.VerifiedEvidenceFile csvEvidence)
        {
            Manifest = manifest;
            Settings = settings;
            this.csvEvidence = csvEvidence;
            analyzerManifest = ValidatedRunManifest.CreateAfterEvidenceBinding(manifest, ValidConsistencyProof);
        }

        internal static bool IsConsistencyProofValid(ConsistencyBindingProof proof)
        {
            return object.ReferenceEquals(proof, ValidConsistencyProof);
        }

#if MEASUREMENT_TESTS
        internal static EvidenceBundleV1 OpenContainedForTests(
            string localAppData, string productFolderName, string evidenceFolderName,
            string manifestRelativePath, EvidenceCaptureContractV1 contract)
        {
            return OpenCore(manifestRelativePath, contract, delegate(string relativePath, EvidencePathPolicy.EvidenceFileKind kind)
            {
                return EvidencePathPolicy.OpenContainedEvidenceForTests(localAppData, productFolderName, evidenceFolderName, relativePath, kind);
            });
        }
#endif

        internal RunMetrics AnalyzeOnce()
        {
            lock (stateLock)
            {
                if (state >= 3) throw new ObjectDisposedException("EvidenceBundleV1");
                if (state != 0) throw new InvalidOperationException("A verified capture bundle can be analyzed only once.");
                state = 1;
            }
            try
            {
                using (StreamReader reader = csvEvidence.CreateStrictUtf8Reader())
                {
                    return PresentMonCsvAnalyzer.Analyze(reader, analyzerManifest);
                }
            }
            finally
            {
                lock (stateLock)
                {
                    state = 2;
                    System.Threading.Monitor.PulseAll(stateLock);
                }
            }
        }

        public void Dispose()
        {
            lock (stateLock)
            {
                while (state == 1 || state == 3) System.Threading.Monitor.Wait(stateLock);
                if (state == 4) return;
                state = 3;
            }
            try
            {
                csvEvidence.Dispose();
            }
            finally
            {
                lock (stateLock)
                {
                    state = 4;
                    System.Threading.Monitor.PulseAll(stateLock);
                }
            }
        }

        private static EvidenceBundleV1 OpenCore(
            string manifestRelativePath, EvidenceCaptureContractV1 contract,
            Func<string, EvidencePathPolicy.EvidenceFileKind, EvidencePathPolicy.VerifiedEvidenceFile> openEvidence)
        {
            if (contract == null) throw new ArgumentNullException("contract");
            if (openEvidence == null) throw new ArgumentNullException("openEvidence");
            MeasurementRunManifestV1 manifest;
            using (EvidencePathPolicy.VerifiedEvidenceFile manifestEvidence = openEvidence(manifestRelativePath, EvidencePathPolicy.EvidenceFileKind.ManifestJson))
            {
                manifest = MeasurementRunManifestV1Validator.ParseVerifiedManifest(manifestEvidence);
            }
            contract.RequireMatches(manifest);

            GameSettingsSnapshotV1 settings;
            using (EvidencePathPolicy.VerifiedEvidenceFile settingsEvidence = openEvidence(manifest.GameSettingsRelativePath, EvidencePathPolicy.EvidenceFileKind.SettingsJson))
            {
                RequireHashMatch("settings snapshot", manifest.GameSettingsHash, settingsEvidence.Sha256);
                settings = GameSettingsSnapshotV1Validator.ParseVerified(settingsEvidence);
            }
            GameSettingsSnapshotV1Validator.RequireMatchesManifest(settings, manifest);
            GameSettingsSnapshotV1Validator.RequireMatchesContract(settings, contract);

            EvidencePathPolicy.VerifiedEvidenceFile csvEvidence = null;
            try
            {
                csvEvidence = openEvidence(manifest.CsvRelativePath, EvidencePathPolicy.EvidenceFileKind.CaptureCsv);
                RequireHashMatch("capture CSV", manifest.CsvSha256, csvEvidence.Sha256);
                return new EvidenceBundleV1(manifest, settings, csvEvidence);
            }
            catch
            {
                if (csvEvidence != null) csvEvidence.Dispose();
                throw;
            }
        }

        private static void RequireHashMatch(string label, string expected, string actual)
        {
            string normalizedExpected = CanonicalEvidenceHash.NormalizeHash(label + " expected SHA-256", expected);
            string normalizedActual = CanonicalEvidenceHash.NormalizeHash(label + " actual SHA-256", actual);
            if (!string.Equals(normalizedExpected, normalizedActual, StringComparison.Ordinal))
                throw new InvalidDataException("The " + label + " bytes do not match the run manifest hash.");
        }

    }
}
