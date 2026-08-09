using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using OpenMeasurementEvidence;

internal static class MeasurementAnalysisTests
{
    private static int failures;

    public static int Main()
    {
        TestStableRun();
        TestGeneratedAndDroppedFrames();
        TestQuotedCsv();
        TestInsufficientFrames();
        TestImprovedComparison();
        TestNoMaterialDifference();
        TestWorseComparison();
        TestProtocolMismatch();
        TestInvocationIdentityExcludedFromProtocol();
        TestPolicyIdentityIncludedInProtocol();
        TestCanonicalHashCollisionResistance();
        TestCaptureIdentitySplit();
        TestUnsafeEvidencePathsRejected();
        TestContainedEvidencePathAccepted();
        TestVerifiedEvidenceHandleHash();
        TestStrictJsonValidDocument();
        TestStrictJsonDuplicateAndTrailingRejected();
        TestStrictJsonUnicodeRejected();
        TestStrictJsonNumberRejected();
        TestStrictJsonDepthAndStringLimitRejected();
        TestStrictJsonVerifiedManifest();
        TestManifestV1Valid();
        TestManifestV1UnknownAndMissingRejected();
        TestManifestV1IdentityMismatchRejected();
        TestManifestV1ProtocolScopeRejected();
        TestSettingsSnapshotV1Valid();
        TestSettingsSnapshotV1Rejected();
        TestEvidenceBundleV1Valid();
        TestEvidenceBundleV1TamperAndContractRejected();
        TestPublishedExamples();
        TestHighVariation();
        TestDuplicateRunId();
        TestExplicitPresentationTiming();
        TestMalformedQuotedCsv();
        TestDuplicateHeader();
        TestUnreasonableIntervalRejected();
        TestInvalidRunId();
        TestMissingContext();
        TestMixedSwapChainRejected();
        TestTimingMismatchRejected();
        TestMalformedDisplayedTimeRejected();
        TestQuotedFieldLimit();
        TestNonFiniteComparisonMetric();
        TestUnknownFrameTypeRejected();
        TestNonAlternatingSequenceRejected();
        TestUnderpoweredComparisonRejected();
        TestBlankDisplayedTimeRejected();
        TestAlternateBasisMalformedDisplayedRejected();
        TestUnequalCountsRejected();
        TestSequenceGapRejected();
        TestPlannedDurationMismatchRejected();
        TestNullRunRejected();
        TestFrameGenerationMismatchRejected();
        TestTargetTopologyVariantsRejected();
        TestMissingDisplayedCellRejected();
        TestUndisplayedMetricUnavailableComparison();

        Console.WriteLine(failures == 0 ? "PASS: 56 synthetic test scenarios" : "FAIL: " + failures + " test(s)");
        return failures == 0 ? 0 : 1;
    }

    private static void TestStableRun()
    {
        RunMetrics metrics = Analyze("stable", MakeFrames(600, 16.667, 0, false));
        Check("stable frame count", metrics.ValidFrameCount == 600);
        Check("stable fps", Math.Abs(metrics.AverageFps - 60.0) < 0.1);
        Check("stable p99", Math.Abs(metrics.P99FrameTimeMs - 16.667) < 0.01);
        Check("stable slow frames", metrics.SlowFrameCount == 0);
    }

    private static void TestGeneratedAndDroppedFrames()
    {
        string officialTokens = MakeFrames(1203, 10.0, 3, true);
        RunMetrics metrics = AnalyzeWithFrameGeneration("on", "intel-xess-fg", officialTokens, "mixed");
        RunMetrics amdMetrics = AnalyzeWithFrameGeneration("on", "amd-afmf", officialTokens.Replace("Intel XeSS-FG", "AMD AFMF"), "mixed-amd");
        Check("undisplayed count", metrics.UndisplayedFrameCount == 3);
        Check("generated count", metrics.GeneratedFrameCount > 0);
        Check("application label count with generation", metrics.ApplicationFrameLabelCount > 0);
        Check("generated warning", metrics.Warnings.Count > 0);
        Check("AMD generated token", amdMetrics.GeneratedFrameCount > 0 && amdMetrics.FrameGenerationTechnology == "amd-afmf");
        bool enumTokenRejected = false;
        bool debugRepeatedRejected = false;
        bool mixedTechnologyRejected = false;
        bool whitespaceRejected = false;
        bool undisplayedGeneratedRejected = false;
        bool undisplayedUnsupportedRejected = false;
        bool undisplayedMixedTechnologyRejected = false;
        try { AnalyzeWithFrameGeneration("on", "intel-xess-fg", officialTokens.Replace("Intel XeSS-FG", "Intel_XEFG"), "mixed"); }
        catch (InvalidDataException) { enumTokenRejected = true; }
        try { AnalyzeWithFrameGeneration("on", "intel-xess-fg", officialTokens.Replace("Intel XeSS-FG", "Repeated"), "mixed"); }
        catch (InvalidDataException) { debugRepeatedRejected = true; }
        try { AnalyzeWithFrameGeneration("on", "intel-xess-fg", ReplaceFirst(officialTokens, "Intel XeSS-FG", "AMD AFMF"), "mixed"); }
        catch (InvalidDataException) { mixedTechnologyRejected = true; }
        try { AnalyzeWithFrameGeneration("on", "intel-xess-fg", officialTokens.Replace("Intel XeSS-FG", " Intel XeSS-FG "), "mixed"); }
        catch (InvalidDataException) { whitespaceRejected = true; }
        try { Analyze("undisplayed-generated", ReplaceFirst(MakeFrames(640, 16.0, 0, false), ",16.000,Application", ",NA,Intel XeSS-FG")); }
        catch (InvalidDataException) { undisplayedGeneratedRejected = true; }
        try { Analyze("undisplayed-unsupported", ReplaceFirst(MakeFrames(640, 16.0, 0, false), ",16.000,Application", ",NA,Unknown")); }
        catch (InvalidDataException) { undisplayedUnsupportedRejected = true; }
        try { AnalyzeWithFrameGeneration("on", "intel-xess-fg", ReplaceFirst(officialTokens, ",NA,Application", ",NA,AMD AFMF"), "mixed-undisplayed"); }
        catch (InvalidDataException) { undisplayedMixedTechnologyRejected = true; }
        Check("official serialized frame type tokens", enumTokenRejected && debugRepeatedRejected && mixedTechnologyRejected && whitespaceRejected && undisplayedGeneratedRejected && undisplayedUnsupportedRejected && undisplayedMixedTechnologyRejected);
    }

    private static void TestQuotedCsv()
    {
        StringBuilder csv = new StringBuilder(CsvHeader("DisplayedTime", true));
        for (int i = 0; i < 640; i++) csv.Append("\"testgame.exe\",4242,0xABC,DXGI,Hardware: Independent Flip,16.0,Application\n");
        RunMetrics metrics = Analyze("quoted", csv.ToString());
        Check("quoted csv", metrics.ValidFrameCount == 640);
    }

    private static void TestInsufficientFrames()
    {
        bool threw = false;
        try { Analyze("short", MakeFrames(10, 16.0, 0, false)); }
        catch (InvalidDataException) { threw = true; }
        Check("minimum frames", threw);
    }

    private static void TestImprovedComparison()
    {
        IList<RunMetrics> baseline = MakeRunSet("base", 16.7, 16.8, 16.6);
        IList<RunMetrics> candidate = MakeRunSet("candidate", 14.4, 14.5, 14.3);
        ComparisonResult result = RunComparison.Compare(baseline, candidate);
        Check("improved verdict", result.Verdict == ComparisonVerdict.Improved);
    }

    private static void TestNoMaterialDifference()
    {
        IList<RunMetrics> baseline = MakeRunSet("base", 16.70, 16.75, 16.65);
        IList<RunMetrics> candidate = MakeRunSet("candidate", 16.60, 16.70, 16.65);
        ComparisonResult result = RunComparison.Compare(baseline, candidate);
        Check("no difference verdict", result.Verdict == ComparisonVerdict.NoMaterialDifference);
    }

    private static void TestWorseComparison()
    {
        IList<RunMetrics> baseline = MakeRunSet("base", 16.6, 16.7, 16.5);
        IList<RunMetrics> candidate = MakeRunSet("candidate", 20.0, 20.2, 19.8);
        ComparisonResult result = RunComparison.Compare(baseline, candidate);
        Check("worse verdict", result.Verdict == ComparisonVerdict.Worse);
    }

    private static void TestProtocolMismatch()
    {
        IList<RunMetrics> baseline = MakeRunSet("base", 16.6, 16.7, 16.5);
        IList<RunMetrics> candidate = MakeRunSet("candidate", 14.0, 14.1, 13.9);
        candidate[0].ProtocolKey = "different";
        ComparisonResult result = RunComparison.Compare(baseline, candidate);
        Check("protocol mismatch", result.Verdict == ComparisonVerdict.Inconclusive);
    }

    private static void TestInvocationIdentityExcludedFromProtocol()
    {
        RunMetrics first = AnalyzeWithCaptureHashes(new string('5', 64), new string('6', 64), "invocation-a");
        RunMetrics second = AnalyzeWithCaptureHashes(new string('5', 64), new string('7', 64), "invocation-b");
        Check("invocation identity excluded from protocol", first.ProtocolKey == second.ProtocolKey);
    }

