using System.Runtime.InteropServices;
using SharpDX;
using SharpDX.DXGI;
using SharpDX.Toolkit.Graphics;

namespace SharpDX_Test
{
    [StructLayout(LayoutKind.Sequential)]
    internal struct NodeVertex
    {
        [VertexElement("POSITION", 0, Format.R32G32_Float)]
        public Vector2 Position;
        [VertexElement("VELOCITY", 0, Format.R32G32_Float)]
        public Vector2 Velocity;
        [VertexElement("TEXCOORD", 0, Format.R32_Float)]
        public float AtlasIndex;
        [VertexElement("EDGESSTART", 0, Format.R32_SInt)]
        public int EdgesStart;
        [VertexElement("EDGESEND", 0, Format.R32_SInt)]
        public int EdgesEnd;

        public NodeVertex(Vector2 position, int atlasIndex, int edgesStart, int edgesCount)
        {
            Position = position;
            Velocity = Vector2.Zero;
            AtlasIndex = atlasIndex;
            EdgesStart = edgesStart;
            EdgesEnd = edgesStart + edgesCount;
        }
    }
}