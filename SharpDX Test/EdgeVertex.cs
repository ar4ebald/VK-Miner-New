using System.Runtime.InteropServices;
using SharpDX.DXGI;
using SharpDX.Toolkit.Graphics;

namespace SharpDX_Test
{
    [StructLayout(LayoutKind.Sequential)]
    internal struct EdgeVertex
    {
        [VertexElement("POSITION", 0, Format.R32_SInt)]
        public int Source;
        [VertexElement("POSITION", 1, Format.R32_SInt)]
        public int Target;

        public EdgeVertex(int source, int target)
        {
            Source = source;
            Target = target;
        }
    }
}