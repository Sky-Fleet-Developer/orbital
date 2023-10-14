using Ara3D;
using Ara3D.Double;
using Orbital.Model.TrajectorySystem;
using Unity.Collections;
using UnityEngine;

namespace Orbital.Model.Simulation
{
    public class TrajectoryContainer
    {
        private NativeArray<Mark> _path;

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
            set => _path[i] = value;
        }

        public int Length => _path.Length;

        public int GetLastIndexForTime(double time)
        {
            for (int i = 0; i < _path.Length; i++)
            {
                if (_path[i].TimeMark > time) return i - 1;
            }

            return -1;
        }
    }

    public class Track : ITrajectorySampler
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
            _currentIdx = container.GetLastIndexForTime(TimeService.WorldTime);
            AssignMarks();
        }

        private void AssignMarks()
        {
            _lastMark = _container[_currentIdx];
            _nextMark = _container[_currentIdx + 1];
            (_tangentA, _tangentB) = _lastMark.CalculateTangents(_nextMark);
        }

        public (DVector3 position, DVector3 velocity) GetSample(double time)
        {
            if (time > _nextMark.TimeMark)
            {
                _currentIdx++;
                AssignMarks();
            }
            double t = (time - _lastMark.TimeMark) / (_nextMark.TimeMark - _lastMark.TimeMark);
            return _lastMark.Interpolate(_nextMark, _tangentA, _tangentB, t);
        }
    }

    public struct Mark
    {
        public DVector3 Position;
        public DVector3 Velocity;
        public double TimeMark;

        public Mark(DVector3 position, DVector3 velocity, double timeMark)
        {
            Position = position;
            Velocity = velocity;
            TimeMark = timeMark;
        }

        public (DVector3 tangentA, DVector3 tangentB) CalculateTangents(Mark next)
        {
            double dTime = next.TimeMark - TimeMark;
            //double velocitiesRatio = Velocity.Length() / next.Velocity.Length();
            const double mul = 1.0 / 3.0;
            return (Position + Velocity * dTime * mul, next.Position - next.Velocity * dTime * mul);
        }

        public (DVector3 position, DVector3 velocity) Interpolate(Mark next, DVector3 tangentA, DVector3 tangentB, double t)
        {
            DVector3 a = DVector3.Lerp(Position, tangentA, t);
            DVector3 b = DVector3.Lerp(tangentB, next.Position, t);
            DVector3 c = DVector3.Lerp(tangentA, tangentB, t);
            DVector3 d = DVector3.Lerp(a, c, t);
            DVector3 e = DVector3.Lerp(c, b, t);
            DVector3 f = DVector3.Lerp(d, e, t);
            return (f, DVector3.Lerp(Velocity, next.Velocity, t));
        }
    }
}