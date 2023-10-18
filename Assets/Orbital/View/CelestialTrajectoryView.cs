using Orbital.Core.TrajectorySystem;
using UnityEngine;

namespace Orbital.View
{
    public class CelestialTrajectoryView
    {
        private CircleViewBuffers _buffers;
        private RelativeTrajectory _source;
        private int _matrixIdx;
        public CelestialTrajectoryView(RelativeTrajectory source, CircleViewBuffers buffers)
        {
            _source = source;
            _buffers = buffers;
            _matrixIdx = buffers.RegisterMatrix();
        }

        public void UpdateMatrix(Vector3 offset, float scale)
        {
            Matrix4x4 trs = _source.RotationMatrix;
            /*trs.m00 *= scale;
            trs.m01 *= scale;
            trs.m02 *= scale;
            trs.m10 *= scale;
            trs.m11 *= scale;
            trs.m12 *= scale;
            trs.m20 *= scale;
            trs.m21 *= scale;
            trs.m22 *= scale;*/
            trs.m03 = offset.x;
            trs.m13 = offset.y;
            trs.m23 = offset.z;
            
            Matrix4x4 figure = Matrix4x4.TRS(new Vector3(0, 0, (float)((_source.PericenterRadius - _source.SemiMajorAxis) * scale)), Quaternion.identity, new Vector3((float)_source.SemiMinorAxis, 0, (float)_source.SemiMajorAxis) * scale);

            _buffers.SetMatrix(_matrixIdx, trs * figure);
        }
        
    }
}
