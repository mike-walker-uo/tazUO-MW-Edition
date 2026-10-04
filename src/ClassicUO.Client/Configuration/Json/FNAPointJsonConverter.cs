using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Xna.Framework;

namespace ClassicUO.Configuration.Json
{
    sealed class Point2Converter : JsonConverter<Point>
    {
        public override Point Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
            => ReadPoint(ref reader);

        internal static Point ReadPoint(ref Utf8JsonReader reader)
        {
            if (reader.TokenType != JsonTokenType.StartObject)
                throw new JsonException("Point must be an object.");

            var point = new Point();
            while (reader.Read())
            {
                if (reader.TokenType == JsonTokenType.EndObject) return point;
                if (reader.TokenType != JsonTokenType.PropertyName)
                    throw new JsonException("Expected a point property.");

                string name = reader.GetString();
                if (!reader.Read()) throw new JsonException("Missing point value.");

                switch (name)
                {
                    case "X":
                    case "Y":
                        if (reader.TokenType != JsonTokenType.Number || !reader.TryGetInt32(out int value))
                            throw new JsonException("Point coordinates must be integers.");
                        if (name == "X") point.X = value;
                        else point.Y = value;
                        break;
                    default:
                        reader.Skip();
                        break;
                }
            }

            throw new JsonException("Incomplete point object.");
        }

        public override void Write(Utf8JsonWriter writer, Point value, JsonSerializerOptions options)
        {
            writer.WriteStartObject();
            writer.WriteNumber("X", value.X);
            writer.WriteNumber("Y", value.Y);
            writer.WriteEndObject();
        }
    }

    sealed class NullablePoint2Converter : JsonConverter<Point?>
    {
        public override Point? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
            => Point2Converter.ReadPoint(ref reader);

        public override void Write(Utf8JsonWriter writer, Point? value, JsonSerializerOptions options)
        {
            writer.WriteStartObject();
            writer.WriteNumber("X", value.Value.X);
            writer.WriteNumber("Y", value.Value.Y);
            writer.WriteEndObject();
        }
    }
}
