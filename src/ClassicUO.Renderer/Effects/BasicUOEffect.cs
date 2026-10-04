using Microsoft.Xna.Framework.Graphics;

namespace ClassicUO.Renderer.Effects
{
    internal class BasicUOEffect : Effect
    {
        private static byte[] Shader()
        {
            string path = System.IO.Path.Combine(System.AppContext.BaseDirectory, "IsometricWorldLinear.fxc");
            return System.IO.File.Exists(path) ? System.IO.File.ReadAllBytes(path) : Resources.GetUOShader().ToArray();
        }
        public BasicUOEffect(GraphicsDevice graphicsDevice) : base(graphicsDevice, Shader())
        {
            MatrixTransform = Parameters["MatrixTransform"];
            WorldMatrix = Parameters["WorldMatrix"];
            Viewport = Parameters["Viewport"];
            Brighlight = Parameters["Brightlight"];
            LinearLight = Parameters["LinearLight"];

            CurrentTechnique = Techniques["HueTechnique"];
            Pass = CurrentTechnique.Passes[0];
        }

        public EffectParameter MatrixTransform { get; }
        public EffectParameter WorldMatrix { get; }
        public EffectParameter Viewport { get; }
        public EffectParameter Brighlight { get; }
        public EffectParameter LinearLight { get; }
        public EffectPass Pass { get; }
    }
}
