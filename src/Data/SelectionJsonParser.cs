using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace XingShangMod.Data
{
    internal sealed class SelectionJsonParser
    {
        private readonly string _text;
        private int _position;

        private SelectionJsonParser(string text)
        {
            _text = text;
        }

        public static SelectionCatalogDto Parse(string text)
        {
            var parser = new SelectionJsonParser(text);
            SelectionCatalogDto result = parser.ParseCatalog();
            parser.SkipWhitespace();
            if (parser._position != parser._text.Length)
                throw new FormatException("Trailing data after selection catalog JSON.");
            return result;
        }

        private SelectionCatalogDto ParseCatalog()
        {
            var result = new SelectionCatalogDto();
            ReadObjectMembers(delegate(string key)
            {
                switch (key)
                {
                    case "schema_version": result.schema_version = ReadInt32(); break;
                    case "schemaVersion": result.schemaVersion = ReadInt32(); break;
                    case "securities": result.securities = ReadEntries(); break;
                    case "stocks": result.stocks = ReadEntries(); break;
                    case "indices": result.indices = ReadEntries(); break;
                    default: SkipValue(0); break;
                }
            });
            return result;
        }

        private SelectionEntryDto[] ReadEntries()
        {
            var result = new List<SelectionEntryDto>();
            SkipWhitespace();
            Expect('[');
            SkipWhitespace();
            if (Consume(']'))
                return result.ToArray();
            while (true)
            {
                result.Add(ReadEntry());
                SkipWhitespace();
                if (Consume(']'))
                    return result.ToArray();
                Expect(',');
            }
        }

        private SelectionEntryDto ReadEntry()
        {
            var result = new SelectionEntryDto();
            ReadObjectMembers(delegate(string key)
            {
                switch (key)
                {
                    case "security_id": result.security_id = ReadNullableString(); break;
                    case "securityId": result.securityId = ReadNullableString(); break;
                    case "index_id": result.index_id = ReadNullableString(); break;
                    case "indexId": result.indexId = ReadNullableString(); break;
                    case "name": result.name = ReadNullableString(); break;
                    case "path": result.path = ReadNullableString(); break;
                    case "relative_path": result.relative_path = ReadNullableString(); break;
                    case "status": result.status = ReadNullableString(); break;
                    case "row_count": result.row_count = ReadInt32(); break;
                    case "rowCount": result.rowCount = ReadInt32(); break;
                    case "first_week": result.first_week = ReadInt32(); break;
                    case "firstWeek": result.firstWeek = ReadInt32(); break;
                    case "last_week": result.last_week = ReadInt32(); break;
                    case "lastWeek": result.lastWeek = ReadInt32(); break;
                    case "observed_rows": result.observed_rows = ReadInt32(); break;
                    case "observedRows": result.observedRows = ReadInt32(); break;
                    case "no_trade_rows": result.no_trade_rows = ReadInt32(); break;
                    case "noTradeRows": result.noTradeRows = ReadInt32(); break;
                    case "source_missing_rows": result.source_missing_rows = ReadInt32(); break;
                    case "sourceMissingRows": result.sourceMissingRows = ReadInt32(); break;
                    default: SkipValue(0); break;
                }
            });
            return result;
        }

        private void ReadObjectMembers(Action<string> readValue)
        {
            var keys = new HashSet<string>(StringComparer.Ordinal);
            SkipWhitespace();
            Expect('{');
            SkipWhitespace();
            if (Consume('}'))
                return;
            while (true)
            {
                SkipWhitespace();
                string key = ReadString();
                if (!keys.Add(key))
                    throw new FormatException("Duplicate selection catalog field: " + key);
                SkipWhitespace();
                Expect(':');
                SkipWhitespace();
                readValue(key);
                SkipWhitespace();
                if (Consume('}'))
                    return;
                Expect(',');
            }
        }

        private int ReadInt32()
        {
            long value = ReadInteger();
            if (value < int.MinValue || value > int.MaxValue)
                throw new FormatException("Selection catalog integer is outside Int32 range.");
            return (int)value;
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
                throw new FormatException("Selection catalog integer cannot contain a fraction or exponent.");
            return long.Parse(
                _text.Substring(start, _position - start),
                NumberStyles.AllowLeadingSign,
                CultureInfo.InvariantCulture);
        }

        private string ReadNullableString()
        {
            if (ConsumeLiteral("null"))
                return null;
            return ReadString();
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
                throw new FormatException("Selection catalog JSON is too deeply nested.");
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
                throw new FormatException("Expected '" + expected + "' in selection catalog JSON.");
        }

        private static bool IsDigit(char value)
        {
            return value >= '0' && value <= '9';
        }
    }
}
