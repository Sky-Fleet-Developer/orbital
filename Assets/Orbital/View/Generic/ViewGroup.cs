using System;
using System.Collections.Generic;
using UnityEngine;

namespace Orbital.View.Generic
{
    public abstract class ViewGroup
    {
        private static readonly Dictionary<int, GraphicsBuffer> GlobalBuffers = new ();
        private readonly Dictionary<int, GraphicsBuffer> _buffers;
        private readonly RenderParams _renderParams;
        //private List<object> _parametersValues;
        //private List<int> _parametersKeys;

        public ViewGroup(Material material)
        {
            _buffers = new Dictionary<int, GraphicsBuffer>();
            _renderParams = new RenderParams(material);
            _renderParams.matProps = new MaterialPropertyBlock();
        }

        public virtual void Draw()
        {
            
        }

        public virtual void Prepare()
        {
            
        }

        public virtual void Refresh()
        {
            
        }

        public static void AddGlobalBuffer(int key, GraphicsBuffer.Target target, int count, int stride)
        {
            GlobalBuffers.TryAdd(key, new GraphicsBuffer(target, count, stride));
        }

        public void AddBuffer(string key, GraphicsBuffer.Target target, int count, int stride)
        {
            AddBuffer(Shader.PropertyToID(key), target, count, stride);
        }
        public void AddBuffer(int id, GraphicsBuffer.Target target, int count, int stride)
        {
            _buffers.Add(id, new GraphicsBuffer(target, count, stride));
        }
        
        //public void AddParameter(string key, )
        public static void SetGlobalBufferData<T>(int id, T[] data, int managedBufferStartIndex, int graphicsBufferStartIndex)
        {
            GlobalBuffers[id].SetData(data, managedBufferStartIndex, graphicsBufferStartIndex, data.Length);
        }
        public void SetBufferData<T>(int id, T[] data, int managedBufferStartIndex, int graphicsBufferStartIndex)
        {
            _buffers[id].SetData(data, managedBufferStartIndex, graphicsBufferStartIndex, data.Length);
        }

        public void BindStatic(int id)
        {
            _renderParams.matProps.SetBuffer(id, GlobalBuffers[id]);
        }
        public void Bind(int id)
        {
            _renderParams.matProps.SetBuffer(id, _buffers[id]);
        }

        public static void DisposeGlobal(int id)
        {
            GlobalBuffers[id].Dispose();
            GlobalBuffers.Remove(id);
        }
        public void Dispose(int id)
        {
            _buffers[id].Dispose();
            _buffers.Remove(id);
        }

        public void DisposeAll()
        {
            for (int i = 0; i < _buffers.Count; i++)
            {
                _buffers[i].Dispose();
            }
            _buffers.Clear();
        }
    }
}