    private static void TestPolicyIdentityIncludedInProtocol()
    {
        RunMetrics first = AnalyzeWithCaptureHashes(new string('5', 64), new string('6', 64), "policy-a");
        RunMetrics second = AnalyzeWithCaptureHashes(new string('8', 64), new string('6', 64), "policy-b");
        Check("policy identity included in protocol", first.ProtocolKey != second.ProtocolKey);
    }

    private static void TestCanonicalHashCollisionResistance()
    {
        string first = CanonicalEvidenceHash.Compute(new[]
        {
            new KeyValuePair<string, string>("a", "bc"),
            new KeyValuePair<string, string>("d", "e")
        });
        string second = CanonicalEvidenceHash.Compute(new[]
        {
            new KeyValuePair<string, string>("ab", "c"),
            new KeyValuePair<string, string>("d", "e")
        });
        Check("canonical length-prefix collision resistance", first != second && first.Length == 64 && second.Length == 64);
        Check("canonical known-answer hash", first == "2B8CBF4758726CF45DE769CF6FA8D9D84124A773ACDE346F31ED367B41EC3A23");
        bool normalizedDuplicateRejected = false;
        try
        {
            CanonicalEvidenceHash.Compute(new[]
            {
                new KeyValuePair<string, string>("\u00C5", "one"),
                new KeyValuePair<string, string>("A\u030A", "two")
            });
        }
        catch (InvalidDataException) { normalizedDuplicateRejected = true; }
        Check("canonical NFC duplicate rejected", normalizedDuplicateRejected);
    }

    private static void TestCaptureIdentitySplit()
    {
        CaptureCommandIdentity first = MakeCaptureIdentity(4242, "captures\\first.csv", "session-first");
        string policy = first.ComputePolicyHash();
        string invocation = first.ComputeInvocationHash();
        CaptureCommandIdentity pidVariant = MakeCaptureIdentity(5252, "captures\\first.csv", "session-first");
        CaptureCommandIdentity pathVariant = MakeCaptureIdentity(4242, "captures\\second.csv", "session-first");
        CaptureCommandIdentity sessionVariant = MakeCaptureIdentity(4242, "captures\\first.csv", "session-second");
        CaptureCommandIdentity durationVariant = MakeCaptureIdentity(4242, "captures\\first.csv", "session-first");
        durationVariant.PlannedTimedSeconds = 61;
        Check("capture policy invariant across runs", new[] { pidVariant, pathVariant, sessionVariant, durationVariant }.All(item => item.ComputePolicyHash() == policy));
        Check("capture invocation binds PID", pidVariant.ComputeInvocationHash() != invocation);
        Check("capture invocation binds output path", pathVariant.ComputeInvocationHash() != invocation);
        Check("capture invocation binds session", sessionVariant.ComputeInvocationHash() != invocation);
        Check("capture invocation binds duration", durationVariant.ComputeInvocationHash() != invocation);
        CaptureCommandIdentity timingVariant = MakeCaptureIdentity(4242, "captures\\first.csv", "session-first");
        timingVariant.TimingBasis = "MsBetweenPresents";
        Check("capture policy binds timing basis", timingVariant.ComputePolicyHash() != policy);
        bool unicodeSessionRejected = false;
        try { MakeCaptureIdentity(6262, "captures\\third.csv", "session-é").ComputeInvocationHash(); }
        catch (InvalidDataException) { unicodeSessionRejected = true; }
        Check("capture session remains ASCII", unicodeSessionRejected);
        bool protocolRejected = false;
        CaptureCommandIdentity wrongProtocol = MakeCaptureIdentity(7272, "captures\\fourth.csv", "session-fourth");
        wrongProtocol.ProtocolIdentifier = "open-measurement-evidence-v2";
        try { wrongProtocol.ComputePolicyHash(); }
        catch (InvalidDataException) { protocolRejected = true; }
        Check("capture protocol identifier pinned", protocolRejected);
        bool legacyMetricsRejected = false;
        CaptureCommandIdentity legacyMetrics = MakeCaptureIdentity(8282, "captures\\five.csv", "session-fifth");
        legacyMetrics.UseV1Metrics = true;
        try { legacyMetrics.ComputePolicyHash(); }
        catch (InvalidDataException) { legacyMetricsRejected = true; }
        bool missingV2Rejected = false;
        CaptureCommandIdentity missingV2 = MakeCaptureIdentity(9292, "captures\\six.csv", "session-sixth");
        missingV2.UseV2Metrics = false;
        try { missingV2.ComputePolicyHash(); }
        catch (InvalidDataException) { missingV2Rejected = true; }
        Check("capture metric schema pinned", legacyMetricsRejected && missingV2Rejected);
    }

    private static void TestUnsafeEvidencePathsRejected()
    {
        string[] unsafePaths =
        {
            "..\\escape.csv",
            "C:\\absolute.csv",
            "\\\\server\\share\\capture.csv",
            "captures\\NUL.csv",
            "captures\\COM¹.csv",
            "captures\\LPT³.csv",
            "captures\\capture.csv:stream",
            "captures\\trailing.\\capture.csv",
            "captures\\\\capture.csv"
        };
        bool allRejected = true;
        foreach (string path in unsafePaths)
        {
            try { EvidencePathPolicy.NormalizeRelativePath(path, ".csv"); allRejected = false; }
            catch (InvalidDataException) { }
        }
        try { EvidencePathPolicy.NormalizeRelativePath("captures\\payload.exe", ".exe"); allRejected = false; }
        catch (InvalidDataException) { }
        Check("unsafe evidence paths rejected", allRejected);
    }

    private static void TestContainedEvidencePathAccepted()
    {
        string localRoot = Path.Combine(Path.GetTempPath(), "OpenMeasurementEvidence-containment-" + Guid.NewGuid().ToString("N"));
        string resolved = EvidencePathPolicy.ResolveContainedPathForTests(localRoot, "OpenMeasurementEvidence", "Evidence", "captures\\run.csv", ".csv");
        string expectedRoot = Path.GetFullPath(Path.Combine(localRoot, "OpenMeasurementEvidence", "Evidence")) + Path.DirectorySeparatorChar;
        Check("contained evidence path accepted", resolved.StartsWith(expectedRoot, StringComparison.OrdinalIgnoreCase));
    }

    private static void TestVerifiedEvidenceHandleHash()
    {
        string localRoot = Path.Combine(Path.GetTempPath(), "OpenMeasurementEvidence-hash-" + Guid.NewGuid().ToString("N"));
        string evidenceRoot = Path.Combine(localRoot, "OpenMeasurementEvidence", "Evidence", "captures");
        Directory.CreateDirectory(evidenceRoot);
        string file = Path.Combine(evidenceRoot, "run.csv");
        try
        {
            File.WriteAllText(file, "abc", new UTF8Encoding(false));
            using (EvidencePathPolicy.VerifiedEvidenceFile evidence = EvidencePathPolicy.OpenContainedEvidenceForTests(localRoot, "OpenMeasurementEvidence", "Evidence", "captures\\run.csv", EvidencePathPolicy.EvidenceFileKind.CaptureCsv))
            {
                Check("verified evidence handle hash", evidence.Sha256 == "BA7816BF8F01CFEA414140DE5DAE2223B00361A396177A9CB410FF61F20015AD");
                using (StreamReader reader = evidence.CreateStrictUtf8Reader())
                {
                    Check("verified evidence reader", reader.ReadToEnd() == "abc");
                    bool concurrentWriterDenied = false;
                    try { using (FileStream ignored = new FileStream(file, FileMode.Open, FileAccess.Write, FileShare.ReadWrite)) { } }
                    catch (IOException) { concurrentWriterDenied = true; }
                    Check("verified evidence denies concurrent writer", concurrentWriterDenied);
                }
                bool secondReaderRejected = false;
                try { evidence.CreateStrictUtf8Reader(); }
                catch (InvalidOperationException) { secondReaderRejected = true; }
                Check("verified evidence parse once", secondReaderRejected);
            }

            string empty = Path.Combine(evidenceRoot, "empty.csv");
            File.WriteAllBytes(empty, new byte[0]);
            bool emptyRejected = false;
            try { using (EvidencePathPolicy.VerifiedEvidenceFile ignored = EvidencePathPolicy.OpenContainedEvidenceForTests(localRoot, "OpenMeasurementEvidence", "Evidence", "captures\\empty.csv", EvidencePathPolicy.EvidenceFileKind.CaptureCsv)) { } }
            catch (InvalidDataException) { emptyRejected = true; }
            Check("empty evidence rejected", emptyRejected);

            string invalidUtf8 = Path.Combine(evidenceRoot, "invalid.json");
            File.WriteAllBytes(invalidUtf8, new byte[] { 0xFF });
            bool invalidUtf8Rejected = false;
            try
            {
                using (EvidencePathPolicy.VerifiedEvidenceFile evidence = EvidencePathPolicy.OpenContainedEvidenceForTests(localRoot, "OpenMeasurementEvidence", "Evidence", "captures\\invalid.json", EvidencePathPolicy.EvidenceFileKind.SettingsJson))
                using (StreamReader reader = evidence.CreateStrictUtf8Reader()) reader.ReadToEnd();
            }
            catch (DecoderFallbackException) { invalidUtf8Rejected = true; }
            Check("invalid UTF-8 evidence rejected", invalidUtf8Rejected);

            string oversized = Path.Combine(evidenceRoot, "oversized.json");
            using (FileStream oversizedStream = new FileStream(oversized, FileMode.CreateNew, FileAccess.Write, FileShare.None)) oversizedStream.SetLength((1024L * 1024L) + 1L);
            bool oversizedRejected = false;
            try { using (EvidencePathPolicy.VerifiedEvidenceFile ignored = EvidencePathPolicy.OpenContainedEvidenceForTests(localRoot, "OpenMeasurementEvidence", "Evidence", "captures\\oversized.json", EvidencePathPolicy.EvidenceFileKind.ManifestJson)) { } }
            catch (InvalidDataException) { oversizedRejected = true; }
            Check("oversized JSON evidence rejected", oversizedRejected);

            string hardLink = Path.Combine(evidenceRoot, "hardlink.csv");
            bool hardLinkCreated = CreateHardLink(hardLink, file, IntPtr.Zero);
            Check("hard link fixture created", hardLinkCreated);
            if (hardLinkCreated)
            {
                bool hardLinkRejected = false;
                try { using (EvidencePathPolicy.VerifiedEvidenceFile ignored = EvidencePathPolicy.OpenContainedEvidenceForTests(localRoot, "OpenMeasurementEvidence", "Evidence", "captures\\run.csv", EvidencePathPolicy.EvidenceFileKind.CaptureCsv)) { } }
                catch (InvalidDataException) { hardLinkRejected = true; }
                Check("hard-linked evidence rejected", hardLinkRejected);
            }
        }
        finally
        {
            if (Directory.Exists(localRoot)) Directory.Delete(localRoot, true);
        }
    }

