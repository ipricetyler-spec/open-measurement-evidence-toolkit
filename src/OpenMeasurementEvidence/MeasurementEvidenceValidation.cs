using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Win32.SafeHandles;

namespace OpenMeasurementEvidence
{
    internal static class CanonicalEvidenceHash
    {
        private static readonly UTF8Encoding StrictUtf8 = new UTF8Encoding(false, true);

        public static string Compute(IEnumerable<KeyValuePair<string, string>> values)
        {
            if (values == null) throw new ArgumentNullException("values");
            HashSet<string> keys = new HashSet<string>(StringComparer.Ordinal);
            List<KeyValuePair<string, string>> normalizedValues = new List<KeyValuePair<string, string>>();
            foreach (KeyValuePair<string, string> item in values)
            {
                string key = NormalizeText("canonical key", item.Key, 256);
                string value = NormalizeText(key, item.Value, 4096);
                if (!keys.Add(key)) throw new InvalidDataException("Duplicate canonical key: " + key);
                normalizedValues.Add(new KeyValuePair<string, string>(key, value));
            }
            if (normalizedValues.Count == 0) throw new InvalidDataException("Canonical evidence identity cannot be empty.");
            KeyValuePair<string, string>[] ordered = normalizedValues.OrderBy(item => item.Key, StringComparer.Ordinal).ToArray();

            using (MemoryStream canonical = new MemoryStream())
            {
                foreach (KeyValuePair<string, string> item in ordered)
                {
                    WriteLengthPrefixed(canonical, item.Key);
                    WriteLengthPrefixed(canonical, item.Value);
                }

                canonical.Position = 0;
                using (SHA256 algorithm = SHA256.Create())
                {
                    return ToUpperHex(algorithm.ComputeHash(canonical));
                }
            }
        }

        public static string NormalizeHash(string name, string value)
        {
            string normalized = NormalizeText(name, value, 64).ToUpperInvariant();
            if (normalized.Length != 64 || normalized.Any(ch => !Uri.IsHexDigit(ch)))
                throw new InvalidDataException(name + " must be a 64-character SHA-256 value.");
            return normalized;
        }

        public static string NormalizeText(string name, string value, int maximumCharacters)
        {
            if (string.IsNullOrEmpty(value)) throw new InvalidDataException(name + " is required.");
            if (value.Length > maximumCharacters) throw new InvalidDataException(name + " exceeds the supported length.");
            if (!string.Equals(value, value.Trim(), StringComparison.Ordinal))
                throw new InvalidDataException(name + " must not contain leading or trailing whitespace.");
            if (value.Any(ch => char.IsControl(ch))) throw new InvalidDataException(name + " contains a control character.");
            string normalized = value.Normalize(NormalizationForm.FormC);
            if (normalized.Length > maximumCharacters) throw new InvalidDataException(name + " exceeds the supported normalized length.");
            return normalized;
        }

        public static string InvariantInteger(int value)
        {
            return value.ToString(CultureInfo.InvariantCulture);
        }

        public static string InvariantBoolean(bool value)
        {
            return value ? "true" : "false";
        }

        private static void WriteLengthPrefixed(Stream destination, string value)
        {
            byte[] valueBytes = StrictUtf8.GetBytes(value);
            byte[] lengthBytes = Encoding.ASCII.GetBytes(valueBytes.Length.ToString(CultureInfo.InvariantCulture));
            destination.Write(lengthBytes, 0, lengthBytes.Length);
            destination.WriteByte((byte)':');
            destination.Write(valueBytes, 0, valueBytes.Length);
        }

        private static string ToUpperHex(byte[] bytes)
        {
            StringBuilder result = new StringBuilder(bytes.Length * 2);
            foreach (byte value in bytes) result.Append(value.ToString("X2", CultureInfo.InvariantCulture));
            return result.ToString();
        }
    }

    internal sealed class CaptureCommandIdentity
    {
        internal const string EvidenceProtocolIdentifier = "open-measurement-evidence-v1";
        public string ProtocolIdentifier;
        public string TimingBasis;
        public int ProcessId;
        public string OutputRelativePath;
        public string SessionName;
        public int PlannedTimedSeconds;
        public bool NoTrackInput;
        public bool TrackFrameType;
        public bool TrackDisplay;
        public bool TrackGpu;
        public bool TerminateAfterTimed;
        public bool ExcludeDropped;
        public bool StopExistingSession;
        public bool RestartAsAdmin;
        public bool OutputStdout;
        public bool MultiCsv;
        public bool UseV1Metrics;
        public bool UseV2Metrics;

