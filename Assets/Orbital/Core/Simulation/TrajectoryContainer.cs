using System;
using System.Collections;
using System.Collections.Generic;
using Ara3D;
using Orbital.Core.TrajectorySystem;
using Unity.Collections;

namespace Orbital.Core.Simulation
{
    public class TrajectoryContainer : IDynamicTrajectory
    {
        private NativeArray<Mark> _path;
        private int _length;
        public event Action PathChangedHandler;

        public TrajectoryContainer(int arraySize)
        {
            _path = new NativeArray<Mark>(arraySize, Allocator.Persistent);
        }

        ~TrajectoryContainer()
        {
            _path.Dispose();
        }

        public Mark this[int i]
        {
            get => _path[i];
            set
            {
                _path[i] = value;
                _length = i;
            }
        }

        public void SetDirty()
        {
            PathChangedHandler?.Invoke();
        }

        NativeArray<Mark> IDynamicTrajectory.Path => _path;
        public int Length => _length;
        public int Capacity => _path.Length;

        public int GetLastIndexForTime(double time)
        {
            for (int i = 0; i < _length - 1; i++)
            {
                if (_path[i].TimeMark > time) return i - 1;
            }

            return -1;
        }
    }

    public class Track : IOrbitSampler
    {
        private TrajectoryContainer _container;
        private Mark _lastMark;
        private Mark _nextMark;
        private int _currentIdx;
        private DVector3 _tangentA;
        private DVector3 _tangentB;

        public Track(TrajectoryContainer container)
        {
            _container = container;
        }

        private void AssignMarks()
        {
            _lastMark = _container[_currentIdx];
            _nextMark = _container[_currentIdx + 1];
            (_tangentA, _tangentB) = _lastMark.CalculateTangents(_nextMark);
        }

        public void ResetProgress()
        {
            _currentIdx = _container.GetLastIndexForTime(TimeService.WorldTime);
            AssignMarks();
        }
        
        public (DVector3 position, DVector3 velocity) GetSample(double time, bool positionRequired = true, bool velocityRequired = true)
        {
            if (time > _nextMark.TimeMark)
            {
                _currentIdx++;
                AssignMarks();
            }
            double t = (time - _lastMark.TimeMark) / (_nextMark.TimeMark - _lastMark.TimeMark);
            return _lastMark.Interpolate(_nextMark, _tangentA, _tangentB, t, positionRequired, velocityRequired);
        }
    }
}