    private static void TestStrictJsonValidDocument()
    {
        StrictJsonObject root = StrictJsonManifestParser.ParseForTests("{\"array\":[true,false,null],\"number\":-12.5e2,\"rawEmoji\":\"safe 🚀\",\"text\":\"safe \\uD83D\\uDE80\"}") as StrictJsonObject;
        Check("strict JSON object parsed", root != null && root.Count == 4);
        StrictJsonString text = root.GetRequired("text") as StrictJsonString;
        StrictJsonNumber number = root.GetRequired("number") as StrictJsonNumber;
        StrictJsonArray array = root.GetRequired("array") as StrictJsonArray;
        Check("strict JSON surrogate pair", text != null && text.Value == "safe 🚀");
        Check("strict JSON raw supplementary character", ((StrictJsonString)root.GetRequired("rawEmoji")).Value == "safe 🚀");
        Check("strict JSON finite decimal", number != null && number.Value == -1250m);
        Check("strict JSON typed array", array != null && array.Values.Count == 3 && array.Values[0].Kind == StrictJsonKind.Boolean && array.Values[2].Kind == StrictJsonKind.Null);
    }

    private static void TestStrictJsonDuplicateAndTrailingRejected()
    {
        string[] invalid =
        {
            "{\"a\":1,\"a\":2}",
            "{\"a\":1,\"\\u0061\":2}",
            "{} trailing",
            "{\"a\":1,}",
            "/*comment*/{}",
            "\uFEFF{}"
        };
        bool allRejected = true;
        foreach (string json in invalid)
        {
            try { StrictJsonManifestParser.ParseForTests(json); allRejected = false; }
            catch (InvalidDataException) { }
        }
        Check("strict JSON duplicate trailing comment BOM rejected", allRejected);
    }

    private static void TestStrictJsonUnicodeRejected()
    {
        string[] invalid = { "\"\\uD800\"", "\"\\uDC00\"", "\"\\uD800\\u0041\"", "\"" + '\uD800' + "\"" };
        bool allRejected = true;
        foreach (string json in invalid)
        {
            try { StrictJsonManifestParser.ParseForTests(json); allRejected = false; }
            catch (InvalidDataException) { }
        }
        Check("strict JSON invalid Unicode rejected", allRejected);
    }

    private static void TestStrictJsonNumberRejected()
    {
        string[] invalid =
        {
            "01",
            "1.",
            "1e",
            "NaN",
            "Infinity",
            "1e1000",
            "1e-1000",
            "1000.0000000000000000000000000001",
            "0.123456789012345678901234567890"
        };
        bool allRejected = true;
        foreach (string json in invalid)
        {
            try { StrictJsonManifestParser.ParseForTests(json); allRejected = false; }
            catch (InvalidDataException) { }
        }
        Check("strict JSON invalid numbers rejected", allRejected);
    }

    private static void TestStrictJsonDepthAndStringLimitRejected()
    {
        string maximumDepth = new string('[', 16) + new string(']', 16);
        string excessiveDepth = new string('[', 17) + new string(']', 17);
        string longString = "\"" + new string('x', 65537) + "\"";
        string maximumNumber = "0." + new string('0', 126);
        string excessiveNumber = "0." + new string('0', 127);
        bool maximumAccepted = false;
        bool deepRejected = false;
        bool longRejected = false;
        bool maximumNumberAccepted = false;
        bool excessiveNumberRejected = false;
        try { maximumAccepted = StrictJsonManifestParser.ParseForTests(maximumDepth) != null; }
        catch (InvalidDataException) { }
        try { StrictJsonManifestParser.ParseForTests(excessiveDepth); }
        catch (InvalidDataException) { deepRejected = true; }
        try { StrictJsonManifestParser.ParseForTests(longString); }
        catch (InvalidDataException) { longRejected = true; }
        try { maximumNumberAccepted = StrictJsonManifestParser.ParseForTests(maximumNumber) != null; }
        catch (InvalidDataException) { }
        try { StrictJsonManifestParser.ParseForTests(excessiveNumber); }
        catch (InvalidDataException) { excessiveNumberRejected = true; }
        Check("strict JSON maximum depth accepted", maximumAccepted);
        Check("strict JSON excessive depth rejected", deepRejected);
        Check("strict JSON decoded string limit", longRejected);
        Check("strict JSON maximum number length accepted", maximumNumberAccepted);
        Check("strict JSON excessive number length rejected", excessiveNumberRejected);

        StrictJsonArray maximumArray = StrictJsonManifestParser.ParseForTests(MakeFlatArrayJson(10000)) as StrictJsonArray;
        bool excessiveArrayRejected = false;
        try { StrictJsonManifestParser.ParseForTests(MakeFlatArrayJson(10001)); }
        catch (InvalidDataException) { excessiveArrayRejected = true; }
        Check("strict JSON maximum array items accepted", maximumArray != null && maximumArray.Values.Count == 10000);
        Check("strict JSON excessive array items rejected", excessiveArrayRejected);

        StrictJsonObject maximumObject = StrictJsonManifestParser.ParseForTests(MakeObjectJson(1000)) as StrictJsonObject;
        bool excessiveObjectRejected = false;
        try { StrictJsonManifestParser.ParseForTests(MakeObjectJson(1001)); }
        catch (InvalidDataException) { excessiveObjectRejected = true; }
        Check("strict JSON maximum object properties accepted", maximumObject != null && maximumObject.Count == 1000);
        Check("strict JSON excessive object properties rejected", excessiveObjectRejected);

        bool maximumNodesAccepted = false;
        bool excessiveNodesRejected = false;
        try { maximumNodesAccepted = StrictJsonManifestParser.ParseForTests(MakeNodeBoundaryJson(false)) != null; }
        catch (InvalidDataException) { }
        try { StrictJsonManifestParser.ParseForTests(MakeNodeBoundaryJson(true)); }
        catch (InvalidDataException) { excessiveNodesRejected = true; }
        Check("strict JSON maximum nodes accepted", maximumNodesAccepted);
        Check("strict JSON excessive nodes rejected", excessiveNodesRejected);
    }

    private static void TestStrictJsonVerifiedManifest()
    {
        string localRoot = Path.Combine(Path.GetTempPath(), "OpenMeasurementEvidence-json-" + Guid.NewGuid().ToString("N"));
        string manifestRoot = Path.Combine(localRoot, "OpenMeasurementEvidence", "Evidence", "manifests");
        Directory.CreateDirectory(manifestRoot);
        string file = Path.Combine(manifestRoot, "run.json");
        string wrongKindFile = Path.Combine(manifestRoot, "not-a-manifest.csv");
        try
        {
            File.WriteAllText(file, "{\"schemaVersion\":1}", new UTF8Encoding(false));
            File.WriteAllText(wrongKindFile, "{\"schemaVersion\":1}", new UTF8Encoding(false));
            using (EvidencePathPolicy.VerifiedEvidenceFile evidence = EvidencePathPolicy.OpenContainedEvidenceForTests(localRoot, "OpenMeasurementEvidence", "Evidence", "manifests\\run.json", EvidencePathPolicy.EvidenceFileKind.ManifestJson))
            {
                StrictJsonObject manifest = StrictJsonManifestParser.ParseVerifiedManifest(evidence);
                Check("strict JSON verified manifest", manifest.Count == 1 && manifest.GetRequired("schemaVersion").Kind == StrictJsonKind.Number);
                bool secondParseRejected = false;
                try { StrictJsonManifestParser.ParseVerifiedManifest(evidence); }
                catch (InvalidOperationException) { secondParseRejected = true; }
                Check("strict JSON verified manifest parse once", secondParseRejected);
            }
            bool wrongKindRejected = false;
            try
            {
                using (EvidencePathPolicy.VerifiedEvidenceFile evidence = EvidencePathPolicy.OpenContainedEvidenceForTests(localRoot, "OpenMeasurementEvidence", "Evidence", "manifests\\not-a-manifest.csv", EvidencePathPolicy.EvidenceFileKind.CaptureCsv))
                    StrictJsonManifestParser.ParseVerifiedManifest(evidence);
            }
            catch (InvalidDataException) { wrongKindRejected = true; }
            Check("strict JSON cross-kind manifest rejected", wrongKindRejected);

            string bomFile = Path.Combine(manifestRoot, "bom.json");
            File.WriteAllBytes(bomFile, new byte[] { 0xEF, 0xBB, 0xBF, (byte)'{', (byte)'}' });
            bool verifiedBomRejected = false;
            try
            {
                using (EvidencePathPolicy.VerifiedEvidenceFile evidence = EvidencePathPolicy.OpenContainedEvidenceForTests(localRoot, "OpenMeasurementEvidence", "Evidence", "manifests\\bom.json", EvidencePathPolicy.EvidenceFileKind.ManifestJson))
                    StrictJsonManifestParser.ParseVerifiedManifest(evidence);
            }
            catch (InvalidDataException) { verifiedBomRejected = true; }
            Check("strict JSON verified BOM rejected", verifiedBomRejected);
        }
        finally
        {
            if (Directory.Exists(localRoot)) Directory.Delete(localRoot, true);
        }
    }

