using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Orbital.Model.Serialization
{
  public class VectorConverter : JsonConverter
  {
    private static readonly System.Type V2 = typeof (Vector2);
    private static readonly System.Type V3 = typeof (Vector3);
    private static readonly System.Type V4 = typeof (Vector4);

    public bool EnableVector2 { get; set; }

    public bool EnableVector3 { get; set; }

    public bool EnableVector4 { get; set; }

    /// <summary>
    /// Default Constructor - All Vector types enabled by default
    /// </summary>
    public VectorConverter()
    {
      this.EnableVector2 = true;
      this.EnableVector3 = true;
      this.EnableVector4 = true;
    }

    /// <summary>Selectively enable Vector types</summary>
    /// <param name="enableVector2">Use for Vector2 objects</param>
    /// <param name="enableVector3">Use for Vector3 objects</param>
    /// <param name="enableVector4">Use for Vector4 objects</param>
    public VectorConverter(bool enableVector2, bool enableVector3, bool enableVector4)
      : this()
    {
      this.EnableVector2 = enableVector2;
      this.EnableVector3 = enableVector3;
      this.EnableVector4 = enableVector4;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="writer"></param>
    /// <param name="value"></param>
    /// <param name="serializer"></param>
    public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
    {
      if (value == null)
      {
        writer.WriteNull();
      }
      else
      {
        System.Type type = value.GetType();
        if ((object) type == (object) VectorConverter.V2)
        {
          Vector2 vector2 = (Vector2) value;
          VectorConverter.WriteVector(writer, vector2.x, vector2.y, new float?(), new float?());
        }
        else if ((object) type == (object) VectorConverter.V3)
        {
          Vector3 vector3 = (Vector3) value;
          VectorConverter.WriteVector(writer, vector3.x, vector3.y, new float?(vector3.z), new float?());
        }
        else if ((object) type == (object) VectorConverter.V4)
        {
          Vector4 vector4 = (Vector4) value;
          VectorConverter.WriteVector(writer, vector4.x, vector4.y, new float?(vector4.z), new float?(vector4.w));
        }
        else
          writer.WriteNull();
      }
    }

    private static void WriteVector(JsonWriter writer, float x, float y, float? z, float? w)
    {
      writer.WriteStartObject();
      writer.WritePropertyName(nameof (x));
      writer.WriteValue(x);
      writer.WritePropertyName(nameof (y));
      writer.WriteValue(y);
      if (z.HasValue)
      {
        writer.WritePropertyName(nameof (z));
        writer.WriteValue(z.Value);
        if (w.HasValue)
        {
          writer.WritePropertyName(nameof (w));
          writer.WriteValue(w.Value);
        }
      }
      writer.WriteEndObject();
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="reader"></param>
    /// <param name="objectType"></param>
    /// <param name="existingValue"></param>
    /// <param name="serializer"></param>
    /// <returns></returns>
    public override object ReadJson(
      JsonReader reader,
      System.Type objectType,
      object existingValue,
      JsonSerializer serializer)
    {
      if ((object) objectType == (object) VectorConverter.V2)
        return (object) VectorConverter.PopulateVector2(reader);
      return (object) objectType == (object) VectorConverter.V3 ? (object) VectorConverter.PopulateVector3(reader) : (object) VectorConverter.PopulateVector4(reader);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="objectType"></param>
    /// <returns></returns>
    public override bool CanConvert(System.Type objectType)
    {
      if (this.EnableVector2 && (object) objectType == (object) VectorConverter.V2 || this.EnableVector3 && (object) objectType == (object) VectorConverter.V3)
        return true;
      return this.EnableVector4 && (object) objectType == (object) VectorConverter.V4;
    }

    private static Vector2 PopulateVector2(JsonReader reader)
    {
      Vector2 vector2 = new Vector2();
      if (reader.TokenType != JsonToken.Null)
      {
        JObject jobject = JObject.Load(reader);
        vector2.x = jobject["x"].Value<float>();
        vector2.y = jobject["y"].Value<float>();
      }
      return vector2;
    }

    private static Vector3 PopulateVector3(JsonReader reader)
    {
      Vector3 vector3 = new Vector3();
      if (reader.TokenType != JsonToken.Null)
      {
        JObject jobject = JObject.Load(reader);
        vector3.x = jobject["x"].Value<float>();
        vector3.y = jobject["y"].Value<float>();
        vector3.z = jobject["z"].Value<float>();
      }
      return vector3;
    }

    private static Vector4 PopulateVector4(JsonReader reader)
    {
      Vector4 vector4 = new Vector4();
      if (reader.TokenType != JsonToken.Null)
      {
        JObject jobject = JObject.Load(reader);
        vector4.x = jobject["x"].Value<float>();
        vector4.y = jobject["y"].Value<float>();
        vector4.z = jobject["z"].Value<float>();
        vector4.w = jobject["w"].Value<float>();
      }
      return vector4;
    }
  }
}
