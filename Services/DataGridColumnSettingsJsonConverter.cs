using Mangaanya.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;

namespace Mangaanya.Services
{
    public class DataGridColumnSettingsJsonConverter : JsonConverter
    {
        public override bool CanConvert(Type objectType)
        {
            return objectType == typeof(DataGridSettings);
        }

        public override object? ReadJson(JsonReader reader, Type objectType, object? existingValue, JsonSerializer serializer)
        {
            if (reader.TokenType == JsonToken.Null)
                return null;

            var settings = new DataGridSettings();
            
            if (reader.TokenType == JsonToken.StartObject)
            {
                JObject obj = JObject.Load(reader);
                var columns = obj["Columns"] as JArray;
                
                if (columns != null)
                {
                    foreach (var column in columns)
                    {
                        var columnSetting = new DataGridColumnSettings
                        {
                            Header = column["Header"]?.ToString() ?? string.Empty,
                            Width = column["Width"]?.Value<double>() ?? 100,
                            DisplayIndex = column["DisplayIndex"]?.Value<int>() ?? 0,
                            IsVisible = column["IsVisible"]?.Value<bool>() ?? true
                        };
                        
                        settings.Columns.Add(columnSetting);
                    }
                }
            }
            
            return settings;
        }

        public override void WriteJson(JsonWriter writer, object? value, JsonSerializer serializer)
        {
            if (value is DataGridSettings settings)
            {
                writer.WriteStartObject();
                
                writer.WritePropertyName("Columns");
                writer.WriteStartArray();
                
                foreach (var column in settings.Columns)
                {
                    writer.WriteStartObject();
                    
                    writer.WritePropertyName("Header");
                    writer.WriteValue(column.Header);
                    
                    writer.WritePropertyName("Width");
                    writer.WriteValue(column.Width);
                    
                    writer.WritePropertyName("DisplayIndex");
                    writer.WriteValue(column.DisplayIndex);
                    
                    writer.WritePropertyName("IsVisible");
                    writer.WriteValue(column.IsVisible);
                    
                    writer.WriteEndObject();
                }
                
                writer.WriteEndArray();
                writer.WriteEndObject();
            }
            else
            {
                writer.WriteNull();
            }
        }
    }
}