        public string ComputePolicyHash()
        {
            Validate();
            return CanonicalEvidenceHash.Compute(PolicyValues());
        }

        public string ComputeInvocationHash()
        {
            Validate();
            List<KeyValuePair<string, string>> values = new List<KeyValuePair<string, string>>(PolicyValues());
            values.Add(Pair("invocation.outputRelativePath", EvidencePathPolicy.NormalizeRelativePath(OutputRelativePath, ".csv")));
            values.Add(Pair("invocation.plannedTimedSeconds", CanonicalEvidenceHash.InvariantInteger(PlannedTimedSeconds)));
            values.Add(Pair("invocation.processId", CanonicalEvidenceHash.InvariantInteger(ProcessId)));
            values.Add(Pair("invocation.sessionName", CanonicalEvidenceHash.NormalizeText("SessionName", SessionName, 128)));
            return CanonicalEvidenceHash.Compute(values);
        }

        public void Validate()
        {
            string protocol = CanonicalEvidenceHash.NormalizeText("ProtocolIdentifier", ProtocolIdentifier, 64);
            if (!string.Equals(protocol, EvidenceProtocolIdentifier, StringComparison.Ordinal))
                throw new InvalidDataException("ProtocolIdentifier is not the exact open-measurement-evidence capture protocol.");
            if (TimingBasis != "DisplayedTime" && TimingBasis != "MsBetweenDisplayChange" && TimingBasis != "MsBetweenPresents")
                throw new InvalidDataException("TimingBasis is not supported by open-measurement-evidence capture protocol v1.");
            if (ProcessId <= 0) throw new InvalidDataException("ProcessId must be positive.");
            if (PlannedTimedSeconds < 30 || PlannedTimedSeconds > 7200)
                throw new InvalidDataException("PlannedTimedSeconds is outside the open-measurement-evidence range.");
            CanonicalEvidenceHash.NormalizeText("SessionName", SessionName, 128);
            if (SessionName.Length < 8 || SessionName.Any(ch => !((ch >= 'A' && ch <= 'Z') || (ch >= 'a' && ch <= 'z') || (ch >= '0' && ch <= '9') || ch == '.' || ch == '_' || ch == '-')))
                throw new InvalidDataException("SessionName contains unsupported characters.");
            EvidencePathPolicy.NormalizeRelativePath(OutputRelativePath, ".csv");
            if (!NoTrackInput) throw new InvalidDataException("Input tracking must remain disabled.");
            if (!TrackFrameType) throw new InvalidDataException("Frame-type tracking must remain enabled for protocol v1.");
            if (!TrackDisplay) throw new InvalidDataException("Display tracking must remain enabled for protocol v1.");
            if (TrackGpu) throw new InvalidDataException("GPU tracking is disabled in the initial frame-pacing protocol.");
            if (!TerminateAfterTimed) throw new InvalidDataException("Timed capture must terminate after its planned duration.");
            if (ExcludeDropped) throw new InvalidDataException("Dropped frames must not be excluded from evidence.");
            if (StopExistingSession) throw new InvalidDataException("The app must not stop an existing trace session.");
            if (RestartAsAdmin) throw new InvalidDataException("The capture tool must not restart itself as administrator.");
            if (OutputStdout) throw new InvalidDataException("Open Measurement Evidence capture must use a protected evidence file.");
            if (MultiCsv) throw new InvalidDataException("Open Measurement Evidence capture targets one exact process and one evidence file.");
            if (UseV1Metrics) throw new InvalidDataException("Open Measurement Evidence capture must not use the legacy v1 metric set.");
            if (!UseV2Metrics) throw new InvalidDataException("Open Measurement Evidence capture requires the explicit v2 metric set.");
        }

