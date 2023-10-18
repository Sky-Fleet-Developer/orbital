using Orbital.Core;
using Orbital.Core.TrajectorySystem;
using Orbital.View.Generic;
using UnityEngine;

namespace Orbital.View
{
    public class StaticTrajectoryView : ViewItem<IStaticTrajectory, StaticTrajectoryViewGroup>
    {
        private int _matrixIdx;
        private IStaticBody _parentBody;
        private float _scale;
        public StaticTrajectoryView(IStaticTrajectory model, IStaticBody parentBody, StaticTrajectoryViewGroup group, WorldViewSettings settings) : base(model, group)
        {
            _matrixIdx = group.RegisterMatrix();
            _parentBody = parentBody;
            _scale = 1 / settings.scale;
        }

        public override void Update()
        {
            Vector3 offset = _parentBody.Position;
            Matrix4x4 trs = Model.RotationMatrix;
            trs.m03 = offset.x;
            trs.m13 = offset.y;
            trs.m23 = offset.z;
            
            Matrix4x4 figure = Matrix4x4.TRS(new Vector3(0, 0, (float)((Model.PericenterRadius - Model.SemiMajorAxis) * _scale)), Quaternion.identity, new Vector3((float)Model.SemiMinorAxis, 0, (float)Model.SemiMajorAxis) * _scale);

            Group.SetBufferData(2, new [] {trs * figure}, 0, _matrixIdx);
        }
        
    }
}
