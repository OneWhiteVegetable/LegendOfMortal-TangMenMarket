using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Text;

namespace XingShangMod.Data
{
    public static class WeeklyFileReader
    {
        private static readonly byte[] Magic = { (byte)'S', (byte)'W', (byte)'K', (byte)'1' };

        public static List<WeeklyRecord> Read(string path, out SecurityHeader header)
        {
            header = null;
            using (var stream = File.OpenRead(path))
            using (var gzip = new GZipStream(stream, CompressionMode.Decompress))
            using (var reader = new BinaryReader(gzip, new UTF8Encoding(false, true)))
            {
                byte[] magic = reader.ReadBytes(4);
                if (magic.Length != 4)
                    throw new EndOfStreamException("Truncated SWK1 magic.");
                for (int i = 0; i < Magic.Length; i++)
                {
                    if (magic[i] != Magic[i])
                        throw new FormatException("Invalid SWK1 magic.");
                }

                uint headerLength = reader.ReadUInt32();
                if (headerLength > int.MaxValue)
                    throw new FormatException("Header JSON is too large.");
                byte[] headerBytes = reader.ReadBytes((int)headerLength);
                if (headerBytes.Length != (int)headerLength)
                    throw new EndOfStreamException("Truncated header JSON.");
                SecurityHeader parsedHeader = HeaderJsonParser.Parse(
                    new UTF8Encoding(false, true).GetString(headerBytes));
                parsedHeader.Validate();

                var records = new List<WeeklyRecord>(parsedHeader.RowCount);
                for (int i = 0; i < parsedHeader.RowCount; i++)
                {
                    var record = new WeeklyRecord
                    {
                        WeekMondayDays = reader.ReadInt32(),
                        CloseDateDays = reader.ReadInt32(),
                        RawCloseTicks = reader.ReadInt64(),
                        HfqFactor = reader.ReadDouble(),
                        Flags = reader.ReadUInt16(),
                        Status = reader.ReadByte(),
                        Reserved = reader.ReadByte()
                    };
                    try
                    {
                        record.Validate();
                    }
                    catch (FormatException exception)
                    {
                        throw new FormatException("Invalid record " + i + ": " + exception.Message, exception);
                    }
                    if (records.Count > 0 && record.WeekMondayDays <= records[records.Count - 1].WeekMondayDays)
                        throw new FormatException("Records must be sorted by strictly increasing WeekMondayDays.");
                    records.Add(record);
                }

                if (parsedHeader.RowCount > 0)
                {
                    if (parsedHeader.FirstWeek.Value != records[0].WeekMondayDays)
                        throw new FormatException("FirstWeek does not match the first record.");
                    if (parsedHeader.LastWeek.Value != records[records.Count - 1].WeekMondayDays)
                        throw new FormatException("LastWeek does not match the last record.");
                }
                if (reader.ReadBytes(1).Length != 0)
                    throw new FormatException("Trailing payload data after the declared records.");

                header = parsedHeader;
                return records;
            }
        }

        private sealed class HeaderJsonParser
        {
            private static readonly HashSet<string> StringFields = new HashSet<string>
            {
                "securityId", "code", "currency", "calendarId", "source", "adjustmentAlgorithm"
            };
            private static readonly HashSet<string> IntegerFields = new HashSet<string>
            {
                "schemaVersion", "recordSize", "rawScale", "rowCount"
            };
            private static readonly HashSet<string> WeekFields = new HashSet<string>
            {
                "firstWeek", "lastWeek"
            };

            private readonly string _text;
            private int _position;

            private HeaderJsonParser(string text)
            {
                _text = text;
            }

            public static SecurityHeader Parse(string text)
            {
                return new HeaderJsonParser(text).ParseHeader();
            }