    private static void TestHighVariation()
    {
        IList<RunMetrics> baseline = MakeRunSet("base", 16.6, 16.7, 25.0);
        IList<RunMetrics> candidate = MakeRunSet("candidate", 14.0, 14.1, 14.2);
        ComparisonResult result = RunComparison.Compare(baseline, candidate);
        Check("high variation", result.Verdict == ComparisonVerdict.Inconclusive);
    }

    private static void TestDuplicateRunId()
    {
        IList<RunMetrics> baseline = MakeRunSet("base", 16.6, 16.7, 16.5);
        IList<RunMetrics> candidate = MakeRunSet("candidate", 14.0, 14.1, 13.9);
        candidate[0].RunId = baseline[0].RunId;
        ComparisonResult result = RunComparison.Compare(baseline, candidate);
        Check("duplicate run id", result.Verdict == ComparisonVerdict.Inconclusive);
    }

    private static void TestExplicitPresentationTiming()
    {
        StringBuilder csv = new StringBuilder(CsvHeader("MsBetweenPresents", false));
        for (int i = 0; i < 1201; i++) csv.Append("testgame.exe,4242,0xABC,DXGI,Hardware: Independent Flip,8.333\n");
        RunMetrics metrics = AnalyzeWithTiming("MsBetweenPresents", csv.ToString(), "presentation");
        Check("explicit timing column", metrics.TimingColumn == "MsBetweenPresents");
        Check("explicit timing warning", metrics.Warnings.Count >= 2);
    }

    private static void TestMalformedQuotedCsv()
    {
        bool threw = false;
        try { Analyze("malformed-quote", "Application,ProcessID,SwapChainAddress,PresentRuntime,PresentMode,DisplayedTime\n\"testgame.exe\"x,4242,0xABC,DXGI,Mode,16\n"); }
        catch (InvalidDataException) { threw = true; }
        Check("malformed quoted csv", threw);
    }

    private static void TestDuplicateHeader()
    {
        bool duplicateRejected = false;
        bool whitespaceRejected = false;
        bool bomRejected = false;
        bool caseRejected = false;
        try { Analyze("duplicate-header", "DisplayedTime,DisplayedTime\n16,16\n"); }
        catch (InvalidDataException) { duplicateRejected = true; }
        try { Analyze("whitespace-header", " Application,ProcessID,SwapChainAddress,PresentRuntime,PresentMode,DisplayedTime\ntestgame.exe,4242,0xABC,DXGI,Hardware: Independent Flip,16\n"); }
        catch (InvalidDataException) { whitespaceRejected = true; }
        try { Analyze("bom-header", "\uFEFFApplication,ProcessID,SwapChainAddress,PresentRuntime,PresentMode,DisplayedTime\ntestgame.exe,4242,0xABC,DXGI,Hardware: Independent Flip,16\n"); }
        catch (InvalidDataException) { bomRejected = true; }
        try { Analyze("case-header", "application,ProcessID,SwapChainAddress,PresentRuntime,PresentMode,DisplayedTime\ntestgame.exe,4242,0xABC,DXGI,Hardware: Independent Flip,16\n"); }
        catch (InvalidDataException) { caseRejected = true; }
        Check("duplicate and non-exact headers", duplicateRejected && whitespaceRejected && bomRejected && caseRejected);
    }

    private static void TestUnreasonableIntervalRejected()
    {
        StringBuilder csv = new StringBuilder(CsvHeader("DisplayedTime", false));
        for (int i = 0; i < 40; i++) csv.Append("testgame.exe,4242,0xABC,DXGI,Hardware: Independent Flip,1000000\n");
        bool threw = false;
        try { Analyze("huge-interval", csv.ToString()); }
        catch (InvalidDataException) { threw = true; }
        Check("unreasonable interval", threw);
    }

    private static void TestInvalidRunId()
    {
        bool threw = false;
        try { AnalyzeWithManifest("not-a-guid", "context", MakeFrames(40, 16.0, 0, false)); }
        catch (InvalidDataException) { threw = true; }
        Check("invalid run id", threw);
    }

    private static void TestMissingContext()
    {
        bool threw = false;
        try { AnalyzeWithManifest(Guid.NewGuid().ToString("N"), null, MakeFrames(40, 16.0, 0, false)); }
        catch (InvalidDataException) { threw = true; }
        Check("missing context", threw);
    }

    private static void TestMixedSwapChainRejected()
    {
        string csv = MakeFrames(640, 16.0, 0, false) + "testgame.exe,4242,0xDEF,DXGI,Hardware: Independent Flip,16.000,Application\n";
        bool threw = false;
        try { Analyze("mixed-swapchain", csv); }
        catch (InvalidDataException) { threw = true; }
        Check("mixed swap chain", threw);
    }

    private static void TestTimingMismatchRejected()
    {
        IList<RunMetrics> baseline = MakeRunSet("base", 16.6, 16.7, 16.5);
        IList<RunMetrics> candidate = MakeRunSet("candidate", 14.0, 14.1, 13.9);
        candidate[0].TimingColumn = "MsBetweenPresents";
        ComparisonResult result = RunComparison.Compare(baseline, candidate);
        Check("timing mismatch", result.Verdict == ComparisonVerdict.Inconclusive);
    }

    private static void TestMalformedDisplayedTimeRejected()
    {
        string csv = MakeFrames(640, 16.0, 0, false).Replace(",16.000,Application", ",oops,Application");
        string lowercaseNa = MakeFrames(640, 16.0, 0, false).Replace(",16.000,Application", ",na,Application");
        bool malformedRejected = false;
        bool lowercaseNaRejected = false;
        try { Analyze("malformed-displayed", csv); }
        catch (InvalidDataException) { malformedRejected = true; }
        try { Analyze("lowercase-na", lowercaseNa); }
        catch (InvalidDataException) { lowercaseNaRejected = true; }
        Check("malformed displayed time", malformedRejected && lowercaseNaRejected);
    }

    private static void TestQuotedFieldLimit()
    {
        bool threw = false;
        try { Analyze("oversized-quoted", "\"" + new string('x', 70000) + "\"\n"); }
        catch (InvalidDataException) { threw = true; }
        Check("quoted field limit", threw);
    }

    private static void TestNonFiniteComparisonMetric()
    {
        IList<RunMetrics> baseline = MakeRunSet("base", 16.6, 16.7, 16.5);
        IList<RunMetrics> candidate = MakeRunSet("candidate", 14.0, 14.1, 13.9);
        candidate[0].AverageFps = double.NaN;
        ComparisonResult result = RunComparison.Compare(baseline, candidate);
        Check("non-finite comparison metric", result.Verdict == ComparisonVerdict.Inconclusive);
    }

    private static void TestUnknownFrameTypeRejected()
    {
        string csv = MakeFrames(640, 16.0, 0, false).Replace(",Application", ",Future_Frame_Type");
        bool threw = false;
        try { Analyze("unknown-frame-type", csv); }
        catch (InvalidDataException) { threw = true; }
        Check("unknown frame type", threw);
    }

    private static void TestNonAlternatingSequenceRejected()
    {
        IList<RunMetrics> baseline = MakeRunSet("base", 16.6, 16.7, 16.5);
        IList<RunMetrics> candidate = MakeRunSet("candidate", 14.0, 14.1, 13.9);
        candidate[0].CaptureSequenceIndex = 8;
        ComparisonResult result = RunComparison.Compare(baseline, candidate);
        Check("non-alternating sequence", result.Verdict == ComparisonVerdict.Inconclusive);
    }

    private static void TestUnderpoweredComparisonRejected()
    {
        IList<RunMetrics> baseline = MakeRunSet("base", 16.6, 16.7, 16.5);
        IList<RunMetrics> candidate = MakeRunSet("candidate", 14.0, 14.1, 13.9);
        candidate[0].ValidFrameCount = 999;
        ComparisonResult result = RunComparison.Compare(baseline, candidate);
        Check("underpowered comparison", result.Verdict == ComparisonVerdict.Inconclusive);
    }

