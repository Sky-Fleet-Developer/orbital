using System;
using JetBrains.Annotations;
using Newtonsoft.Json;
using Zenject;

namespace Orbital.Core.Serialization
{
    public class JsonPerformance : ISerializer
    {
        private readonly JsonSerializerSettings _settings;
        public JsonPerformance()
        {
            _settings = new JsonSerializerSettings();
            _settings.TypeNameHandling = TypeNameHandling.Auto;
            _settings.Converters.Add(new VectorConverter());
            _settings.Converters.Add(new Matrix4x4Converter());
        }
        
        [Inject]
        public void Inject(DiContainer container)
        {
            //_settings.Converters.Add(new ComponentConverter(container));
        }

        public string Serialize(object value)
        {
            return JsonConvert.SerializeObject(value, _settings);
        }

        public void Populate<T>(T target, string value)
        {

            JsonConvert.PopulateObject(value, target, _settings);
        }

        public T Deserialize<T>(string value)
        {
            return JsonConvert.DeserializeObject<T>(value, _settings);
        }
        [CanBeNull]
        public object Deserialize(Type type, string value)
        {
            return JsonConvert.DeserializeObject(value, type, _settings);
        }
        
        /*private class ComponentConverter : JsonConverter<Model.Component>
        {
            private DiContainer _container;

            public ComponentConverter(DiContainer container)
            {
                _container = container;
            }
            
            public override void WriteJson(JsonWriter writer, Model.Component? value, JsonSerializer serializer)
            {
                serializer.Serialize(writer, value);
                //writer.WriteValue(value?.Id ?? -1);
            }

            public override Model.Component? ReadJson(JsonReader reader, Type objectType, Model.Component? existingValue, bool hasExistingValue,
                JsonSerializer serializer)
            {
                return _container.ResolveId<Model.Component>(reader.ReadAsInt32());
            }
        }*/
    }
}