            private SecurityHeader ParseHeader()
            {
                var values = new Dictionary<string, object>();
                SkipWhitespace();
                Expect('{');
                SkipWhitespace();
                if (!Consume('}'))
                {
                    while (true)
                    {
                        SkipWhitespace();
                        string key = ReadString();
                        if (values.ContainsKey(key))
                            throw new FormatException("Duplicate header field: " + key);
                        SkipWhitespace();
                        Expect(':');
                        SkipWhitespace();
                        if (StringFields.Contains(key))
                            values.Add(key, ReadString());
                        else if (IntegerFields.Contains(key))
                            values.Add(key, ReadInteger());
                        else if (WeekFields.Contains(key))
                            values.Add(key, ReadNullableInteger());
                        else
                        {
                            SkipValue(0);
                            values.Add(key, null);
                        }
                        SkipWhitespace();
                        if (Consume('}'))
                            break;
                        Expect(',');
                    }
                }
                SkipWhitespace();
                if (_position != _text.Length)
                    throw new FormatException("Trailing data after header JSON.");

                var header = new SecurityHeader
                {
                    SchemaVersion = GetInt32(values, "schemaVersion"),
                    RecordSize = GetInt32(values, "recordSize"),
                    SecurityId = GetString(values, "securityId"),
                    Code = GetString(values, "code"),
                    Currency = GetString(values, "currency"),
                    RawScale = GetInt32(values, "rawScale"),
                    CalendarId = GetString(values, "calendarId"),
                    RowCount = GetInt32(values, "rowCount"),
                    FirstWeek = GetNullableInt32(values, "firstWeek"),
                    LastWeek = GetNullableInt32(values, "lastWeek"),
                    Source = GetString(values, "source"),
                    AdjustmentAlgorithm = GetString(values, "adjustmentAlgorithm")
                };
                return header;
            }

            private static string GetString(Dictionary<string, object> values, string key)
            {
                object value = GetRequired(values, key);
                if (!(value is string))
                    throw new FormatException(key + " must be a string.");
                return (string)value;
            }

            private static int GetInt32(Dictionary<string, object> values, string key)
            {
                object value = GetRequired(values, key);
                if (!(value is long))
                    throw new FormatException(key + " must be an integer.");
                long number = (long)value;
                if (number < int.MinValue || number > int.MaxValue)
                    throw new FormatException(key + " is outside Int32 range.");
                return (int)number;
            }

            private static int? GetNullableInt32(Dictionary<string, object> values, string key)
            {
                object value = GetRequired(values, key);
                if (value == null)
                    return null;
                return GetInt32(values, key);
            }

            private static object GetRequired(Dictionary<string, object> values, string key)
            {
                object value;
                if (!values.TryGetValue(key, out value))
                    throw new FormatException("Missing header field: " + key);
                return value;
            }

            private object ReadNullableInteger()
            {
                if (ConsumeLiteral("null"))
                    return null;
                return ReadInteger();
            }

            private long ReadInteger()
            {
                int start = _position;
                if (Consume('-') && _position == _text.Length)
                    throw new FormatException("Invalid JSON integer.");
                if (Consume('0'))
                {
                    if (_position < _text.Length && IsDigit(_text[_position]))
                        throw new FormatException("Leading zero in JSON integer.");
                }
                else
                {
                    ReadDigits(true);
                }
                if (_position < _text.Length && (_text[_position] == '.' || _text[_position] == 'e' || _text[_position] == 'E'))
                    throw new FormatException("Header integer cannot contain a fraction or exponent.");
                return long.Parse(_text.Substring(start, _position - start), NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture);
            }

            private string ReadString()
            {
                Expect('"');
                var result = new StringBuilder();
                while (_position < _text.Length)
                {
                    char current = _text[_position++];
                    if (current == '"')
                        return result.ToString();
                    if (current < ' ')
                        throw new FormatException("Control character in JSON string.");
                    if (current != '\\')
                    {
                        result.Append(current);
                        continue;
                    }
                    if (_position == _text.Length)
                        throw new FormatException("Truncated JSON escape.");
                    char escape = _text[_position++];
                    switch (escape)
                    {
                        case '"': result.Append('"'); break;
                        case '\\': result.Append('\\'); break;
                        case '/': result.Append('/'); break;
                        case 'b': result.Append('\b'); break;
                        case 'f': result.Append('\f'); break;
                        case 'n': result.Append('\n'); break;
                        case 'r': result.Append('\r'); break;
                        case 't': result.Append('\t'); break;
                        case 'u': result.Append(ReadUnicodeEscape()); break;
                        default: throw new FormatException("Invalid JSON escape.");
                    }
                }
                throw new FormatException("Unterminated JSON string.");
            }

