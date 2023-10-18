using System;
using Orbital.View.Generic;
using UnityEngine;

namespace Orbital.View
{
    [Serializable]
    public struct StaticTrajectoryViewSettings
    {
        public Material material;
        public int accuracy;
    }
    
    public class StaticTrajectoryViewGroup : ViewGroup
    {
        private int _lastMatricesCount;
        private int _matricesCount;
        private int _accuracy;
        private int _indicesId = Shader.PropertyToID("_Indices");
        private int _positionsId = Shader.PropertyToID("_Positions");
        private int _matricesId = Shader.PropertyToID("_Matrices");
        
        public StaticTrajectoryViewGroup(StaticTrajectoryViewSettings settings) : base(settings.material)
        {
            _accuracy = settings.accuracy;
        }
    
        public int RegisterMatrix()
        {
            return _matricesCount++;
        }

        public override void Prepare()
        {
            AddBuffer(_positionsId, GraphicsBuffer.Target.Structured, _accuracy * 2 + 2, sizeof(int));
            {
                int[] arr = new int[_accuracy * 2];
                for (int i = 0; i < _accuracy; i++)
                {
                    arr[i * 2] = i;
                    arr[i * 2 + 1] = i + 1;
                }
                SetBufferData(0, arr, 0, 0);
            }
            AddGlobalBuffer(_indicesId, GraphicsBuffer.Target.Structured, _accuracy + 1, 2 * sizeof(float));
            {
                Vector2[] arr = new Vector2[_accuracy + 1];
                for (int i = 0; i <= _accuracy; i++)
                {
                    float angle = ((float) i / _accuracy) * Mathf.PI * 2;
                    arr[i] = new Vector2(Mathf.Sin(angle), Mathf.Cos(angle));
                }
                SetGlobalBufferData(_indicesId, arr, 0, 0);
            }
            
            AddBuffer(_matricesId, GraphicsBuffer.Target.Structured, _accuracy + 1, 2 * sizeof(float));

        }

        
        
        public override void Refresh()
        {
            if (_lastMatricesCount != _matricesCount)
            {
                Dispose(_matricesId);
                AddBuffer(_matricesId, GraphicsBuffer.Target.Structured, _matricesCount, 16 * sizeof(float));
            }
        }
    }
}