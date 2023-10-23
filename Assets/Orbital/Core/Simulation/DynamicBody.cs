using System;
using System.Threading.Tasks;
using Ara3D;
using Orbital.Core.TrajectorySystem;
using Sirenix.OdinInspector;
using UnityEngine;
using Zenject;

namespace Orbital.Core.Simulation
{
    public class DynamicBody : SystemComponent<DynamicBodyVariables, DynamicBodySettings>, IDynamicBody, IDynamicBodyAccessor
    {
        private static AsyncThreadScheduler _trajectoryRefreshScheduler = new AsyncThreadScheduler(3);

        [SerializeField] private DynamicBodyVariables variables;
        [SerializeField] private DynamicBodySettings settings;
        private World _world;
        //private Track _trajectoryTrack;
        private IStaticTrajectory _trajectory;
        private IStaticBody _parent;

        private float _awakeTime = 0;
        private const float AwakeDelay = 0;
        private DynamicBodyMode _mode = DynamicBodyMode.Trajectory;
        private SimulationSpace _simulationSpace;
        private RigidbodyPresentation _presentation;
        private Task _trajectoryCalculation;
        private bool _isVelocityDirty;
        private bool _isTrajectoryCalculating;
        private bool _isInitialized;

        #region InterfaceImplementation
        
        public Task WaitForTrajectoryCalculated => _trajectoryCalculation;
        public IStaticTrajectory Trajectory => _trajectory;
        public IStaticBody Parent => _parent;
        [ShowInInspector] public DynamicBodyMode Mode => _mode;
        ITrajectoryRefSampler IDynamicBody.TrajectorySampler => _trajectory;
        IDynamicBody IDynamicBodyAccessor.Self => this;
        IStaticBody IDynamicBodyAccessor.Parent
        {
            get => _parent;
            set => _parent = value;
        }
        IStaticTrajectory IDynamicBodyAccessor.Trajectory
        {
            get => _trajectory;
            set => _trajectory = value;
        }
        public RigidbodyPresentation Presentation => _presentation;
        public event Action<DynamicBodyMode> ModeChangedHandler;
        #endregion

        public override DynamicBodyVariables Variables
        {
            get => variables;
            set => variables = value;
        }
        public override DynamicBodySettings Settings
        {
            get => settings;
            set => settings = value;
        }
        
        protected override void Start()
        {
            base.Start();
            Init();
        }

        public void Init()
        {
            if(_isInitialized) return;
            _parent = GetComponentInParent<IStaticBody>();
            _world = GetComponentInParent<World>();
            #if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                _world.Load();
            }
            #endif
            _world.RegisterRigidBody(this);
            _trajectory.Calculate(variables.position, variables.velocity);
            _isInitialized = true;
            //_trajectoryCalculation = FillTrajectory(variables.position, variables.velocity);
        }

        /*private void OnDrawGizmosSelected()
        {
            if (_trajectory == null || _parent == null || _world == null)
            {
                _isInitialized = false;
            }
            Init();
            _trajectory.DrawGizmos();
        }*/

        private void OnValidate()
        {
            if(_trajectory == null) return;
            _isInitialized = false;
            Init();
            /*if (Application.isPlaying)
            {
                _trajectoryCalculation = RefreshTrajectory();
            }
            else
            {
                FillTrajectory(variables.position, variables.velocity);
            }*/
        }

        public void SetVelocityDirty()
        {
            if (_mode == DynamicBodyMode.Trajectory) throw new Exception("Can't accelerate in Trajectory mode!");
            
            _isVelocityDirty = true;
            if (!_isTrajectoryCalculating)
            {
                RecalculateLoop();
            }

            if (_mode != DynamicBodyMode.Simulation)
            {
                _mode = DynamicBodyMode.Simulation;
                ModeChangedHandler?.Invoke(_mode);
            }
        }

        public void SimulationWasMoved(DVector3 deltaPosition, DVector3 deltaVelocity)
        {
            if (_mode == DynamicBodyMode.Simulation)
            {
                _presentation.Position -= (Vector3) deltaPosition;
                _presentation.Velocity -= (Vector3) deltaVelocity;
            }
        }

        private async void RecalculateLoop()
        {
            _isTrajectoryCalculating = true;
            while (_isVelocityDirty)
            {
                _isVelocityDirty = false;
                await RefreshTrajectory();
            }
            _isTrajectoryCalculating = false;
            _mode = DynamicBodyMode.Sleep;
        }

       /* private async void StartSleepTimer()
        {
            //Debug.Log($"Begin timer {name}");
            _awakeTime = Time.realtimeSinceStartup;
            do
            {
                await Task.Delay((int) (_awakeTime + AwakeDelay - Time.realtimeSinceStartup) * 998);
                //Debug.Log($"Timer step {name}");
            } while (!IsSleepTimerInCondition());

            Sleep();
        }*/

        private bool IsSleepTimerInCondition()
        {
            return _awakeTime + AwakeDelay < Time.realtimeSinceStartup;
        }
        

        public void Present(SimulationSpace simulationSpace)
        {
            if (_mode != DynamicBodyMode.Trajectory) throw new Exception("Already presents!");
            _mode = DynamicBodyMode.Sleep;


            _simulationSpace = simulationSpace;
            DVector3 origin = simulationSpace.Position;
            DVector3 originVelocity = simulationSpace.Velocity;
            var sample = _trajectory.GetSample(TimeService.WorldTime);
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
            if (_mode == DynamicBodyMode.Trajectory) throw new Exception("Present is not exists!");
            if (_mode != DynamicBodyMode.Sleep) await RefreshTrajectory();
            _mode = DynamicBodyMode.Trajectory;

            Destroy(_presentation.gameObject);

            ModeChangedHandler?.Invoke(_mode);
        }

        private async Task RefreshTrajectory()
        {
            //await FillTrajectory((DVector3) _presentation.Position + _simulationSpace.Position,
               // (DVector3) _presentation.Velocity + _simulationSpace.Velocity);
            //RefreshVariables();
        }

       /* private Task FillTrajectory(DVector3 position, DVector3 velocity)
        {
            _trajectoryCalculation = FillTrajectoryRoutine(position, velocity);
            return _trajectoryCalculation;
        }*/

        /*private async Task FillTrajectoryRoutine(DVector3 position, DVector3 velocity)
        {
            //await _trajectoryRefreshScheduler.Schedule(() => IterativeSimulation.FillTrajectoryContainer(_trajectory, TimeService.WorldTime, position, velocity, _parent.MassSystem.Mass, targetAccuracy, nonuniformity));
            //_trajectory.SetDirty();
            _trajectoryTrack.ResetProgress();
        }*/
        
        /*[Button]
        private void Simulate()
        {
            IterativeSimulation.DrawTrajectoryCircle(variables.position, variables.velocity, _parent.MassSystem.Mass,
                targetAccuracy, nonuniformity);
        }*/

        private void RefreshVariables()
        {
            DynamicBodyVariables local = variables;
            //(local.position, local.velocity) = _trajectoryTrack.GetSample(TimeService.WorldTime);
            variables = local;
        }
    }

    [Serializable]
    public struct DynamicBodyVariables
    {
        public DVector3 velocity;
        public DVector3 position;
    }

    [Serializable]
    public struct DynamicBodySettings
    {
        public RigidbodyPresentation presentation;
    }
}