using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace ClassicUO.Renderer
{
    public sealed class WaterEffect : Effect
    {
        public WaterEffect(GraphicsDevice graphicsDevice, byte[] bytecode)
            : base(graphicsDevice, bytecode)
        {
            MatrixTransform = Parameters["MatrixTransform"];
            WorldMatrix = Parameters["WorldMatrix"];
            Viewport = Parameters["Viewport"];
            Time = Parameters["Time"];
            MotionAmount = Parameters["MotionAmount"];
            CurrentTechnique = Techniques["WaterTechnique"];
        }

        public EffectParameter MatrixTransform { get; }
        public EffectParameter WorldMatrix { get; }
        public EffectParameter Viewport { get; }
        public EffectParameter Time { get; }
        public EffectParameter MotionAmount { get; }

        public void Configure(Matrix transform, float time, float motionAmount)
        {
            Matrix.CreateOrthographicOffCenter(
                0f,
                GraphicsDevice.Viewport.Width,
                GraphicsDevice.Viewport.Height,
                0f,
                short.MinValue,
                short.MaxValue,
                out Matrix projection
            );
            Matrix.Multiply(ref transform, ref projection, out Matrix matrix);
            MatrixTransform.SetValue(matrix);
            WorldMatrix.SetValue(Matrix.Identity);
            Viewport.SetValue(new Vector2(GraphicsDevice.Viewport.Width, GraphicsDevice.Viewport.Height));
            Time.SetValue(time);
            MotionAmount.SetValue(motionAmount);
        }
    }
}
