using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Reflection;
using System.Text;

namespace XingShangMod.Data
{
    public static class JsonCodec
    {
        private const int MaximumDepth = 128;
        private static readonly ConcurrentDictionary<Type, SerializableField[]> FieldCache =
            new ConcurrentDictionary<Type, SerializableField[]>();

        private sealed class SerializableField
        {
            internal FieldInfo Field;
            internal DefaultValueAttribute OmittedDefault;
        }

        public static T Deserialize<T>(string json)
        {
            return Deserialize<T>(json, false);
        }

        public static T Deserialize<T>(string json, bool ignorePropertyCase)
        {
            if (json == null)
                throw new ArgumentNullException("json");
            JsonValue value = JsonParser.Parse(json);
            return (T)ConvertValue(value, typeof(T), ignorePropertyCase, "$");
        }

        public static string Serialize<T>(T value, bool pretty)
        {
            return Serialize(value, typeof(T), pretty);
        }

        public static string Serialize(object value, Type declaredType, bool pretty)
        {
            if (declaredType == null)
                throw new ArgumentNullException("declaredType");
            var result = new StringBuilder();
            WriteValue(result, value, declaredType, pretty, 0);
            return result.ToString();
        }

        public static T DeepClone<T>(T source)
        {
            return Deserialize<T>(Serialize(source, false));
        }

        private static object ConvertValue(
            JsonValue value,
            Type targetType,
            bool ignorePropertyCase,
            string path)
        {
            Type nullableType = Nullable.GetUnderlyingType(targetType);
            if (value.Kind == JsonKind.Null)
            {
                if (!targetType.IsValueType || nullableType != null)
                    return null;
                throw TypeMismatch(path, targetType);
            }
            if (nullableType != null)
                targetType = nullableType;

            if (targetType == typeof(string))
            {
                RequireKind(value, JsonKind.String, path, targetType);
                return value.Value;
            }
            if (targetType == typeof(char))
            {
                RequireKind(value, JsonKind.String, path, targetType);
                string text = (string)value.Value;
                if (text.Length != 1)
                    throw new FormatException(path + " must contain exactly one character.");
                return text[0];
            }
            if (targetType == typeof(bool))
            {
                RequireKind(value, JsonKind.Boolean, path, targetType);
                return value.Value;
            }
            if (targetType.IsEnum)
                return ConvertEnum(value, targetType, path);
            if (IsNumberType(targetType))
                return ConvertNumber(value, targetType, path);
            if (targetType.IsArray)
                return ConvertArray(value, targetType, ignorePropertyCase, path);

            Type listElementType = GetListElementType(targetType);
            if (listElementType != null)
                return ConvertList(value, targetType, listElementType, ignorePropertyCase, path);
            if (targetType.IsClass || targetType.IsValueType)
                return ConvertObject(value, targetType, ignorePropertyCase, path);
            throw new NotSupportedException("JSON target type is not supported: " + targetType.FullName);
        }

        private static object ConvertEnum(JsonValue value, Type targetType, string path)
        {
            if (value.Kind == JsonKind.String)
                return Enum.Parse(targetType, (string)value.Value, false);
            if (value.Kind == JsonKind.Number)
            {
                object number = ConvertNumber(value, Enum.GetUnderlyingType(targetType), path);
                return Enum.ToObject(targetType, number);
            }
            throw TypeMismatch(path, targetType);
        }