    private static void TestBlankDisplayedTimeRejected()
    {
        string csv = MakeFrames(640, 16.0, 0, false).Replace(",16.000,Application", ",,Application");
        bool threw = false;
        try { Analyze("blank-displayed", csv); }
        catch (InvalidDataException) { threw = true; }
        Check("blank displayed time", threw);
    }

    private static void TestAlternateBasisMalformedDisplayedRejected()
    {
        StringBuilder csv = new StringBuilder("Application,ProcessID,SwapChainAddress,PresentRuntime,PresentMode,DisplayedTime,MsBetweenPresents,FrameType\n");
        for (int i = 0; i < 1900; i++) csv.Append("testgame.exe,4242,0xABC,DXGI,Hardware: Independent Flip,oops,16.000,Application\n");
        bool threw = false;
        try { AnalyzeWithTiming("MsBetweenPresents", csv.ToString(), "alternate-malformed"); }
        catch (InvalidDataException) { threw = true; }
        Check("alternate basis malformed displayed time", threw);
    }

    private static void TestUnequalCountsRejected()
    {
        IList<RunMetrics> baseline = MakeRunSet("base", 16.6, 16.7, 16.5);
        IList<RunMetrics> candidate = MakeRunSet("candidate", 14.0, 14.1, 13.9);
        candidate.RemoveAt(candidate.Count - 1);
        ComparisonResult result = RunComparison.Compare(baseline, candidate);
        Check("unequal counts", result.Verdict == ComparisonVerdict.Inconclusive);
    }

    private static void TestSequenceGapRejected()
    {
        IList<RunMetrics> baseline = MakeRunSet("base", 16.6, 16.7, 16.5);
        IList<RunMetrics> candidate = MakeRunSet("candidate", 14.0, 14.1, 13.9);
        candidate[2].CaptureSequenceIndex = 8;
        ComparisonResult result = RunComparison.Compare(baseline, candidate);
        Check("sequence gap", result.Verdict == ComparisonVerdict.Inconclusive);
    }

    private static void TestPlannedDurationMismatchRejected()
    {
        IList<RunMetrics> baseline = MakeRunSet("base", 16.6, 16.7, 16.5);
        IList<RunMetrics> candidate = MakeRunSet("candidate", 14.0, 14.1, 13.9);
        candidate[0].PlannedCaptureSeconds = 60;
        ComparisonResult result = RunComparison.Compare(baseline, candidate);
        Check("planned duration mismatch", result.Verdict == ComparisonVerdict.Inconclusive);
    }

    private static void TestNullRunRejected()
    {
        IList<RunMetrics> baseline = MakeRunSet("base", 16.6, 16.7, 16.5);
        IList<RunMetrics> candidate = MakeRunSet("candidate", 14.0, 14.1, 13.9);
        candidate[0] = null;
        ComparisonResult result = RunComparison.Compare(baseline, candidate);
        Check("null run", result.Verdict == ComparisonVerdict.Inconclusive);
    }

    private static void TestFrameGenerationMismatchRejected()
    {
        IList<RunMetrics> baseline = MakeRunSet("base", 16.6, 16.7, 16.5);
        IList<RunMetrics> candidate = MakeRunSet("candidate", 14.0, 14.1, 13.9);
        candidate[0].FrameGenerationState = "unknown";
        ComparisonResult result = RunComparison.Compare(baseline, candidate);
        Check("frame generation mismatch", result.Verdict == ComparisonVerdict.Inconclusive);
    }

    private static void TestTargetTopologyVariantsRejected()
    {
        string normal = MakeFrames(640, 16.0, 0, false);
        string[] variants =
        {
            normal.Replace("testgame.exe,4242", "other.exe,4242"),
            normal.Replace("testgame.exe,4242", "testgame.exe,4343"),
            normal.Replace(",DXGI,", ",OtherRuntime,"),
            normal.Replace(",Hardware: Independent Flip,", ",Composed: Flip,"),
            normal.Replace(",DXGI,", ", DXGI,"),
            normal.Replace(",Hardware: Independent Flip,", ",Hardware: Independent Flip ,")
        };
        bool allRejected = true;
        foreach (string variant in variants)
        {
            try { Analyze("target-variant", variant); allRejected = false; }
            catch (InvalidDataException) { }
        }
        Check("target topology variants", allRejected);
    }

    private static void TestMissingDisplayedCellRejected()
    {
        string csv = MakeFrames(640, 16.0, 0, false) + "testgame.exe,4242,0xABC,DXGI,Hardware: Independent Flip\n";
        bool threw = false;
        try { Analyze("missing-displayed-cell", csv); }
        catch (InvalidDataException) { threw = true; }
        Check("missing displayed cell", threw);
    }

    private static void TestUndisplayedMetricUnavailableComparison()
    {
        IList<RunMetrics> baseline = MakeRunSet("base", 16.6, 16.7, 16.5);
        IList<RunMetrics> candidate = MakeRunSet("candidate", 14.0, 14.1, 13.9);
        candidate[0].UndisplayedMetricAvailable = false;
        candidate[0].UndisplayedFramesPerThousand = double.NaN;
        ComparisonResult result = RunComparison.Compare(baseline, candidate);
        Check("undisplayed metric unavailable", result.Verdict == ComparisonVerdict.Inconclusive);
    }

    private static void TestManifestV1Valid()
    {
        string valid = MakeValidManifestJson();
        MeasurementRunManifestV1 parsed = MeasurementRunManifestV1Validator.ParseForTests(valid);
        MeasurementRunManifestV1 withoutOptionalNotes = MeasurementRunManifestV1Validator.ParseForTests(valid.Replace(",\"notes\":\"Synthetic only.\"", string.Empty));
        Check("manifest v1 valid run", parsed.RunId.Length == 36);
        CheckKnownAnswer("manifest v1 capture policy identity", "58706ECC4A18B89B23F6B8ADA88870B1EF2657CBA7353FC4C54730D0304D6575", parsed.CapturePolicyHash);
        CheckKnownAnswer("manifest v1 capture invocation identity", "7A3BED868CCCBE0C76540EB18BF6D1B04516E9C04D8841A8D91171F7E5DF3C00", parsed.CaptureInvocationHash);
        CheckKnownAnswer("manifest v1 context identity", "38AB4587C046A05C5B3455413424B1FEB51F252B4B5BA5F5F22482BF093EFC37", parsed.ComparisonContextId);
        MeasurementRunManifestV1 excludedLabelChange = MeasurementRunManifestV1Validator.ParseForTests(valid.Replace("\"label\":\"baseline\"", "\"label\":\"baseline-repeat-2\""));
        Check("manifest v1 excluded label invariance", excludedLabelChange.ComparisonContextId == parsed.ComparisonContextId);
        Check("manifest v1 baseline scope", parsed.ConfigurationRole == "baseline" && parsed.ChangedVariablesKey == "open-measurement-evidence-baseline-empty-v1" && withoutOptionalNotes != null);
    }

    private static void TestManifestV1UnknownAndMissingRejected()
    {
        string valid = MakeValidManifestJson();
        bool unknownRejected = ThrowsInvalidData(valid.Replace("\"schemaVersion\":1", "\"schemaVersion\":1,\"unexpected\":true"));
        bool missingRejected = ThrowsInvalidData(valid.Replace("\"buildId\":\"synthetic-build-1\",", string.Empty));
        bool wrongTypeRejected = ThrowsInvalidData(valid.Replace("\"targetProcessId\":4242", "\"targetProcessId\":\"4242\""));
        Check("manifest v1 unknown/missing/type", unknownRejected && missingRejected && wrongTypeRejected);
    }

    private static void TestManifestV1IdentityMismatchRejected()
    {
        string valid = MakeValidManifestJson();
        string badPolicy = valid.Replace("\"capturePolicyHash\":\"" + ExtractJsonString(valid, "capturePolicyHash") + "\"", "\"capturePolicyHash\":\"" + new string('A', 64) + "\"");
        string badInvocation = valid.Replace("\"captureInvocationHash\":\"" + ExtractJsonString(valid, "captureInvocationHash") + "\"", "\"captureInvocationHash\":\"" + new string('B', 64) + "\"");
        string badContext = valid.Replace("\"comparisonContextId\":\"" + ExtractJsonString(valid, "comparisonContextId") + "\"", "\"comparisonContextId\":\"" + new string('C', 64) + "\"");
        string includedFieldMutation = valid.Replace("\"gpu\":\"synthetic\"", "\"gpu\":\"different-gpu\"");
        Check("manifest v1 identity mismatch", ThrowsInvalidData(badPolicy) && ThrowsInvalidData(badInvocation) && ThrowsInvalidData(badContext) && ThrowsInvalidData(includedFieldMutation));
    }

