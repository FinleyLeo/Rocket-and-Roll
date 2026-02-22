using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Unity.Services.Multiplayer.Editor.Matchmaker.Authoring.Core.Model;
using JObjSepcialized = Unity.Services.Multiplayer.Editor.Matchmaker.Authoring.AdminApi.MatchmakerAdminClient.JsonObjectSpecialized;

namespace Unity.Services.Multiplayer.Editor.Matchmaker.Authoring.IO
{
    /// <summary>
    /// This converter stores the raw json string for complex object that we don't care to deserialize and the client generator can't handle
    /// It can take a json object, array, string or number literal and output it back without messing the type
    /// </summary>
    class JsonObjectSpecializedConverter : JsonConverter
    {
        public override void WriteJson(JsonWriter writer,
            object value,
            JsonSerializer serializer)
        {
            if (value is JsonObject valueJson)
            {
                var obj = JToken.Parse(valueJson.Value);
                obj.WriteTo(writer);
            }
        }

        public override object ReadJson(
            JsonReader reader,
            Type objectType,
            object existingValue,
            JsonSerializer serializer)
        {
            var obj = JToken.ReadFrom(reader);
            if (obj.Type == JTokenType.String)
                return new JObjSepcialized("\"" + obj + "\"");
            return new JObjSepcialized(obj.ToString());
        }

        public override bool CanConvert(Type objectType)
        {
            // FilterValue has its own FilterValueConverter that needs to be used instead of this one
            return typeof(JsonObject).IsAssignableFrom(objectType) && !typeof(FilteredPoolConfig.Filter.FilterValue).IsAssignableFrom(objectType);
        }
    }
}
