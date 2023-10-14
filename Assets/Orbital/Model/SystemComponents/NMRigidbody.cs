using System;
using System.Threading.Tasks;
using Ara3D;
using Codice.CM.Client.Differences;
using Orbital.Model.Simulation;
using Orbital.Model.TrajectorySystem;
using Sirenix.OdinInspector;
using UnityEngine;
using Zenject;

namespace Orbital.Model.SystemComponents
{
    public class NMRigidbody : SystemComponent<NMRigidbodyVariables, NMRigidbodySettings>, IRigidBody
    {
        [SerializeField] private NMRigidbodyVariables variables;
        [SerializeField] private NMRigidbodySettings settings;
        [SerializeField] private int accuracy;
        [SerializeField] private float nonuniformity;
        [Inject] private World _world;
        private Track _trajectoryTrack;
        private TrajectoryContainer _trajectoryContainer;
        private MassSystemComponent _parent;

        private float _awakeTime = 0;
        private const float AwakeDelay = 1;
        private RigidBodyMode _mode;
        private bool _isSleep;
        private Observer _observer;
        private RigidbodyPresentation _presentation;

        public override NMRigidbodyVariables Variables { get => variables; set => variables = value; }
        public override NMRigidbodySettings Settings { get => settings; set => settings = value; }

        public MassSystemComponent Parent => _parent;

        public RigidBodyMode Mode => _mode;

        public ITrajectorySampler Trajectory => _trajectoryTrack;

        public event Action<RigidBodyMode> ModeChangedHandler;

        protected override void Start()
        {
            base.Start();
            _parent = GetComponentInParent<MassSystemComponent>();
            _world.RegisterRigidBody(this);
            _trajectoryContainer = new TrajectoryContainer(300);
            FillTrjectory(variables.position, variables.velocity);
            _trajectoryTrack = new Track(_trajectoryContainer);
        }

        public void AwakeFromSleep()
        {
            if (_mode == RigidBodyMode.Trajectory) throw new Exception("Can't accelerate in Trajectory mode!");
            _isSleep = false;
            _mode = RigidBodyMode.Simulation;
            if (IsSleepTimerInCondition())
            {
                StartSleepTimer();
            }
            else
            {
                _awakeTime = Time.realtimeSinceStartup;
            }
            ModeChangedHandler?.Invoke(_mode);
        }

        private async void StartSleepTimer()
        {
            _awakeTime = Time.realtimeSinceStartup;

            do
            {
                await Task.Delay((int)(_awakeTime + AwakeDelay - Time.realtimeSinceStartup) * 998);
            } while (!IsSleepTimerInCondition());
            Sleep();
        }

        private bool IsSleepTimerInCondition()
        {
            return _awakeTime + AwakeDelay < Time.realtimeSinceStartup;
        }

        private void Sleep()
        {
            if (_mode == RigidBodyMode.Trajectory) throw new Exception("Can't sleep in Trajectory mode!");
            _isSleep = true;
            RefreshTrajectory();
            _mode = RigidBodyMode.Sleep;
            ModeChangedHandler?.Invoke(_mode);
        }

        public void Present(Observer observer)
        {
            if (_mode != RigidBodyMode.Trajectory) throw new Exception("Already presents!");
            _isSleep = true;
            _mode = RigidBodyMode.Sleep;


            _observer = observer;
            DVector3 origin = observer.Position;
            DVector3 originVelocity = observer.Velocity;
            var sample = _trajectoryTrack.GetSample(TimeService.WorldTime);
            Vector3 localPosition = sample.position - origin;

            _presentation = Instantiate(Settings.presentation, observer.Root);
            _presentation.Init(this, observer);
            _presentation.Position = localPosition;
            _presentation.Rotation = Quaternion.identity;
            _presentation.Velocity = sample.velocity - originVelocity;

            ModeChangedHandler?.Invoke(_mode);
        }

        public void RemovePresent()
        {
            if (_mode == RigidBodyMode.Trajectory) throw new Exception("Present is not exists!");
            _isSleep = false;
            if (!_isSleep) RefreshTrajectory();
            _mode = RigidBodyMode.Trajectory;

            Destroy(_presentation.gameObject);

            ModeChangedHandler?.Invoke(_mode);
        }

        private void RefreshTrajectory()
        {
            FillTrjectory((DVector3)_presentation.Position + _observer.Position, (DVector3)_presentation.Velocity + _observer.Velocity);
            RefreshVariables();
        }

        private void FillTrjectory(DVector3 position, DVector3 velocity)
        {
            IterativeSimulation.FillTrajectoryContainer(_trajectoryContainer, TimeService.WorldTime, position, velocity, _parent.Mass, accuracy, nonuniformity);
        }

        private void RefreshVariables()
        {
            NMRigidbodyVariables local = variables;
            (local.position, local.velocity) = _trajectoryTrack.GetSample(TimeService.WorldTime);
            variables = local;
        }

        [Button]
        private void Simulate()
        {
            IterativeSimulation.DrawTrajectoryCircle(variables.position, variables.velocity, variables.parentMass, accuracy, nonuniformity);
        }
    }

    [Serializable]
    public struct NMRigidbodyVariables
    {
        public Vector3 velocity;
        public Vector3 position;
        public float parentMass;
    }
    [Serializable]
    public struct NMRigidbodySettings
    {
        public RigidbodyPresentation presentation;
    }
}