    private static void TestManifestV1ProtocolScopeRejected()
    {
        string valid = MakeValidManifestJson();
        bool candidateRejected = ThrowsInvalidData(valid.Replace("\"role\":\"baseline\"", "\"role\":\"candidate\""));
        bool changedRejected = ThrowsInvalidData(valid.Replace("\"changedVariables\":[]", "\"changedVariables\":[\"HAGS\"]"));
        bool nonV4Rejected = ThrowsInvalidData(valid.Replace(ExtractJsonString(valid, "runId"), "00000000-0000-1000-8000-000000000000"));
        bool nonUtcRejected = ThrowsInvalidData(valid.Replace("2026-08-08T02:30:00Z", "2026-08-07T21:30:00-05:00"));
        bool spaceTimestampRejected = ThrowsInvalidData(valid.Replace("2026-08-08T02:30:00Z", "2026-08-08 02:30:00Z"));
        bool missingSecondsRejected = ThrowsInvalidData(valid.Replace("2026-08-08T02:30:00Z", "2026-08-08T02:30Z"));
        bool reservedExecutableRejected = ThrowsInvalidData(valid.Replace("testgame.exe", "NUL.exe"));
        bool executableTooLongRejected = ThrowsInvalidData(valid.Replace("testgame.exe", new string('a', 252) + ".exe"));
        Check("manifest v1 owner beta scope", candidateRejected && changedRejected && nonV4Rejected && nonUtcRejected && spaceTimestampRejected && missingSecondsRejected && reservedExecutableRejected && executableTooLongRejected);
    }

    private static void TestSettingsSnapshotV1Valid()
    {
        GameSettingsSnapshotV1 parsed = GameSettingsSnapshotV1Validator.ParseForTests(MakeValidSettingsJson());
        Check("settings snapshot v1 identity", parsed.GameExecutable == "testgame.exe" && parsed.GameBuild == "synthetic-build-1" && parsed.GraphicsApi == "DX12");
        Check("settings snapshot v1 entries", parsed.Settings.Count() == 3 && parsed.Settings.First().Key == "frameGeneration");
        MeasurementRunManifestV1 manifest = MeasurementRunManifestV1Validator.ParseForTests(MakeValidManifestJson());
        bool matched = true;
        try { GameSettingsSnapshotV1Validator.RequireMatchesManifest(parsed, manifest); }
        catch (InvalidDataException) { matched = false; }
        Check("settings snapshot matches manifest", matched);
    }

    private static void TestSettingsSnapshotV1Rejected()
    {
        string valid = MakeValidSettingsJson();
        string reversed = valid.Replace(
            "{\"key\":\"frameLimit\",\"value\":\"240\",\"source\":\"verified-config\"},{\"key\":\"qualityPreset\",\"value\":\"High\",\"source\":\"verified-config\"}",
            "{\"key\":\"qualityPreset\",\"value\":\"High\",\"source\":\"verified-config\"},{\"key\":\"frameLimit\",\"value\":\"240\",\"source\":\"verified-config\"}");
        bool orderRejected = reversed != valid && ThrowsSettingsInvalid(reversed);
        bool unknownRejected = ThrowsSettingsInvalid(valid.Replace("\"schemaVersion\":1", "\"schemaVersion\":1,\"unknown\":true"));
        bool frameGenerationRejected = ThrowsSettingsInvalid(valid.Replace("\"frameGenerationState\":\"off\"", "\"frameGenerationState\":\"on\""));
        bool executablePathRejected = ThrowsSettingsInvalid(valid.Replace("\"testgame.exe\"", "\"folder/testgame.exe\""));
        bool reservedExecutableRejected = ThrowsSettingsInvalid(valid.Replace("\"testgame.exe\"", "\"NUL.exe\""));
        GameSettingsSnapshotV1 mismatch = GameSettingsSnapshotV1Validator.ParseForTests(valid.Replace("\"displayMode\":\"2560x1440\"", "\"displayMode\":\"1920x1080\""));
        bool mismatchRejected = false;
        try { GameSettingsSnapshotV1Validator.RequireMatchesManifest(mismatch, MeasurementRunManifestV1Validator.ParseForTests(MakeValidManifestJson())); }
        catch (InvalidDataException) { mismatchRejected = true; }
        Check("settings snapshot v1 rejects unsafe variants", orderRejected && unknownRejected && frameGenerationRejected && executablePathRejected && reservedExecutableRejected && mismatchRejected);
    }

    private static void TestEvidenceBundleV1Valid()
    {
        string localRoot = Path.Combine(Path.GetTempPath(), "OpenMeasurementEvidence-bundle-valid-" + Guid.NewGuid().ToString("N"));
        string evidenceRoot = Path.Combine(localRoot, "OpenMeasurementEvidence", "Evidence");
        try
        {
            Directory.CreateDirectory(Path.Combine(evidenceRoot, "manifests"));
            Directory.CreateDirectory(Path.Combine(evidenceRoot, "settings"));
            Directory.CreateDirectory(Path.Combine(evidenceRoot, "captures"));
            string settings = MakeValidSettingsJson();
            string csv = MakeFrames(1900, 16.0, 0, false);
            File.WriteAllText(Path.Combine(evidenceRoot, "settings", "test.json"), settings, new UTF8Encoding(false));
            File.WriteAllText(Path.Combine(evidenceRoot, "captures", "test.csv"), csv, new UTF8Encoding(false));
            string manifest = MakeValidManifestJson(Sha256Text(settings), Sha256Text(csv));
            File.WriteAllText(Path.Combine(evidenceRoot, "manifests", "run.json"), manifest, new UTF8Encoding(false));
            using (EvidenceBundleV1 bundle = EvidenceBundleV1.OpenContainedForTests(
                localRoot, "OpenMeasurementEvidence", "Evidence", "manifests\\run.json", MakeSyntheticCaptureContract()))
            {
                RunMetrics metrics = bundle.AnalyzeOnce();
                Check("verified evidence bundle analyzes exact CSV", metrics.ValidFrameCount == 1900 && bundle.Settings.Settings.Count() == 3);
                bool secondAnalysisRejected = false;
                try { bundle.AnalyzeOnce(); }
                catch (InvalidOperationException) { secondAnalysisRejected = true; }
                Check("verified evidence bundle analyzes once", secondAnalysisRejected);
            }
            using (EvidenceBundleV1 concurrentBundle = EvidenceBundleV1.OpenContainedForTests(
                localRoot, "OpenMeasurementEvidence", "Evidence", "manifests\\run.json", MakeSyntheticCaptureContract()))
            using (ManualResetEventSlim start = new ManualResetEventSlim(false))
            {
                int succeeded = 0;
                int rejected = 0;
                Action attempt = delegate
                {
                    start.Wait();
                    try { concurrentBundle.AnalyzeOnce(); Interlocked.Increment(ref succeeded); }
                    catch (InvalidOperationException) { Interlocked.Increment(ref rejected); }
                };
                Task first = Task.Factory.StartNew(attempt);
                Task second = Task.Factory.StartNew(attempt);
                start.Set();
                Task.WaitAll(first, second);
                Check("verified evidence bundle concurrent analyze once", succeeded == 1 && rejected == 1);
            }
            EvidenceBundleV1 concurrentDisposeBundle = EvidenceBundleV1.OpenContainedForTests(
                localRoot, "OpenMeasurementEvidence", "Evidence", "manifests\\run.json", MakeSyntheticCaptureContract());
            Task disposeFirst = Task.Factory.StartNew(concurrentDisposeBundle.Dispose);
            Task disposeSecond = Task.Factory.StartNew(concurrentDisposeBundle.Dispose);
            Task.WaitAll(disposeFirst, disposeSecond);
            bool handleReleasedAfterBothDisposals = false;
            using (FileStream reopened = new FileStream(Path.Combine(evidenceRoot, "captures", "test.csv"), FileMode.Open, FileAccess.ReadWrite, FileShare.None))
                handleReleasedAfterBothDisposals = reopened.Length > 0;
            Check("verified evidence bundle concurrent dispose completes handle release", handleReleasedAfterBothDisposals);
        }
        finally
        {
            if (Directory.Exists(localRoot)) Directory.Delete(localRoot, true);
        }
    }