        private static object ConvertNumber(JsonValue value, Type targetType, string path)
        {
            RequireKind(value, JsonKind.Number, path, targetType);
            string text = (string)value.Value;
            try
            {
                switch (Type.GetTypeCode(targetType))
                {
                    case TypeCode.Byte:
                        return byte.Parse(text, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture);
                    case TypeCode.SByte:
                        return sbyte.Parse(text, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture);
                    case TypeCode.Int16:
                        return short.Parse(text, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture);
                    case TypeCode.UInt16:
                        return ushort.Parse(text, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture);
                    case TypeCode.Int32:
                        return int.Parse(text, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture);
                    case TypeCode.UInt32:
                        return uint.Parse(text, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture);
                    case TypeCode.Int64:
                        return long.Parse(text, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture);
                    case TypeCode.UInt64:
                        return ulong.Parse(text, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture);
                    case TypeCode.Single:
                    {
                        float number = float.Parse(text, NumberStyles.Float, CultureInfo.InvariantCulture);
                        if (float.IsNaN(number) || float.IsInfinity(number))
                            throw new FormatException(path + " must be a finite Single value.");
                        return number;
                    }
                    case TypeCode.Double:
                    {
                        double number = double.Parse(text, NumberStyles.Float, CultureInfo.InvariantCulture);
                        if (double.IsNaN(number) || double.IsInfinity(number))
                            throw new FormatException(path + " must be a finite Double value.");
                        return number;
                    }
                    case TypeCode.Decimal:
                        return decimal.Parse(text, NumberStyles.Float, CultureInfo.InvariantCulture);
                    default:
                        throw new NotSupportedException("JSON number target is not supported: " + targetType.FullName);
                }
            }
            catch (OverflowException exception)
            {
                throw new FormatException(path + " is outside the range of " + targetType.Name + ".", exception);
            }
        }

        private static Array ConvertArray(
            JsonValue value,
            Type targetType,
            bool ignorePropertyCase,
            string path)
        {
            RequireKind(value, JsonKind.Array, path, targetType);
            Type elementType = targetType.GetElementType();
            var items = (List<JsonValue>)value.Value;
            Array result = Array.CreateInstance(elementType, items.Count);
            for (int i = 0; i < items.Count; i++)
            {
                object item = ConvertValue(
                    items[i],
                    elementType,
                    ignorePropertyCase,
                    path + "[" + i.ToString(CultureInfo.InvariantCulture) + "]");
                result.SetValue(item, i);
            }
            return result;
        }

        private static object ConvertList(
            JsonValue value,
            Type targetType,
            Type elementType,
            bool ignorePropertyCase,
            string path)
        {
            RequireKind(value, JsonKind.Array, path, targetType);
            var items = (List<JsonValue>)value.Value;
            IList result = (IList)Activator.CreateInstance(targetType, true);
            for (int i = 0; i < items.Count; i++)
            {
                object item = ConvertValue(
                    items[i],
                    elementType,
                    ignorePropertyCase,
                    path + "[" + i.ToString(CultureInfo.InvariantCulture) + "]");
                result.Add(item);
            }
            return result;
        }

        private static object ConvertObject(
            JsonValue value,
            Type targetType,
            bool ignorePropertyCase,
            string path)
        {
            RequireKind(value, JsonKind.Object, path, targetType);
            if (targetType.IsAbstract || targetType.IsInterface)
                throw new NotSupportedException("Cannot construct JSON target type: " + targetType.FullName);

            object result = Activator.CreateInstance(targetType, true);
            SerializableField[] fields = FieldCache.GetOrAdd(targetType, DescribeFields);
            var comparer = ignorePropertyCase ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
            var fieldsByName = new Dictionary<string, FieldInfo>(comparer);
            for (int i = 0; i < fields.Length; i++)
            {
                FieldInfo field = fields[i].Field;
                if (!fieldsByName.ContainsKey(field.Name))
                    fieldsByName.Add(field.Name, field);
                else
                    throw new NotSupportedException("JSON target has ambiguous fields: " + targetType.FullName);
            }

            var assigned = new HashSet<string>(comparer);
            var members = (List<KeyValuePair<string, JsonValue>>)value.Value;
            for (int i = 0; i < members.Count; i++)
            {
                FieldInfo field;
                if (!fieldsByName.TryGetValue(members[i].Key, out field))
                    continue;
                if (!assigned.Add(field.Name))
                    throw new FormatException(path + " contains a duplicate field: " + field.Name);
                object fieldValue = ConvertValue(
                    members[i].Value,
                    field.FieldType,
                    ignorePropertyCase,
                    path + "." + field.Name);
                field.SetValue(result, fieldValue);
            }
            return result;
        }