        private IEnumerable<KeyValuePair<string, string>> PolicyValues()
        {
            return new[]
            {
                Pair("policy.excludeDropped", CanonicalEvidenceHash.InvariantBoolean(ExcludeDropped)),
                Pair("policy.multiCsv", CanonicalEvidenceHash.InvariantBoolean(MultiCsv)),
                Pair("policy.noTrackInput", CanonicalEvidenceHash.InvariantBoolean(NoTrackInput)),
                Pair("policy.outputStdout", CanonicalEvidenceHash.InvariantBoolean(OutputStdout)),
                Pair("policy.protocolIdentifier", CanonicalEvidenceHash.NormalizeText("ProtocolIdentifier", ProtocolIdentifier, 64)),
                Pair("policy.restartAsAdmin", CanonicalEvidenceHash.InvariantBoolean(RestartAsAdmin)),
                Pair("policy.stopExistingSession", CanonicalEvidenceHash.InvariantBoolean(StopExistingSession)),
                Pair("policy.terminateAfterTimed", CanonicalEvidenceHash.InvariantBoolean(TerminateAfterTimed)),
                Pair("policy.timedCapture", "true"),
                Pair("policy.timingBasis", TimingBasis),
                Pair("policy.trackDisplay", CanonicalEvidenceHash.InvariantBoolean(TrackDisplay)),
                Pair("policy.trackFrameType", CanonicalEvidenceHash.InvariantBoolean(TrackFrameType)),
                Pair("policy.trackGpu", CanonicalEvidenceHash.InvariantBoolean(TrackGpu)),
                Pair("policy.useV1Metrics", CanonicalEvidenceHash.InvariantBoolean(UseV1Metrics)),
                Pair("policy.useV2Metrics", CanonicalEvidenceHash.InvariantBoolean(UseV2Metrics))
            };
        }

        private static KeyValuePair<string, string> Pair(string key, string value)
        {
            return new KeyValuePair<string, string>(key, value);
        }
    }

    internal static class EvidencePathPolicy
    {
        internal enum EvidenceFileKind
        {
            CaptureCsv,
            ManifestJson,
            SettingsJson,
            CaptureRunJournal
        }

        private const long MaximumCaptureBytes = 32L * 1024L * 1024L;
        private const long MaximumJsonBytes = 1L * 1024L * 1024L;
        private const long MaximumJournalBytes = 64L * 1024L;
        private const string ProductDataFolderName = "OpenMeasurementEvidence";
        private const string EvidenceFolderName = "Evidence";
        private const uint FinalPathNameNormalized = 0;

        [StructLayout(LayoutKind.Sequential)]
        private struct ByHandleFileInformation
        {
            internal uint FileAttributes;
            internal System.Runtime.InteropServices.ComTypes.FILETIME CreationTime;
            internal System.Runtime.InteropServices.ComTypes.FILETIME LastAccessTime;
            internal System.Runtime.InteropServices.ComTypes.FILETIME LastWriteTime;
            internal uint VolumeSerialNumber;
            internal uint FileSizeHigh;
            internal uint FileSizeLow;
            internal uint NumberOfLinks;
            internal uint FileIndexHigh;
            internal uint FileIndexLow;
        }

        private static readonly HashSet<string> ReservedDeviceNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "CON", "PRN", "AUX", "NUL", "CLOCK$",
            "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
            "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9",
            "COM¹", "COM²", "COM³", "LPT¹", "LPT²", "LPT³"
        };

        internal sealed class VerifiedEvidenceFile : IDisposable
        {
            private FileStream stream;
            private bool readerCreated;

            internal string FullPath { get; private set; }
            internal string Sha256 { get; private set; }
            internal EvidenceFileKind Kind { get; private set; }
            internal long Length { get { return stream == null ? 0 : stream.Length; } }

            internal VerifiedEvidenceFile(string fullPath, FileStream stream, string sha256, EvidenceFileKind kind)
            {
                FullPath = fullPath;
                this.stream = stream;
                Sha256 = sha256;
                Kind = kind;
            }

            internal StreamReader CreateStrictUtf8Reader()
            {
                if (stream == null) throw new ObjectDisposedException("VerifiedEvidenceFile");
                if (readerCreated) throw new InvalidOperationException("The verified evidence stream can be parsed only once.");
                readerCreated = true;
                stream.Position = 0;
                return new StreamReader(stream, new UTF8Encoding(false, true), false, 65536, true);
            }

            public void Dispose()
            {
                if (stream == null) return;
                stream.Dispose();
                stream = null;
            }
        }

