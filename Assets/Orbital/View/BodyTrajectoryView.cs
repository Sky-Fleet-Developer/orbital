using Ara3D;
using Orbital.Core.Simulation;
using Orbital.Core.TrajectorySystem;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;

namespace Orbital.View
{
    public class BodyTrajectoryView
    {
        private TrajectoryContainer _trajectory;
        private OffsetsBuffer _offsetsBuffer;
        private int _offsetIndex;
        private GraphicsBuffer _pathBuffer;
        private Vector3[] _cacheArray;
        public BodyTrajectoryView(TrajectoryContainer source, OffsetsBuffer offsetsBuffer, int pathBufferSize)
        {
            _trajectory = source;
            _offsetsBuffer = offsetsBuffer;
            _offsetIndex = _offsetsBuffer.RegisterNew();
            _pathBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, pathBufferSize, 3 * sizeof(float));
            _cacheArray = new Vector3[pathBufferSize];
        }

        public void FillPathBuffer()
        {
            
        }

        public void UpdateOffset(Vector3 offset)
        {
            _offsetsBuffer.SetOffset(_offsetIndex, offset);
        }
    }

    public struct CopyJob : IJobParallelFor
    {
        public NativeArray<Mark> Origin;
        public NativeArray<Vector3> Destination;
        public void Execute(int index)
        {
            
        }
    }

    public class OffsetsBuffer
    {
        public GraphicsBuffer Buffer;
        private int _count;
        public int RegisterNew() => _count++;

        public void Refresh()
        {
            Buffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, _count, 3 * sizeof(float));
        }

        public void SetOffset(int idx, Vector3 value)
        {
            Buffer.SetData(new [] {value}, 0, idx, 1);
        }
        
        public void Dispose()
        {
            Buffer.Dispose();
            Buffer = null;
        }
    }
}