            private char ReadUnicodeEscape()
            {
                if (_text.Length - _position < 4)
                    throw new FormatException("Truncated Unicode escape.");
                int value = 0;
                for (int i = 0; i < 4; i++)
                {
                    char current = _text[_position++];
                    int digit;
                    if (current >= '0' && current <= '9') digit = current - '0';
                    else if (current >= 'a' && current <= 'f') digit = current - 'a' + 10;
                    else if (current >= 'A' && current <= 'F') digit = current - 'A' + 10;
                    else throw new FormatException("Invalid Unicode escape.");
                    value = value * 16 + digit;
                }
                return (char)value;
            }

            private void SkipValue(int depth)
            {
                if (depth > 64)
                    throw new FormatException("Header JSON is too deeply nested.");
                SkipWhitespace();
                if (_position == _text.Length)
                    throw new FormatException("Missing JSON value.");
                char current = _text[_position];
                if (current == '"')
                {
                    ReadString();
                    return;
                }
                if (current == '{')
                {
                    _position++;
                    SkipWhitespace();
                    if (Consume('}')) return;
                    while (true)
                    {
                        SkipWhitespace();
                        ReadString();
                        SkipWhitespace();
                        Expect(':');
                        SkipValue(depth + 1);
                        SkipWhitespace();
                        if (Consume('}')) return;
                        Expect(',');
                    }
                }
                if (current == '[')
                {
                    _position++;
                    SkipWhitespace();
                    if (Consume(']')) return;
                    while (true)
                    {
                        SkipValue(depth + 1);
                        SkipWhitespace();
                        if (Consume(']')) return;
                        Expect(',');
                    }
                }
                if (ConsumeLiteral("true") || ConsumeLiteral("false") || ConsumeLiteral("null"))
                    return;
                SkipNumber();
            }

            private void SkipNumber()
            {
                Consume('-');
                if (Consume('0'))
                {
                    if (_position < _text.Length && IsDigit(_text[_position]))
                        throw new FormatException("Leading zero in JSON number.");
                }
                else
                {
                    ReadDigits(true);
                }
                if (Consume('.'))
                    ReadDigits(true);
                if (Consume('e') || Consume('E'))
                {
                    Consume('+');
                    Consume('-');
                    ReadDigits(true);
                }
            }

            private void ReadDigits(bool requireDigit)
            {
                int start = _position;
                while (_position < _text.Length && IsDigit(_text[_position]))
                    _position++;
                if (requireDigit && start == _position)
                    throw new FormatException("Expected a JSON digit.");
            }

            private bool ConsumeLiteral(string literal)
            {
                if (_text.Length - _position < literal.Length)
                    return false;
                if (string.CompareOrdinal(_text, _position, literal, 0, literal.Length) != 0)
                    return false;
                _position += literal.Length;
                return true;
            }

            private void SkipWhitespace()
            {
                while (_position < _text.Length)
                {
                    char current = _text[_position];
                    if (current != ' ' && current != '\t' && current != '\r' && current != '\n')
                        return;
                    _position++;
                }
            }

            private bool Consume(char expected)
            {
                if (_position < _text.Length && _text[_position] == expected)
                {
                    _position++;
                    return true;
                }
                return false;
            }

            private void Expect(char expected)
            {
                if (!Consume(expected))
                    throw new FormatException("Expected '" + expected + "' in header JSON.");
            }

            private static bool IsDigit(char value)
            {
                return value >= '0' && value <= '9';
            }
        }
    }
}
