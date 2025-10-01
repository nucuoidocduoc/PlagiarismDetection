using Google.Protobuf.Collections;
using System.Reflection;

namespace PlagiarismDetection
{
    using Google.Protobuf.Collections;
    using Qdrant.Client.Grpc;
    using System.Reflection;

    public static class QdrantExtensions
    {
        public static MapField<string, Value> ToMapField(this object obj)
        {
            var mapField = new MapField<string, Value>();

            if (obj == null) return mapField;

            var properties = obj.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance);

            foreach (var property in properties)
            {
                var value = property.GetValue(obj);
                if (value != null)
                {
                    mapField[property.Name] = value.ToQdrantValue();
                }
            }

            return mapField;
        }

        public static Dictionary<string, object> ToSepicificDictionary(this MapField<string, Value> obj)
        {
            var dictionary = new Dictionary<string, object>();

            foreach(var key in obj.Keys)
            {
                var value = obj[key];
                if (value.HasStringValue)
                {
                    dictionary[key] = value.StringValue;
                }
                else if (value.HasDoubleValue)
                {
                    dictionary[key] = value.DoubleValue;
                }
                else if (value.HasIntegerValue)
                {
                    dictionary[key] = value.IntegerValue;
                }
                else if (value.HasBoolValue)
                {
                    dictionary[key] = value.BoolValue;
                }

            }

            return dictionary;
        }

        public static IEnumerable<KeyValuePair<string, Value>> ToQdrantKeyValue(this object obj)
        {
            if (obj == null)
            {
                yield break;
            }

            var properties = obj.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance);

            foreach (var property in properties)
            {
                var value = property.GetValue(obj);
                if (value != null)
                {
                    yield return new KeyValuePair<string, Value>(property.Name, value.ToQdrantValue());
                }
            }
        }

        public static Value ToQdrantValue(this object value)
        {
            if (value == null) return new Value { NullValue = NullValue.NullValue };

            return value switch
            {
                string s => new Value { StringValue = s },
                int i => new Value { IntegerValue = i },
                long l => new Value { IntegerValue = l },
                float f => new Value { DoubleValue = f },
                double d => new Value { DoubleValue = d },
                bool b => new Value { BoolValue = b },
                DateTime dt => new Value { StringValue = dt.ToString("o") },
                IEnumerable<string> stringList => new Value
                {
                    ListValue = new ListValue
                    {
                        Values = { stringList.Select(s => s.ToQdrantValue()) }
                    }
                },
                IEnumerable<object> objectList => new Value
                {
                    ListValue = new ListValue
                    {
                        Values = { objectList.Select(obj => obj.ToQdrantValue()) }
                    }
                },
                _ => new Value { StringValue = value.ToString() }
            };
        }
    }
}