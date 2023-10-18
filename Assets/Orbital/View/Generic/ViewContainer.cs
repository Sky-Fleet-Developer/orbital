using System.Collections.Generic;
using UnityEngine;

namespace Orbital.View.Generic
{
    public class ViewContainer
    {
        private readonly List<GraphicsBuffer> _buffers;
        private readonly List<int> _buffersKeys;
        private readonly RenderParams _renderParams;
        //private List<object> _parametersValues;
        //private List<int> _parametersKeys;

        public ViewContainer(Material material)
        {
            _buffers = new List<GraphicsBuffer>();
            _buffersKeys = new List<int>();
            _renderParams = new RenderParams(material);
            _renderParams.matProps = new MaterialPropertyBlock();
        }

        public virtual void Draw()
        {
            
        }

        public virtual void Prepare()
        {
            
        }

        public int AddBuffer(string key, GraphicsBuffer.Target target, int count, int stride)
        {
            _buffers.Add(new GraphicsBuffer(target, count, stride));
            _buffersKeys.Add(Shader.PropertyToID(key));
            return _buffers.Count - 1;
        }
        
        //public void AddParameter(string key, )

        public void SetBufferData<T>(int id, T[] data, int managedBufferStartIndex, int graphicsBufferStartIndex)
        {
            _buffers[id].SetData(data, managedBufferStartIndex, graphicsBufferStartIndex, data.Length);
        }

        public void Bind()
        {
            for (int i = 0; i < _buffers.Count; i++)
            {
                _renderParams.matProps.SetBuffer(_buffersKeys[i], _buffers[i]);
            }
        }

        public void Dispose()
        {
            for (int i = 0; i < _buffers.Count; i++)
            {
                _buffers[i].Dispose();
            }
            _buffers.Clear();
            _buffersKeys.Clear();
        }
    }
}