    private static void TestEvidenceBundleV1TamperAndContractRejected()
    {
        string localRoot = Path.Combine(Path.GetTempPath(), "OpenMeasurementEvidence-bundle-reject-" + Guid.NewGuid().ToString("N"));
        string evidenceRoot = Path.Combine(localRoot, "OpenMeasurementEvidence", "Evidence");
        try
        {
            Directory.CreateDirectory(Path.Combine(evidenceRoot, "manifests"));
            Directory.CreateDirectory(Path.Combine(evidenceRoot, "settings"));
            Directory.CreateDirectory(Path.Combine(evidenceRoot, "captures"));
            string settings = MakeValidSettingsJson();
            string csv = MakeFrames(1900, 16.0, 0, false);
            string settingsPath = Path.Combine(evidenceRoot, "settings", "test.json");
            File.WriteAllText(settingsPath, settings, new UTF8Encoding(false));
            File.WriteAllText(Path.Combine(evidenceRoot, "captures", "test.csv"), csv, new UTF8Encoding(false));
            File.WriteAllText(Path.Combine(evidenceRoot, "manifests", "run.json"), MakeValidManifestJson(Sha256Text(settings), Sha256Text(csv)), new UTF8Encoding(false));

            File.WriteAllText(settingsPath, settings.Replace("\"High\"", "\"Low\""), new UTF8Encoding(false));
            bool tamperRejected = false;
            try { using (EvidenceBundleV1 ignored = EvidenceBundleV1.OpenContainedForTests(localRoot, "OpenMeasurementEvidence", "Evidence", "manifests\\run.json", MakeSyntheticCaptureContract())) { } }
            catch (InvalidDataException) { tamperRejected = true; }
            string semanticMismatch = settings.Replace("\"value\":\"240\"", "\"value\":\"1\"");
            File.WriteAllText(settingsPath, semanticMismatch, new UTF8Encoding(false));
            File.WriteAllText(Path.Combine(evidenceRoot, "manifests", "run.json"), MakeValidManifestJson(Sha256Text(semanticMismatch), Sha256Text(csv)), new UTF8Encoding(false));
            bool semanticMismatchRejected = false;
            try { using (EvidenceBundleV1 ignored = EvidenceBundleV1.OpenContainedForTests(localRoot, "OpenMeasurementEvidence", "Evidence", "manifests\\run.json", MakeSyntheticCaptureContract())) { } }
            catch (InvalidDataException) { semanticMismatchRejected = true; }
            File.WriteAllText(settingsPath, settings, new UTF8Encoding(false));
            File.WriteAllText(Path.Combine(evidenceRoot, "manifests", "run.json"), MakeValidManifestJson(Sha256Text(settings), Sha256Text(csv)), new UTF8Encoding(false));

            bool contractRejected = false;
            EvidenceCaptureContractV1 wrongContract = new EvidenceCaptureContractV1(
                "synthetic-fixture", "2.0", new string('1', 64), "synthetic-v1", "frame-analysis-v1-draft", "not-applicable",
                "testgame.exe", "synthetic-build-1", "fixed-route-60-seconds", "DX12", "DXGI", "Hardware: Independent Flip", "verified-config", MakeExpectedSettings());
            try { using (EvidenceBundleV1 ignored = EvidenceBundleV1.OpenContainedForTests(localRoot, "OpenMeasurementEvidence", "Evidence", "manifests\\run.json", wrongContract)) { } }
            catch (InvalidDataException) { contractRejected = true; }
            Check("verified evidence bundle rejects tamper, semantic mismatch, and contract mismatch", tamperRejected && semanticMismatch != settings && semanticMismatchRejected && contractRejected);
        }
        finally
        {
            if (Directory.Exists(localRoot)) Directory.Delete(localRoot, true);
        }
    }

    private static void TestPublishedExamples()
    {
        DirectoryInfo current = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
        while (current != null && !File.Exists(Path.Combine(current.FullName, "OpenMeasurementEvidence.sln")))
            current = current.Parent;
        if (current == null)
        {
            Check("published examples repository root", false);
            return;
        }

        string manifest = File.ReadAllText(Path.Combine(current.FullName, "examples", "measurement-run-manifest-v1.example.json"), Encoding.UTF8);
        string settings = File.ReadAllText(Path.Combine(current.FullName, "examples", "game-settings-snapshot-v1.example.json"), Encoding.UTF8);
        bool valid = true;
        try
        {
            MeasurementRunManifestV1Validator.ParseForTests(manifest);
            GameSettingsSnapshotV1Validator.ParseForTests(settings);
        }
        catch (InvalidDataException)
        {
            valid = false;
        }
        Check("published examples validate", valid);
    }

    private static IList<RunMetrics> MakeRunSet(string label, params double[] frameTimes)
    {
        List<RunMetrics> result = new List<RunMetrics>();
        for (int i = 0; i < frameTimes.Length; i++)
        {
            int frameCount = (int)Math.Ceiling(30000.0 / frameTimes[i]);
            RunMetrics metrics = Analyze(label + i, label, MakeFrames(frameCount, frameTimes[i], 0, false));
            metrics.CaptureSequenceIndex = (label == "base" ? 1 : 2) + (i * 2);
            metrics.RepetitionIndex = i + 1;
            result.Add(metrics);
        }
        return result;
    }

    private static RunMetrics Analyze(string id, string csv)
    {
        return Analyze(id, id, csv);
    }

    private static RunMetrics Analyze(string id, string configurationLabel, string csv)
    {
        return AnalyzeWithManifest(Guid.NewGuid().ToString("N"), new string('2', 64), csv, configurationLabel);
    }

    private static RunMetrics AnalyzeWithManifest(string runId, string context, string csv)
    {
        return AnalyzeWithManifest(runId, context, csv, "test-configuration");
    }

    private static RunMetrics AnalyzeWithManifest(string runId, string context, string csv, string configurationLabel)
    {
        return AnalyzeWithManifest(runId, context, csv, configurationLabel, "DisplayedTime");
    }

    private static RunMetrics AnalyzeWithTiming(string timingBasis, string csv, string configurationLabel)
    {
        return AnalyzeWithManifest(Guid.NewGuid().ToString("N"), new string('2', 64), csv, configurationLabel, timingBasis);
    }

    private static RunMetrics AnalyzeWithFrameGeneration(string state, string technology, string csv, string configurationLabel)
    {
        return AnalyzeWithManifestAndHashes(Guid.NewGuid().ToString("N"), new string('2', 64), csv, configurationLabel, "DisplayedTime", state, new string('5', 64), new string('6', 64), technology);
    }

    private static RunMetrics AnalyzeWithManifest(string runId, string context, string csv, string configurationLabel, string timingBasis)
    {
        return AnalyzeWithManifest(runId, context, csv, configurationLabel, timingBasis, "off");
    }

    private static RunMetrics AnalyzeWithManifest(string runId, string context, string csv, string configurationLabel, string timingBasis, string frameGenerationState)
    {
        return AnalyzeWithManifestAndHashes(runId, context, csv, configurationLabel, timingBasis, frameGenerationState, new string('5', 64), new string('6', 64));
    }

    private static RunMetrics AnalyzeWithCaptureHashes(string capturePolicyHash, string captureInvocationHash, string configurationLabel)
    {
        return AnalyzeWithManifestAndHashes(Guid.NewGuid().ToString("N"), new string('2', 64), MakeFrames(1900, 16.0, 0, false), configurationLabel, "DisplayedTime", "off", capturePolicyHash, captureInvocationHash);
    }