        internal sealed class OpenEvidenceIdentity
        {
            internal string FullPath { get; private set; }
            internal string Sha256 { get; private set; }
            internal long Length { get; private set; }
            internal uint VolumeSerialNumber { get; private set; }
            internal ulong FileIndex { get; private set; }
            internal uint NumberOfLinks { get; private set; }

            internal OpenEvidenceIdentity(string fullPath, string sha256, long length,
                uint volumeSerialNumber, ulong fileIndex, uint numberOfLinks)
            {
                FullPath = fullPath;
                Sha256 = sha256;
                Length = length;
                VolumeSerialNumber = volumeSerialNumber;
                FileIndex = fileIndex;
                NumberOfLinks = numberOfLinks;
            }

            internal bool SameFile(OpenEvidenceIdentity other)
            {
                return other != null && VolumeSerialNumber == other.VolumeSerialNumber &&
                    FileIndex == other.FileIndex && NumberOfLinks == other.NumberOfLinks &&
                    Length == other.Length && Sha256 == other.Sha256 &&
                    string.Equals(FullPath, other.FullPath, StringComparison.OrdinalIgnoreCase);
            }
        }

        public static string NormalizeRelativePath(string relativePath, string requiredExtension)
        {
            if (requiredExtension != ".csv" && requiredExtension != ".json")
                throw new InvalidDataException("Evidence extension policy is not supported.");
            string normalized = CanonicalEvidenceHash.NormalizeText("relative evidence path", relativePath, 512).Replace('/', '\\');
            if (Path.IsPathRooted(normalized) || normalized.StartsWith("\\", StringComparison.Ordinal) || normalized.IndexOf(':') >= 0)
                throw new InvalidDataException("Evidence path must be relative and must not use a drive, device, UNC path, or alternate data stream.");

            string[] segments = normalized.Split('\\');
            if (segments.Length == 0) throw new InvalidDataException("Evidence path is empty.");
            foreach (string segment in segments)
            {
                ValidateSegment(segment);
            }

            if (!string.Equals(Path.GetExtension(normalized), requiredExtension, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Evidence path has an unsupported file extension.");
            return string.Join("\\", segments);
        }

        public static VerifiedEvidenceFile OpenEvidence(string relativePath, EvidenceFileKind kind)
        {
            string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            return OpenContainedEvidence(localAppData, ProductDataFolderName, EvidenceFolderName, relativePath, kind);
        }

        internal static OpenEvidenceIdentity InspectHeldEvidence(FileStream stream,
            string expectedFullPath, string allowedRoot, EvidenceFileKind kind)
        {
            string ignoredExtension;
            long maximumBytes;
            GetFilePolicy(kind, out ignoredExtension, out maximumBytes);
            return VerifyOpenHandle(stream, expectedFullPath, allowedRoot, maximumBytes);
        }

#if MEASUREMENT_TESTS
        internal static string ResolveContainedPathForTests(string localAppData, string productFolderName, string evidenceFolderName, string relativePath, string requiredExtension)
        {
            string evidenceRoot;
            return ResolveContainedPath(localAppData, productFolderName, evidenceFolderName, relativePath, requiredExtension, out evidenceRoot);
        }

        internal static VerifiedEvidenceFile OpenContainedEvidenceForTests(string localAppData, string productFolderName, string evidenceFolderName, string relativePath, EvidenceFileKind kind)
        {
            return OpenContainedEvidence(localAppData, productFolderName, evidenceFolderName, relativePath, kind);
        }
#endif

        private static string ResolveContainedPath(string localAppData, string productFolderName, string evidenceFolderName, string relativePath, string requiredExtension, out string evidenceRoot)
        {
            string localRoot = NormalizeDirectory(localAppData);
            string productName = ValidateSingleFolderName("product folder", productFolderName);
            string evidenceName = ValidateSingleFolderName("evidence folder", evidenceFolderName);
            string productRoot = NormalizeDirectory(Path.Combine(localRoot, productName));
            EnsureContained(localRoot, productRoot, "product data root");
            evidenceRoot = NormalizeDirectory(Path.Combine(productRoot, evidenceName));
            EnsureContained(productRoot, evidenceRoot, "evidence root");

            RejectReparsePointIfPresent(localRoot);
            RejectReparsePointIfPresent(productRoot);
            RejectReparsePointIfPresent(evidenceRoot);

            string normalizedRelative = NormalizeRelativePath(relativePath, requiredExtension);
            string target = Path.GetFullPath(Path.Combine(evidenceRoot, normalizedRelative));
            EnsureContained(evidenceRoot, target, "evidence file");
            RejectExistingReparseComponents(evidenceRoot, normalizedRelative);
            return target;
        }

        private static VerifiedEvidenceFile OpenContainedEvidence(string localAppData, string productFolderName, string evidenceFolderName, string relativePath, EvidenceFileKind kind)
        {
            string requiredExtension;
            long maximumBytes;
            GetFilePolicy(kind, out requiredExtension, out maximumBytes);
            string evidenceRoot;
            string fullPath = ResolveContainedPath(localAppData, productFolderName, evidenceFolderName, relativePath, requiredExtension, out evidenceRoot);
            FileStream stream = OpenVerifiedReadHandle(fullPath, evidenceRoot, maximumBytes);
            try
            {
                string hash = ComputeSha256(stream);
                return new VerifiedEvidenceFile(fullPath, stream, hash, kind);
            }
            catch
            {
                stream.Dispose();
                throw;
            }
        }

        private static void GetFilePolicy(EvidenceFileKind kind, out string requiredExtension, out long maximumBytes)
        {
            switch (kind)
            {
                case EvidenceFileKind.CaptureCsv:
                    requiredExtension = ".csv";
                    maximumBytes = MaximumCaptureBytes;
                    return;
                case EvidenceFileKind.ManifestJson:
                case EvidenceFileKind.SettingsJson:
                    requiredExtension = ".json";
                    maximumBytes = MaximumJsonBytes;
                    return;
                case EvidenceFileKind.CaptureRunJournal:
                    requiredExtension = ".journal.v1";
                    maximumBytes = MaximumJournalBytes;
                    return;
                default:
                    throw new InvalidDataException("Evidence file kind is not supported.");
            }
        }

        private static FileStream OpenVerifiedReadHandle(string fullPath, string allowedRoot, long maximumBytes)
        {
            if (maximumBytes <= 0) throw new ArgumentOutOfRangeException("maximumBytes");
            RejectReparsePointIfPresent(fullPath);
            FileStream stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read, 65536, FileOptions.SequentialScan);
            try
            {
                VerifyOpenHandle(stream, fullPath, allowedRoot, maximumBytes);
                return stream;
            }
            catch
            {
                stream.Dispose();
                throw;
            }
        }

        private static OpenEvidenceIdentity VerifyOpenHandle(FileStream stream,
            string expectedFullPath, string allowedRoot, long maximumBytes)
        {
            if (stream == null) throw new ArgumentNullException("stream");
            if (!stream.CanRead || !stream.CanSeek) throw new InvalidDataException("Evidence handle must be readable and seekable.");
            if (maximumBytes <= 0) throw new ArgumentOutOfRangeException("maximumBytes");
            string requestedPath = Path.GetFullPath(expectedFullPath);
            string openedPath = GetFinalPath(stream.SafeFileHandle);
            if (!string.Equals(requestedPath, openedPath, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("The opened evidence handle resolved to a different filesystem target.");
            EnsureContained(allowedRoot, openedPath, "opened evidence handle");
            ByHandleFileInformation identity;
            if (!GetFileInformationByHandle(stream.SafeFileHandle, out identity))
                throw new IOException("Windows could not read the evidence file identity.", new Win32Exception(Marshal.GetLastWin32Error()));
            if ((identity.FileAttributes & (uint)FileAttributes.ReparsePoint) != 0)
                throw new InvalidDataException("The evidence handle identifies a reparse point.");
            if (identity.NumberOfLinks != 1)
                throw new InvalidDataException("Evidence files must have exactly one filesystem link.");
            if (stream.Length <= 0 || stream.Length > maximumBytes)
                throw new InvalidDataException("Evidence file size is outside the supported range.");
            ulong fileIndex = ((ulong)identity.FileIndexHigh << 32) | identity.FileIndexLow;
            return new OpenEvidenceIdentity(openedPath, ComputeSha256(stream), stream.Length,
                identity.VolumeSerialNumber, fileIndex, identity.NumberOfLinks);
        }

        private static string ComputeSha256(FileStream openHandle)
        {
            if (openHandle == null) throw new ArgumentNullException("openHandle");
            if (!openHandle.CanRead || !openHandle.CanSeek) throw new InvalidDataException("Evidence handle must be readable and seekable.");
            long originalPosition = openHandle.Position;
            openHandle.Position = 0;
            try
            {
                using (SHA256 algorithm = SHA256.Create())
                {
                    return ToUpperHex(algorithm.ComputeHash(openHandle));
                }
            }
            finally
            {
                openHandle.Position = originalPosition;
            }
        }

        private static void ValidateSegment(string segment)
        {
            if (string.IsNullOrEmpty(segment) || segment == "." || segment == "..")
                throw new InvalidDataException("Evidence path contains an empty or traversal segment.");
            if (segment.EndsWith(" ", StringComparison.Ordinal) || segment.EndsWith(".", StringComparison.Ordinal))
                throw new InvalidDataException("Evidence path segment has a trailing space or dot.");
            if (segment.Any(ch => ch < 32 || ch == '"' || ch == '<' || ch == '>' || ch == '|' || ch == '*' || ch == '?' || ch == ':' || ch == '/' || ch == '\\'))
                throw new InvalidDataException("Evidence path contains an invalid Windows filename character.");
            string baseName = segment.Split('.')[0];
            if (ReservedDeviceNames.Contains(baseName)) throw new InvalidDataException("Evidence path uses a reserved Windows device name.");
        }

        private static string ValidateSingleFolderName(string name, string value)
        {
            string normalized = CanonicalEvidenceHash.NormalizeText(name, value, 128);
            ValidateSegment(normalized);
            return normalized;
        }

        private static string NormalizeDirectory(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) throw new InvalidDataException("A required directory path is unavailable.");
            string full = Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            if (full.Length == 0 || Path.GetPathRoot(full) == full) throw new InvalidDataException("A broad filesystem root is not an allowed evidence directory.");
            return full + Path.DirectorySeparatorChar;
        }

        private static void EnsureContained(string rootWithSeparator, string candidate, string name)
        {
            string normalizedRoot = NormalizeDirectory(rootWithSeparator);
            string normalizedCandidate = Path.GetFullPath(candidate);
            if (!normalizedCandidate.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException(name + " escaped its allowed root.");
        }

        private static void RejectExistingReparseComponents(string root, string normalizedRelative)
        {
            string current = root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            foreach (string segment in normalizedRelative.Split('\\'))
            {
                current = Path.Combine(current, segment);
                RejectReparsePointIfPresent(current);
            }
        }

        private static void RejectReparsePointIfPresent(string path)
        {
            if (!File.Exists(path) && !Directory.Exists(path)) return;
            FileAttributes attributes = File.GetAttributes(path);
            if ((attributes & FileAttributes.ReparsePoint) != 0)
                throw new InvalidDataException("Evidence paths must not contain junctions, symbolic links, or other reparse points.");
        }

        private static string ToUpperHex(byte[] bytes)
        {
            StringBuilder result = new StringBuilder(bytes.Length * 2);
            foreach (byte value in bytes) result.Append(value.ToString("X2", CultureInfo.InvariantCulture));
            return result.ToString();
        }

        private static string GetFinalPath(SafeFileHandle handle)
        {
            StringBuilder buffer = new StringBuilder(32768);
            uint length = GetFinalPathNameByHandle(handle, buffer, (uint)buffer.Capacity, FinalPathNameNormalized);
            if (length == 0 || length >= buffer.Capacity)
                throw new IOException("Windows could not resolve the opened evidence handle.");
            string path = buffer.ToString();
            const string uncPrefix = @"\\?\UNC\";
            const string devicePrefix = @"\\?\";
            if (path.StartsWith(uncPrefix, StringComparison.OrdinalIgnoreCase)) path = @"\\" + path.Substring(uncPrefix.Length);
            else if (path.StartsWith(devicePrefix, StringComparison.OrdinalIgnoreCase)) path = path.Substring(devicePrefix.Length);
            return Path.GetFullPath(path);
        }

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern uint GetFinalPathNameByHandle(SafeFileHandle file, StringBuilder path, uint pathLength, uint flags);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetFileInformationByHandle(SafeFileHandle file, out ByHandleFileInformation information);
    }
}
