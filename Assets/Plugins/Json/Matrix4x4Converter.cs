// Decompiled with JetBrains decompiler
// Type: Newtonsoft.Json.Converters.Matrix4x4Converter
// Assembly: Newtonsoft.Json, Version=8.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: 97722D3A-BC9F-4CF6-9F9E-21E6770081B3
// Assembly location: G:\Projects\scy-fleet-fp\com.unity.nuget.newtonsoft-json@2.0.0-preview\Runtime\Standalone\Newtonsoft.Json.dll

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Plugins.Json
{
  public class Matrix4x4Converter : JsonConverter
  {
    public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
    {
      if (value == null)
      {
        writer.WriteNull();
      }
      else
      {
        Matrix4x4 matrix4x4 = (Matrix4x4) value;
        writer.WriteStartObject();
        writer.WritePropertyName("m00");
        writer.WriteValue(matrix4x4.m00);
        writer.WritePropertyName("m01");
        writer.WriteValue(matrix4x4.m01);
        writer.WritePropertyName("m02");
        writer.WriteValue(matrix4x4.m02);
        writer.WritePropertyName("m03");
        writer.WriteValue(matrix4x4.m03);
        writer.WritePropertyName("m10");
        writer.WriteValue(matrix4x4.m10);
        writer.WritePropertyName("m11");
        writer.WriteValue(matrix4x4.m11);
        writer.WritePropertyName("m12");
        writer.WriteValue(matrix4x4.m12);
        writer.WritePropertyName("m13");
        writer.WriteValue(matrix4x4.m13);
        writer.WritePropertyName("m20");
        writer.WriteValue(matrix4x4.m20);
        writer.WritePropertyName("m21");
        writer.WriteValue(matrix4x4.m21);
        writer.WritePropertyName("m22");
        writer.WriteValue(matrix4x4.m22);
        writer.WritePropertyName("m23");
        writer.WriteValue(matrix4x4.m23);
        writer.WritePropertyName("m30");
        writer.WriteValue(matrix4x4.m30);
        writer.WritePropertyName("m31");
        writer.WriteValue(matrix4x4.m31);
        writer.WritePropertyName("m32");
        writer.WriteValue(matrix4x4.m32);
        writer.WritePropertyName("m33");
        writer.WriteValue(matrix4x4.m33);
        writer.WriteEnd();
      }
    }

    public override object ReadJson(
      JsonReader reader,
      System.Type objectType,
      object existingValue,
      JsonSerializer serializer)
    {
      if (reader.TokenType == JsonToken.Null)
        return (object) new Matrix4x4();
      JObject jobject = JObject.Load(reader);
      return (object) new Matrix4x4()
      {
        m00 = (float) jobject["m00"],
        m01 = (float) jobject["m01"],
        m02 = (float) jobject["m02"],
        m03 = (float) jobject["m03"],
        m20 = (float) jobject["m20"],
        m21 = (float) jobject["m21"],
        m22 = (float) jobject["m22"],
        m23 = (float) jobject["m23"],
        m30 = (float) jobject["m30"],
        m31 = (float) jobject["m31"],
        m32 = (float) jobject["m32"],
        m33 = (float) jobject["m33"]
      };
    }

    public override bool CanRead => true;

    public override bool CanConvert(System.Type objectType) => objectType == typeof (Matrix4x4);
  }
}