    private static CaptureCommandIdentity MakeCaptureIdentity(int processId, string outputRelativePath, string sessionName)
    {
        return new CaptureCommandIdentity
        {
            ProtocolIdentifier = CaptureCommandIdentity.EvidenceProtocolIdentifier,
            TimingBasis = "DisplayedTime",
            ProcessId = processId,
            OutputRelativePath = outputRelativePath,
            SessionName = sessionName,
            PlannedTimedSeconds = 60,
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

    private static RunMetrics AnalyzeWithManifestAndHashes(string runId, string context, string csv, string configurationLabel, string timingBasis, string frameGenerationState, string capturePolicyHash, string captureInvocationHash)
    {
        string technology = frameGenerationState == "off" ? "none" : frameGenerationState == "unknown" ? "unknown" : "intel-xess-fg";
        return AnalyzeWithManifestAndHashes(runId, context, csv, configurationLabel, timingBasis, frameGenerationState, capturePolicyHash, captureInvocationHash, technology);
    }

    private static RunMetrics AnalyzeWithManifestAndHashes(string runId, string context, string csv, string configurationLabel, string timingBasis, string frameGenerationState, string capturePolicyHash, string captureInvocationHash, string frameGenerationTechnology)
    {
        RunManifest manifest = new RunManifest
        {
            RunId = runId,
            ProtocolVersion = "1",
            CaptureSource = "synthetic",
            GameExecutable = "testgame.exe",
            GameBuild = "1",
            Scenario = "fixed-route",
            GraphicsApi = "DX12",
            DisplayMode = "2560x1440@240",
            ComparisonContextId = context,
            ConfigurationLabel = configurationLabel,
            CaptureSchemaVersion = "presentmon-2.5.1-test",
            TimingBasis = timingBasis,
            TargetApplication = "testgame.exe",
            TargetProcessId = 4242,
            TargetSwapChainAddress = "0xABC",
            PlannedCaptureSeconds = 30,
            SlowFrameThresholdMs = 50.0,
            CaptureSequenceIndex = 1,
            CaptureSourceVersion = "2.5.1-test",
            CaptureSourceSha256 = new string('1', 64),
            CapturePolicyHash = capturePolicyHash,
            CaptureInvocationHash = captureInvocationHash,
            SessionName = "test-session-1234",
            NoTrackInput = true,
            GameSettingsHash = new string('3', 64),
            ChangedVariablesKey = "synthetic-test-variable",
            TimingDefinitionVersion = "frame-analysis-v1-draft",
            FrameGenerationState = frameGenerationState,
            FrameGenerationTechnology = frameGenerationTechnology,
            RepetitionIndex = 1,
            PlannedRepetitions = 3,
            ExpectedPresentRuntime = "DXGI",
            ExpectedPresentMode = "Hardware: Independent Flip"
        };
        return PresentMonCsvAnalyzer.Analyze(new StringReader(csv), ValidatedRunManifest.CreateForUnboundAnalysis(manifest));
    }

    private static string MakeFrames(int count, double frameTime, int dropped, bool generated)
    {
        StringBuilder csv = new StringBuilder(CsvHeader("DisplayedTime", true));
        for (int i = 0; i < count; i++)
        {
            string time = i < dropped ? "NA" : frameTime.ToString("0.000", CultureInfo.InvariantCulture);
            string type = generated && i % 2 == 1 ? "Intel XeSS-FG" : "Application";
            csv.Append("testgame.exe,4242,0xABC,DXGI,Hardware: Independent Flip,").Append(time).Append(',').Append(type).Append('\n');
        }
        return csv.ToString();
    }

    private static string MakeValidManifestJson()
    {
        return MakeValidManifestJson(new string('3', 64), new string('4', 64));
    }

    private static string MakeValidManifestJson(string settingsHash, string csvHash)
    {
        const string source = "synthetic-fixture";
        const string sourceVersion = "1.0";
        string sourceHash = new string('1', 64);
        const string captureSchema = "synthetic-v1";
        const string timingDefinition = "frame-analysis-v1-draft";
        const string timingBasis = "DisplayedTime";
        const int processId = 4242;
        const string csvPath = "captures\\test.csv";
        const string session = "synthetic-session-1234";
        const int captureSeconds = 60;
        CaptureCommandIdentity captureIdentity = MakeCaptureIdentity(processId, csvPath, session);
        captureIdentity.PlannedTimedSeconds = captureSeconds;
        string policyHash = captureIdentity.ComputePolicyHash();
        string invocationHash = captureIdentity.ComputeInvocationHash();
        string context = MeasurementRunManifestV1Validator.ComputeComparisonContext(
            source, sourceVersion, sourceHash, captureSchema, timingDefinition, timingBasis,
            policyHash, "testgame.exe", "synthetic-build-1", "fixed-route-60-seconds",
            "DX12", "not-applicable", "1.0-draft", 30, captureSeconds, 3, 50m,
            "synthetic", "synthetic", "synthetic", "2560x1440", 240m, "on",
            "Balanced", settingsHash, "DXGI", "Hardware: Independent Flip", "off", "none");
        string runId = Guid.NewGuid().ToString("D");
        return "{" +
            "\"schemaVersion\":1," +
            "\"runId\":\"" + runId + "\"," +
            "\"createdUtc\":\"2026-08-08T02:30:00Z\"," +
            "\"capture\":{" +
                "\"source\":\"" + source + "\",\"sourceVersion\":\"" + sourceVersion + "\",\"sourceSha256\":\"" + sourceHash + "\"," +
                "\"captureSchemaVersion\":\"" + captureSchema + "\",\"timingDefinitionVersion\":\"" + timingDefinition + "\",\"timingBasis\":\"" + timingBasis + "\"," +
                "\"targetProcessId\":" + processId.ToString(CultureInfo.InvariantCulture) + ",\"targetSwapChainAddress\":\"0xABC\",\"sessionName\":\"" + session + "\"," +
                "\"noTrackInput\":true,\"capturePolicyHash\":\"" + policyHash + "\",\"captureInvocationHash\":\"" + invocationHash + "\"}," +
            "\"game\":{\"executableBaseName\":\"testgame.exe\",\"buildId\":\"synthetic-build-1\",\"scenarioId\":\"fixed-route-60-seconds\",\"graphicsApi\":\"DX12\",\"antiCheatStatus\":\"not-applicable\"}," +
            "\"protocol\":{\"protocolVersion\":\"1.0-draft\",\"comparisonContextId\":\"" + context + "\",\"warmupSeconds\":30,\"captureSeconds\":60,\"repetitionIndex\":1,\"plannedRepetitions\":3,\"captureSequenceIndex\":1,\"slowFrameThresholdMs\":50}," +
            "\"environment\":{\"windowsBuild\":\"synthetic\",\"gpu\":\"synthetic\",\"gpuDriver\":\"synthetic\",\"displayMode\":\"2560x1440\",\"refreshHz\":240,\"hags\":\"on\",\"powerPlan\":\"Balanced\",\"gameSettingsRelativePath\":\"settings\\\\test.json\",\"gameSettingsHash\":\"" + settingsHash + "\",\"presentRuntime\":\"DXGI\",\"presentMode\":\"Hardware: Independent Flip\",\"frameGenerationState\":\"off\",\"frameGenerationTechnology\":\"none\",\"notes\":\"Synthetic only.\"}," +
            "\"configuration\":{\"label\":\"baseline\",\"role\":\"baseline\",\"changedVariables\":[]}," +
            "\"evidence\":{\"csvRelativePath\":\"captures\\\\test.csv\",\"csvSha256\":\"" + csvHash + "\"}," +
            "\"limitations\":[\"Synthetic example.\"]}";
    }

    private static string MakeValidSettingsJson()
    {
        return "{" +
            "\"schemaVersion\":1," +
            "\"game\":{\"executableBaseName\":\"testgame.exe\",\"buildId\":\"synthetic-build-1\",\"graphicsApi\":\"DX12\"}," +
            "\"environment\":{\"displayMode\":\"2560x1440\",\"refreshHz\":240,\"frameGenerationState\":\"off\",\"frameGenerationTechnology\":\"none\",\"frameGenerationEvidenceSource\":\"verified-config\"}," +
            "\"settings\":[" +
                "{\"key\":\"frameGeneration\",\"value\":\"off\",\"source\":\"verified-config\"}," +
                "{\"key\":\"frameLimit\",\"value\":\"240\",\"source\":\"verified-config\"}," +
                "{\"key\":\"qualityPreset\",\"value\":\"High\",\"source\":\"verified-config\"}" +
            "]}";
    }

    private static EvidenceCaptureContractV1 MakeSyntheticCaptureContract()
    {
        return new EvidenceCaptureContractV1(
            "synthetic-fixture", "1.0", new string('1', 64), "synthetic-v1", "frame-analysis-v1-draft", "not-applicable",
            "testgame.exe", "synthetic-build-1", "fixed-route-60-seconds", "DX12", "DXGI", "Hardware: Independent Flip", "verified-config", MakeExpectedSettings());
    }

    private static IEnumerable<GameSettingEvidenceV1> MakeExpectedSettings()
    {
        return new[]
        {
            new GameSettingEvidenceV1("frameGeneration", "off", "verified-config"),
            new GameSettingEvidenceV1("frameLimit", "240", "verified-config"),
            new GameSettingEvidenceV1("qualityPreset", "High", "verified-config")
        };
    }

    private static bool ThrowsSettingsInvalid(string json)
    {
        try { GameSettingsSnapshotV1Validator.ParseForTests(json); return false; }
        catch (InvalidDataException) { return true; }
    }

    private static string Sha256Text(string value)
    {
        using (SHA256 algorithm = SHA256.Create())
        {
            byte[] hash = algorithm.ComputeHash(new UTF8Encoding(false).GetBytes(value));
            StringBuilder result = new StringBuilder(hash.Length * 2);
            foreach (byte item in hash) result.Append(item.ToString("X2", CultureInfo.InvariantCulture));
            return result.ToString();
        }
    }

    private static bool ThrowsInvalidData(string json)
    {
        try { MeasurementRunManifestV1Validator.ParseForTests(json); return false; }
        catch (InvalidDataException) { return true; }
    }

    private static string ExtractJsonString(string json, string propertyName)
    {
        string marker = "\"" + propertyName + "\":\"";
        int start = json.IndexOf(marker, StringComparison.Ordinal);
        if (start < 0) throw new InvalidOperationException("Test JSON property was not found.");
        start += marker.Length;
        int end = json.IndexOf('"', start);
        if (end < 0) throw new InvalidOperationException("Test JSON string was not terminated.");
        return json.Substring(start, end - start);
    }

    private static string CsvHeader(string timingColumn, bool includeFrameType)
    {
        return "Application,ProcessID,SwapChainAddress,PresentRuntime,PresentMode," + timingColumn + (includeFrameType ? ",FrameType" : string.Empty) + "\n";
    }

    private static string ReplaceFirst(string value, string oldValue, string newValue)
    {
        int index = value.IndexOf(oldValue, StringComparison.Ordinal);
        if (index < 0) throw new InvalidOperationException("Synthetic token was not found.");
        return value.Substring(0, index) + newValue + value.Substring(index + oldValue.Length);
    }

    private static string MakeFlatArrayJson(int count)
    {
        StringBuilder json = new StringBuilder(count * 2 + 2).Append('[');
        for (int index = 0; index < count; index++)
        {
            if (index > 0) json.Append(',');
            json.Append('0');
        }
        return json.Append(']').ToString();
    }

    private static string MakeObjectJson(int count)
    {
        StringBuilder json = new StringBuilder(count * 10 + 2).Append('{');
        for (int index = 0; index < count; index++)
        {
            if (index > 0) json.Append(',');
            json.Append("\"p").Append(index.ToString(CultureInfo.InvariantCulture)).Append("\":0");
        }
        return json.Append('}').ToString();
    }

    private static string MakeNodeBoundaryJson(bool excessive)
    {
        StringBuilder json = new StringBuilder(110000).Append('[');
        for (int index = 0; index < 10000; index++)
        {
            if (index > 0) json.Append(',');
            int scalarCount = index == 9999 && !excessive ? 3 : 4;
            json.Append('[');
            for (int scalar = 0; scalar < scalarCount; scalar++)
            {
                if (scalar > 0) json.Append(',');
                json.Append('0');
            }
            json.Append(']');
        }
        return json.Append(']').ToString();
    }

    private static void Check(string name, bool condition)
    {
        if (condition) return;
        failures++;
        Console.WriteLine("FAILED: " + name);
    }

    private static void CheckKnownAnswer(string name, string expected, string actual)
    {
        if (string.Equals(expected, actual, StringComparison.Ordinal)) return;
        failures++;
        Console.WriteLine("FAILED: " + name + " expected=" + expected + " actual=" + actual);
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CreateHardLink(string fileName, string existingFileName, IntPtr securityAttributes);
}
