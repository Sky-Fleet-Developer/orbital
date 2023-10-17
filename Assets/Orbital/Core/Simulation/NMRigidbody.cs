using System;
using System.Threading.Tasks;
using Ara3D;
using Orbital.Core.TrajectorySystem;
using Sirenix.OdinInspector;
using UnityEngine;
using Zenject;

namespace Orbital.Core.Simulation
{
    public class NMRigidbody : SystemComponent<NMRigidbodyVariables, NMRigidbodySettings>, IRigidBody
    {
        private static AsyncThreadScheduler _trajectoryRefreshScheduler = new AsyncThreadScheduler(3);

        [SerializeField] private NMRigidbodyVariables variables;
        [SerializeField] private NMRigidbodySettings settings;
        [SerializeField] private int accuracy;
        [SerializeField] private float nonuniformity;
        [Inject] private World _world;
        private Track _trajectoryTrack;
        private TrajectoryContainer _trajectoryContainer;
        private MassSystemComponent _parent;

        private float _awakeTime = 0;
        private const float AwakeDelay = 4;
        private RigidBodyMode _mode = RigidBodyMode.Trajectory;
        private SimulationSpace _simulationSpace;
        private RigidbodyPresentation _presentation;
        private Task _trajectoryCalculation;

        public Task WaitForTrajectoryCalculated => _trajectoryCalculation;

        public override NMRigidbodyVariables Variables
        {
            get => variables;
            set => variables = value;
        }

        public override NMRigidbodySettings Settings
        {
            get => settings;
            set => settings = value;
        }

        public MassSystemComponent Parent => _parent;

        [ShowInInspector] public RigidBodyMode Mode => _mode;

        public ITrajectorySampler Trajectory => _trajectoryTrack;
        public RigidbodyPresentation Presentation => _presentation;

        public event Action<RigidBodyMode> ModeChangedHandler;

        protected override void Start()
        {
            base.Start();
            _parent = GetComponentInParent<MassSystemComponent>();
            _world.RegisterRigidBody(this);
            _trajectoryContainer = new TrajectoryContainer(300);
            _trajectoryTrack = new Track(_trajectoryContainer);
            Task t = FillTrajectory(variables.position, variables.velocity);
        }

        public void AwakeFromSleep()
        {
            if (_mode == RigidBodyMode.Trajectory) throw new Exception("Can't accelerate in Trajectory mode!");
            _mode = RigidBodyMode.Simulation;
            //Debug.Log($"Call awake {name}");
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
            //Debug.Log($"Begin timer {name}");
            _awakeTime = Time.realtimeSinceStartup;

            do
            {
                await Task.Delay((int) (_awakeTime + AwakeDelay - Time.realtimeSinceStartup) * 998);
                //Debug.Log($"Timer step {name}");
            } while (!IsSleepTimerInCondition());

            Sleep();
        }

        private bool IsSleepTimerInCondition()
        {
            return _awakeTime + AwakeDelay < Time.realtimeSinceStartup;
        }

        private async void Sleep()
        {
            //Debug.Log($"Sleep {name}");
            if (_mode == RigidBodyMode.Trajectory) throw new Exception("Can't sleep in Trajectory mode!");
            await RefreshTrajectory();
            _mode = RigidBodyMode.Sleep;
            ModeChangedHandler?.Invoke(_mode);
        }

        public void Present(SimulationSpace simulationSpace)
        {
            if (_mode != RigidBodyMode.Trajectory) throw new Exception("Already presents!");
            _mode = RigidBodyMode.Sleep;


            _simulationSpace = simulationSpace;
            DVector3 origin = simulationSpace.Position;
            DVector3 originVelocity = simulationSpace.Velocity;
            var sample = _trajectoryTrack.GetSample(TimeService.WorldTime);
            Vector3 localPosition = sample.position - origin;

            _presentation = Instantiate(Settings.presentation, simulationSpace.Root);
            _presentation.Init(this, simulationSpace);
            _presentation.Position = localPosition;
            _presentation.Rotation = Quaternion.identity;
            _presentation.Velocity = sample.velocity - originVelocity;

            ModeChangedHandler?.Invoke(_mode);
        }

        public async void RemovePresent()
        {
            if (_mode == RigidBodyMode.Trajectory) throw new Exception("Present is not exists!");
            if (_mode != RigidBodyMode.Sleep) RefreshTrajectory();
            _mode = RigidBodyMode.Trajectory;

            Destroy(_presentation.gameObject);

            ModeChangedHandler?.Invoke(_mode);
        }

        private async Task RefreshTrajectory()
        {
            await FillTrajectory((DVector3) _presentation.Position + _simulationSpace.Position,
                (DVector3) _presentation.Velocity + _simulationSpace.Velocity);
            RefreshVariables();
        }

        private Task FillTrajectory(DVector3 position, DVector3 velocity)
        {
            _trajectoryCalculation = FillTrajectoryRoutine(position, velocity);
            return _trajectoryCalculation;
        }

        private async Task FillTrajectoryRoutine(DVector3 position, DVector3 velocity)
        {
            await _trajectoryRefreshScheduler.Schedule(() =>
                IterativeSimulation.FillTrajectoryContainer(_trajectoryContainer,
                    TimeService.WorldTime, position, velocity, _parent.Mass, accuracy, nonuniformity));
            _trajectoryTrack.ResetProgress();
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
            IterativeSimulation.DrawTrajectoryCircle(variables.position, variables.velocity, variables.parentMass,
                accuracy, nonuniformity);
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