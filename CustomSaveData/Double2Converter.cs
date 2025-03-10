using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace CustomSaveData
{
    public class Double2Converter : JsonConverter<Double2>
    {
        public override Double2 ReadJson(JsonReader reader, Type objectType, Double2 existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            JObject obj = JObject.Load(reader);
            return obj.ToObject<Double2>();

        }

        public override void WriteJson(JsonWriter writer, Double2 value, JsonSerializer serializer)
        {
            JObject obj = new JObject
            {
                { "x", value.x },
                { "y", value.y },
            };
            obj.WriteTo(writer);
        }
    }
}