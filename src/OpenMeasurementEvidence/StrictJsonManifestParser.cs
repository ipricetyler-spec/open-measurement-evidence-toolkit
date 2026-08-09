using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace OpenMeasurementEvidence
{
    internal enum StrictJsonKind
    {
        Object,
        Array,
        String,
        Number,
        Boolean,
        Null
    }

    internal abstract class StrictJsonValue
    {
        internal abstract StrictJsonKind Kind { get; }
    }

    internal sealed class StrictJsonObject : StrictJsonValue
    {
        private readonly Dictionary<string, StrictJsonValue> properties;
        internal override StrictJsonKind Kind { get { return StrictJsonKind.Object; } }
        internal IEnumerable<string> PropertyNames { get { return properties.Keys; } }
        internal int Count { get { return properties.Count; } }

        internal StrictJsonObject(Dictionary<string, StrictJsonValue> properties)
        {
            this.properties = properties;
        }

        internal bool TryGetValue(string name, out StrictJsonValue value)
        {
            return properties.TryGetValue(name, out value);
        }

        internal StrictJsonValue GetRequired(string name)
        {
            StrictJsonValue value;
            if (!properties.TryGetValue(name, out value)) throw new InvalidDataException("Required JSON property is missing: " + name);
            return value;
        }
    }

    internal sealed class StrictJsonArray : StrictJsonValue
    {
        internal readonly IList<StrictJsonValue> Values;
        internal override StrictJsonKind Kind { get { return StrictJsonKind.Array; } }
        internal StrictJsonArray(IList<StrictJsonValue> values) { Values = values; }
    }

    internal sealed class StrictJsonString : StrictJsonValue
    {
        internal readonly string Value;
        internal override StrictJsonKind Kind { get { return StrictJsonKind.String; } }
        internal StrictJsonString(string value) { Value = value; }
    }

    internal sealed class StrictJsonNumber : StrictJsonValue
    {
        internal readonly string Raw;
        internal readonly decimal Value;
        internal override StrictJsonKind Kind { get { return StrictJsonKind.Number; } }
        internal StrictJsonNumber(string raw, decimal value) { Raw = raw; Value = value; }
    }

    internal sealed class StrictJsonBoolean : StrictJsonValue
    {
        internal readonly bool Value;
        internal override StrictJsonKind Kind { get { return StrictJsonKind.Boolean; } }
        internal StrictJsonBoolean(bool value) { Value = value; }
    }

    internal sealed class StrictJsonNull : StrictJsonValue
    {
        internal static readonly StrictJsonNull Instance = new StrictJsonNull();
        internal override StrictJsonKind Kind { get { return StrictJsonKind.Null; } }
        private StrictJsonNull() { }
    }

    internal static class StrictJsonManifestParser
    {
        private const int MaximumDepth = 16;
        private const int MaximumNodes = 50000;
        private const int MaximumObjectProperties = 1000;
        private const int MaximumArrayItems = 10000;
        private const int MaximumDecodedStringCharacters = 65536;
        private const int MaximumNumberCharacters = 128;

        internal static StrictJsonObject ParseVerifiedManifest(EvidencePathPolicy.VerifiedEvidenceFile evidence)
        {
            return ParseVerifiedObject(evidence, EvidencePathPolicy.EvidenceFileKind.ManifestJson, "run manifest");
        }

        internal static StrictJsonObject ParseVerifiedSettings(EvidencePathPolicy.VerifiedEvidenceFile evidence)
        {
            return ParseVerifiedObject(evidence, EvidencePathPolicy.EvidenceFileKind.SettingsJson, "settings snapshot");
        }

        private static StrictJsonObject ParseVerifiedObject(EvidencePathPolicy.VerifiedEvidenceFile evidence, EvidencePathPolicy.EvidenceFileKind requiredKind, string label)
        {
            if (evidence == null) throw new ArgumentNullException("evidence");
            if (evidence.Kind != requiredKind)
                throw new InvalidDataException("The verified evidence kind does not match the expected " + label + " kind.");
            using (StreamReader reader = evidence.CreateStrictUtf8Reader())
            {
                StrictJsonValue root = new Parser(reader.ReadToEnd()).Parse();
                StrictJsonObject result = root as StrictJsonObject;
                if (result == null) throw new InvalidDataException("The " + label + " root must be a JSON object.");
                return result;
            }
        }

#if MEASUREMENT_TESTS
        internal static StrictJsonValue ParseForTests(string json)
        {
            return new Parser(json).Parse();
        }
#endif

        private sealed class Parser
        {
            private readonly string text;
            private int index;
            private int nodeCount;

            internal Parser(string text)
            {
                if (text == null) throw new ArgumentNullException("text");
                this.text = text;
            }

            internal StrictJsonValue Parse()
            {
                SkipWhitespace();
                if (index == text.Length) throw Error("JSON input is empty.");
                StrictJsonValue result = ParseValue(0);
                SkipWhitespace();
                if (index != text.Length) throw Error("JSON contains trailing data.");
                return result;
            }

            private StrictJsonValue ParseValue(int depth)
            {
                if (depth > MaximumDepth) throw Error("JSON exceeds the maximum nesting depth.");
                if (++nodeCount > MaximumNodes) throw Error("JSON exceeds the maximum node count.");
                if (index >= text.Length) throw Error("JSON ended before a value was complete.");
                char current = text[index];
                if ((current == '{' || current == '[') && depth >= MaximumDepth)
                    throw Error("JSON exceeds the maximum container nesting depth.");
                if (current == '{') return ParseObject(depth);
                if (current == '[') return ParseArray(depth);
                if (current == '"') return new StrictJsonString(ParseString());
                if (current == 't') { ReadLiteral("true"); return new StrictJsonBoolean(true); }
                if (current == 'f') { ReadLiteral("false"); return new StrictJsonBoolean(false); }
                if (current == 'n') { ReadLiteral("null"); return StrictJsonNull.Instance; }
                if (current == '-' || (current >= '0' && current <= '9')) return ParseNumber();
                throw Error("JSON contains an unsupported token.");
            }

            private StrictJsonObject ParseObject(int depth)
            {
                index++;
                SkipWhitespace();
                Dictionary<string, StrictJsonValue> properties = new Dictionary<string, StrictJsonValue>(StringComparer.Ordinal);
                if (TryConsume('}')) return new StrictJsonObject(properties);
                while (true)
                {
                    if (index >= text.Length || text[index] != '"') throw Error("JSON object property name must be a string.");
                    string name = ParseString();
                    if (properties.ContainsKey(name)) throw Error("JSON object contains a duplicate property.");
                    SkipWhitespace();
                    Require(':');
                    SkipWhitespace();
                    properties.Add(name, ParseValue(depth + 1));
                    if (properties.Count > MaximumObjectProperties) throw Error("JSON object exceeds the property limit.");
                    SkipWhitespace();
                    if (TryConsume('}')) break;
                    Require(',');
                    SkipWhitespace();
                }
                return new StrictJsonObject(properties);
            }

            private StrictJsonArray ParseArray(int depth)
            {
                index++;
                SkipWhitespace();
                List<StrictJsonValue> values = new List<StrictJsonValue>();
                if (TryConsume(']')) return new StrictJsonArray(values);
                while (true)
                {
                    values.Add(ParseValue(depth + 1));
                    if (values.Count > MaximumArrayItems) throw Error("JSON array exceeds the item limit.");
                    SkipWhitespace();
                    if (TryConsume(']')) break;
                    Require(',');
                    SkipWhitespace();
                }
                return new StrictJsonArray(values);
            }

            private string ParseString()
            {
                Require('"');
                StringBuilder value = new StringBuilder();
                while (index < text.Length)
                {
                    char current = text[index++];
                    if (current == '"') return value.ToString();
                    if (current < 0x20) throw Error("JSON string contains an unescaped control character.");
                    if (current == '\\')
                    {
                        if (index >= text.Length) throw Error("JSON string ends inside an escape sequence.");
                        char escape = text[index++];
                        switch (escape)
                        {
                            case '"': value.Append('"'); break;
                            case '\\': value.Append('\\'); break;
                            case '/': value.Append('/'); break;
                            case 'b': value.Append('\b'); break;
                            case 'f': value.Append('\f'); break;
                            case 'n': value.Append('\n'); break;
                            case 'r': value.Append('\r'); break;
                            case 't': value.Append('\t'); break;
                            case 'u': AppendEscapedUnicode(value); break;
                            default: throw Error("JSON string contains an unsupported escape sequence.");
                        }
                    }
                    else
                    {
                        if (char.IsHighSurrogate(current))
                        {
                            if (index >= text.Length || !char.IsLowSurrogate(text[index]))
                                throw Error("JSON string contains an unpaired high surrogate.");
                            value.Append(current);
                            value.Append(text[index++]);
                        }
                        else
                        {
                            if (char.IsLowSurrogate(current)) throw Error("JSON string contains an unpaired low surrogate.");
                            value.Append(current);
                        }
                    }
                    if (value.Length > MaximumDecodedStringCharacters) throw Error("JSON string exceeds the decoded length limit.");
                }
                throw Error("JSON string is unterminated.");
            }

            private void AppendEscapedUnicode(StringBuilder value)
            {
                char first = (char)ReadHexQuad();
                if (char.IsHighSurrogate(first))
                {
                    if (index + 6 > text.Length || text[index] != '\\' || text[index + 1] != 'u')
                        throw Error("JSON high surrogate is not followed by an escaped low surrogate.");
                    index += 2;
                    char second = (char)ReadHexQuad();
                    if (!char.IsLowSurrogate(second)) throw Error("JSON surrogate pair is invalid.");
                    value.Append(first);
                    value.Append(second);
                    return;
                }
                if (char.IsLowSurrogate(first)) throw Error("JSON contains an unpaired low surrogate.");
                value.Append(first);
            }

            private int ReadHexQuad()
            {
                if (index + 4 > text.Length) throw Error("JSON Unicode escape is incomplete.");
                int result = 0;
                for (int count = 0; count < 4; count++)
                {
                    char value = text[index++];
                    int digit;
                    if (value >= '0' && value <= '9') digit = value - '0';
                    else if (value >= 'A' && value <= 'F') digit = value - 'A' + 10;
                    else if (value >= 'a' && value <= 'f') digit = value - 'a' + 10;
                    else throw Error("JSON Unicode escape contains a non-hexadecimal character.");
                    result = (result << 4) | digit;
                }
                return result;
            }

            private StrictJsonNumber ParseNumber()
            {
                int start = index;
                if (TryConsume('-') && index >= text.Length) throw Error("JSON number is incomplete.");
                if (TryConsume('0'))
                {
                    if (index < text.Length && char.IsDigit(text[index])) throw Error("JSON number contains a leading zero.");
                }
                else
                {
                    if (index >= text.Length || text[index] < '1' || text[index] > '9') throw Error("JSON number integer part is invalid.");
                    while (index < text.Length && text[index] >= '0' && text[index] <= '9') index++;
                }
                if (TryConsume('.'))
                {
                    int fractionStart = index;
                    while (index < text.Length && text[index] >= '0' && text[index] <= '9') index++;
                    if (index == fractionStart) throw Error("JSON number fraction is incomplete.");
                }
                if (index < text.Length && (text[index] == 'e' || text[index] == 'E'))
                {
                    index++;
                    if (index < text.Length && (text[index] == '+' || text[index] == '-')) index++;
                    int exponentStart = index;
                    while (index < text.Length && text[index] >= '0' && text[index] <= '9') index++;
                    if (index == exponentStart) throw Error("JSON number exponent is incomplete.");
                }
                int length = index - start;
                if (length > MaximumNumberCharacters) throw Error("JSON number exceeds the supported length.");
                string raw = text.Substring(start, length);
                decimal value;
                if (!decimal.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out value))
                    throw Error("JSON number is outside the supported finite decimal range.");
                string rawIdentity;
                string decimalIdentity;
                if (!TryCanonicalizeExactNumber(raw, out rawIdentity) || !TryCanonicalizeExactNumber(value.ToString("G29", CultureInfo.InvariantCulture), out decimalIdentity) || rawIdentity != decimalIdentity)
                    throw Error("JSON number cannot be represented exactly by the supported decimal model.");
                return new StrictJsonNumber(raw, value);
            }

            private static bool TryCanonicalizeExactNumber(string raw, out string identity)
            {
                identity = null;
                int position = 0;
                bool negative = false;
                if (position < raw.Length && raw[position] == '-') { negative = true; position++; }
                int exponentMarker = raw.IndexOfAny(new[] { 'e', 'E' }, position);
                string mantissa = exponentMarker < 0 ? raw.Substring(position) : raw.Substring(position, exponentMarker - position);
                long explicitExponent = 0;
                if (exponentMarker >= 0 && !long.TryParse(raw.Substring(exponentMarker + 1), NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out explicitExponent)) return false;
                int decimalPoint = mantissa.IndexOf('.');
                int fractionalDigits = decimalPoint < 0 ? 0 : mantissa.Length - decimalPoint - 1;
                string digits = decimalPoint < 0 ? mantissa : mantissa.Remove(decimalPoint, 1);
                int firstNonZero = 0;
                while (firstNonZero < digits.Length && digits[firstNonZero] == '0') firstNonZero++;
                if (firstNonZero == digits.Length) { identity = "0"; return true; }
                digits = digits.Substring(firstNonZero);
                int trailingZeros = 0;
                while (trailingZeros < digits.Length && digits[digits.Length - 1 - trailingZeros] == '0') trailingZeros++;
                if (trailingZeros > 0) digits = digits.Substring(0, digits.Length - trailingZeros);
                long exponent;
                try { exponent = checked(explicitExponent - fractionalDigits + trailingZeros); }
                catch (OverflowException) { return false; }
                identity = (negative ? "-" : "+") + digits + "e" + exponent.ToString(CultureInfo.InvariantCulture);
                return true;
            }

            private void ReadLiteral(string literal)
            {
                if (index + literal.Length > text.Length || !string.Equals(text.Substring(index, literal.Length), literal, StringComparison.Ordinal))
                    throw Error("JSON literal is invalid.");
                index += literal.Length;
            }

            private void SkipWhitespace()
            {
                while (index < text.Length)
                {
                    char value = text[index];
                    if (value != ' ' && value != '\t' && value != '\r' && value != '\n') break;
                    index++;
                }
            }

            private bool TryConsume(char expected)
            {
                if (index >= text.Length || text[index] != expected) return false;
                index++;
                return true;
            }

            private void Require(char expected)
            {
                if (!TryConsume(expected)) throw Error("JSON expected '" + expected + "'.");
            }

            private InvalidDataException Error(string message)
            {
                return new InvalidDataException(message + " Character offset: " + index.ToString(CultureInfo.InvariantCulture) + ".");
            }
        }
    }
}
