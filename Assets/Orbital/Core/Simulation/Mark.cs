using Ara3D;

namespace Orbital.Core.Simulation
{
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

        public (DVector3 position, DVector3 velocity) Interpolate(Mark next, DVector3 tangentA, DVector3 tangentB, double t, bool positionRequired = true, bool velocityRequired = true)
        {
            DVector3 resultPosition = DVector3.Zero;
            if (positionRequired)
            {
                DVector3 a = DVector3.Lerp(Position, tangentA, t);
                DVector3 b = DVector3.Lerp(tangentB, next.Position, t);
                DVector3 c = DVector3.Lerp(tangentA, tangentB, t);
                DVector3 d = DVector3.Lerp(a, c, t);
                DVector3 e = DVector3.Lerp(c, b, t);
                resultPosition = DVector3.Lerp(d, e, t);
            }

            return (resultPosition, velocityRequired ? DVector3.Lerp(Velocity, next.Velocity, t) : DVector3.Zero);
        }
    }
}