        private static bool IsNumberType(Type type)
        {
            switch (Type.GetTypeCode(type))
            {
                case TypeCode.Byte:
                case TypeCode.SByte:
                case TypeCode.Int16:
                case TypeCode.UInt16:
                case TypeCode.Int32:
                case TypeCode.UInt32:
                case TypeCode.Int64:
                case TypeCode.UInt64:
                case TypeCode.Single:
                case TypeCode.Double:
                case TypeCode.Decimal:
                    return true;
                default:
                    return false;
            }
        }

        private static Type GetListElementType(Type type)
        {
            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>))
                return type.GetGenericArguments()[0];
            return null;
        }

        private static SerializableField[] DescribeFields(Type type)
        {
            FieldInfo[] fields = type.GetFields(BindingFlags.Instance | BindingFlags.Public);
            Array.Sort(fields, delegate(FieldInfo left, FieldInfo right)
            {
                return string.CompareOrdinal(left.Name, right.Name);
            });
            var result = new SerializableField[fields.Length];
            for (int i = 0; i < fields.Length; i++)
            {
                result[i] = new SerializableField
                {
                    Field = fields[i],
                    OmittedDefault = fields[i].GetCustomAttribute<DefaultValueAttribute>()
                };
            }
            return result;
        }

        private static void RequireKind(JsonValue value, JsonKind kind, string path, Type targetType)
        {
            if (value.Kind != kind)
                throw TypeMismatch(path, targetType);
        }

        private static FormatException TypeMismatch(string path, Type targetType)
        {
            return new FormatException(path + " cannot be converted to " + targetType.FullName + ".");
        }

        private static void WriteValue(
            StringBuilder result,
            object value,
            Type declaredType,
            bool pretty,
            int depth)
        {
            if (depth > MaximumDepth)
                throw new InvalidOperationException("JSON object graph is too deeply nested.");
            if (value == null)
            {
                result.Append("null");
                return;
            }

            Type nullableType = Nullable.GetUnderlyingType(declaredType);
            if (nullableType != null)
                declaredType = nullableType;
            if (declaredType == typeof(string))
            {
                WriteString(result, (string)value);
                return;
            }
            if (declaredType == typeof(char))
            {
                WriteString(result, value.ToString());
                return;
            }
            if (declaredType == typeof(bool))
            {
                result.Append((bool)value ? "true" : "false");
                return;
            }
            if (declaredType.IsEnum)
            {
                WriteString(result, value.ToString());
                return;
            }
            if (IsNumberType(declaredType))
            {
                WriteNumber(result, value, declaredType);
                return;
            }
            if (declaredType.IsArray)
            {
                WriteArray(result, (IList)value, declaredType.GetElementType(), pretty, depth);
                return;
            }

            Type listElementType = GetListElementType(declaredType);
            if (listElementType != null)
            {
                WriteArray(result, (IList)value, listElementType, pretty, depth);
                return;
            }
            if (declaredType.IsClass || declaredType.IsValueType)
            {
                WriteObject(result, value, declaredType, pretty, depth);
                return;
            }
            throw new NotSupportedException("JSON source type is not supported: " + declaredType.FullName);
        }

        private static void WriteNumber(StringBuilder result, object value, Type type)
        {
            string text;
            if (type == typeof(double))
            {
                double number = (double)value;
                if (double.IsNaN(number) || double.IsInfinity(number))
                    throw new InvalidOperationException("JSON cannot contain a non-finite Double value.");
                text = number.ToString("R", CultureInfo.InvariantCulture);
            }
            else if (type == typeof(float))
            {
                float number = (float)value;
                if (float.IsNaN(number) || float.IsInfinity(number))
                    throw new InvalidOperationException("JSON cannot contain a non-finite Single value.");
                text = number.ToString("R", CultureInfo.InvariantCulture);
            }
            else
            {
                text = ((IFormattable)value).ToString(null, CultureInfo.InvariantCulture);
            }
            result.Append(text);
        }

        private static void WriteObject(
            StringBuilder result,
            object value,
            Type declaredType,
            bool pretty,
            int depth)
        {
            SerializableField[] fields = FieldCache.GetOrAdd(declaredType, DescribeFields);
            result.Append('{');
            bool wroteField = false;
            for (int i = 0; i < fields.Length; i++)
            {
                FieldInfo field = fields[i].Field;
                object member = field.GetValue(value);
                DefaultValueAttribute defaultValue = fields[i].OmittedDefault;
                if (defaultValue != null && Equals(member, defaultValue.Value))
                    continue;
                if (wroteField)
                    result.Append(',');
                if (pretty)
                {
                    result.Append('\n');
                    AppendIndent(result, depth + 1);
                }
                WriteString(result, field.Name);
                result.Append(pretty ? ": " : ":");
                WriteValue(result, member, field.FieldType, pretty, depth + 1);
                wroteField = true;
            }
            if (wroteField && pretty)
            {
                result.Append('\n');
                AppendIndent(result, depth);
            }
            result.Append('}');
        }

        private static void WriteArray(
            StringBuilder result,
            IList values,
            Type elementType,
            bool pretty,
            int depth)
        {
            result.Append('[');
            if (values.Count > 0 && pretty)
                result.Append('\n');
            for (int i = 0; i < values.Count; i++)
            {
                if (pretty)
                    AppendIndent(result, depth + 1);
                WriteValue(result, values[i], elementType, pretty, depth + 1);
                if (i + 1 < values.Count)
                    result.Append(',');
                if (pretty)
                    result.Append('\n');
            }
            if (values.Count > 0 && pretty)
                AppendIndent(result, depth);
            result.Append(']');
        }

        private static void AppendIndent(StringBuilder result, int depth)
        {
            for (int i = 0; i < depth; i++)
                result.Append("  ");
        }

        private static void WriteString(StringBuilder result, string value)
        {
            result.Append('"');
            for (int i = 0; i < value.Length; i++)
            {
                char current = value[i];
                switch (current)
                {
                    case '"': result.Append("\\\""); break;
                    case '\\': result.Append("\\\\"); break;
                    case '\b': result.Append("\\b"); break;
                    case '\f': result.Append("\\f"); break;
                    case '\n': result.Append("\\n"); break;
                    case '\r': result.Append("\\r"); break;
                    case '\t': result.Append("\\t"); break;
                    default:
                        if (current < ' ' || char.IsSurrogate(current))
                        {
                            result.Append("\\u");
                            result.Append(((int)current).ToString("x4", CultureInfo.InvariantCulture));
                        }
                        else
                        {
                            result.Append(current);
                        }
                        break;
                }
            }
            result.Append('"');
        }

        private enum JsonKind
        {
            Null,
            Boolean,
            Number,
            String,
            Array,
            Object
        }

        private sealed class JsonValue
        {
            public JsonKind Kind;
            public object Value;
        }

        private sealed class JsonParser
        {
            private readonly string _text;
            private int _position;

            private JsonParser(string text)
            {
                _text = text;
            }

            public static JsonValue Parse(string text)
            {
                var parser = new JsonParser(text);
                if (parser._text.Length > 0 && parser._text[0] == '\ufeff')
                    parser._position++;
                parser.SkipWhitespace();
                JsonValue result = parser.ReadValue(0);
                parser.SkipWhitespace();
                if (parser._position != parser._text.Length)
                    throw parser.Error("Trailing data after JSON value");
                return result;
            }

            private JsonValue ReadValue(int depth)
            {
                if (depth > MaximumDepth)
                    throw Error("JSON value is too deeply nested");
                SkipWhitespace();
                if (_position == _text.Length)
                    throw Error("Missing JSON value");

                char current = _text[_position];
                if (current == '{')
                    return ReadObject(depth);
                if (current == '[')
                    return ReadArray(depth);
                if (current == '"')
                    return new JsonValue { Kind = JsonKind.String, Value = ReadString() };
                if (current == 't')
                {
                    ReadLiteral("true");
                    return new JsonValue { Kind = JsonKind.Boolean, Value = true };
                }
                if (current == 'f')
                {
                    ReadLiteral("false");
                    return new JsonValue { Kind = JsonKind.Boolean, Value = false };
                }
                if (current == 'n')
                {
                    ReadLiteral("null");
                    return new JsonValue { Kind = JsonKind.Null };
                }
                if (current == '-' || IsDigit(current))
                    return new JsonValue { Kind = JsonKind.Number, Value = ReadNumber() };
                throw Error("Unexpected character in JSON value");
            }

            private JsonValue ReadObject(int depth)
            {
                var members = new List<KeyValuePair<string, JsonValue>>();
                var names = new HashSet<string>(StringComparer.Ordinal);
                Expect('{');
                SkipWhitespace();
                if (Consume('}'))
                    return new JsonValue { Kind = JsonKind.Object, Value = members };

                while (true)
                {
                    SkipWhitespace();
                    string name = ReadString();
                    if (!names.Add(name))
                        throw Error("Duplicate JSON object field: " + name);
                    SkipWhitespace();
                    Expect(':');
                    JsonValue value = ReadValue(depth + 1);
                    members.Add(new KeyValuePair<string, JsonValue>(name, value));
                    SkipWhitespace();
                    if (Consume('}'))
                        return new JsonValue { Kind = JsonKind.Object, Value = members };
                    Expect(',');
                }
            }

            private JsonValue ReadArray(int depth)
            {
                var items = new List<JsonValue>();
                Expect('[');
                SkipWhitespace();
                if (Consume(']'))
                    return new JsonValue { Kind = JsonKind.Array, Value = items };

                while (true)
                {
                    items.Add(ReadValue(depth + 1));
                    SkipWhitespace();
                    if (Consume(']'))
                        return new JsonValue { Kind = JsonKind.Array, Value = items };
                    Expect(',');
                }
            }

            private string ReadNumber()
            {
                int start = _position;
                Consume('-');
                if (Consume('0'))
                {
                    if (_position < _text.Length && IsDigit(_text[_position]))
                        throw Error("Leading zero in JSON number");
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
                return _text.Substring(start, _position - start);
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
                        throw Error("Control character in JSON string");
                    if (current != '\\')
                    {
                        result.Append(current);
                        continue;
                    }
                    if (_position == _text.Length)
                        throw Error("Truncated JSON escape");
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
                        default: throw Error("Invalid JSON escape");
                    }
                }
                throw Error("Unterminated JSON string");
            }

            private char ReadUnicodeEscape()
            {
                if (_text.Length - _position < 4)
                    throw Error("Truncated Unicode escape");
                int value = 0;
                for (int i = 0; i < 4; i++)
                {
                    char current = _text[_position++];
                    int digit;
                    if (current >= '0' && current <= '9') digit = current - '0';
                    else if (current >= 'a' && current <= 'f') digit = current - 'a' + 10;
                    else if (current >= 'A' && current <= 'F') digit = current - 'A' + 10;
                    else throw Error("Invalid Unicode escape");
                    value = value * 16 + digit;
                }
                return (char)value;
            }

            private void ReadLiteral(string literal)
            {
                if (_text.Length - _position < literal.Length
                    || string.CompareOrdinal(_text, _position, literal, 0, literal.Length) != 0)
                    throw Error("Invalid JSON literal");
                _position += literal.Length;
            }

            private void ReadDigits(bool requireDigit)
            {
                int start = _position;
                while (_position < _text.Length && IsDigit(_text[_position]))
                    _position++;
                if (requireDigit && start == _position)
                    throw Error("Expected a JSON digit");
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
                    throw Error("Expected '" + expected + "' in JSON");
            }

            private FormatException Error(string message)
            {
                return new FormatException(
                    message + " at character " + _position.ToString(CultureInfo.InvariantCulture) + ".");
            }

            private static bool IsDigit(char value)
            {
                return value >= '0' && value <= '9';
            }
        }
    }
}
