using System.Numerics;
using CUE4Parse_Conversion.Meshes.PSK;
using UniversalUmap.Rendering.Resources;
using Veldrid;

namespace UniversalUmap.Rendering.Renderables.Models
{
    public class Lod : IDisposable
    {
        public readonly DeviceBuffer VertexBuffer;
        public readonly DeviceBuffer IndexBuffer;
        public readonly Section[] Sections;
        public readonly bool IsTwoSided;

        private Lod(DeviceBuffer vertexBuffer, DeviceBuffer indexBuffer, Section[] sections, bool isTwoSided)
        {
            VertexBuffer = vertexBuffer;
            IndexBuffer = indexBuffer;
            Sections = sections;
            IsTwoSided = isTwoSided;
        }

        public static bool TryCreate(GraphicsDevice graphicsDevice, CStaticMeshLod lod, out Lod lodInstance)
        {
            lodInstance = null;
            try
            {
                if (lod.Verts == null || lod.Indices == null || lod.Sections == null)
                    return false;

                // Process vertex data
                var vertices = new Vertex[lod.Verts.Length];
                for (var i = 0; i < lod.Verts.Length; i++)
                {
                    var vert = lod.Verts[i];
                    var position = new Vector3(vert.Position.X, vert.Position.Z, vert.Position.Y);

                    Vector4 color;
                    if (lod.VertexColors != null)
                        color = new Vector4(lod.VertexColors[i].R, lod.VertexColors[i].G, lod.VertexColors[i].B, lod.VertexColors[i].A);
                    else
                        color = new Vector4(1, 1, 1, 1);

                    var normal = new Vector3(vert.Normal.X, vert.Normal.Z, vert.Normal.Y);
                    var tangent = new Vector3(vert.Tangent.X, vert.Tangent.Z, vert.Tangent.Y);
                    var uv = new Vector2(vert.UV.U, vert.UV.V);
                    vertices[i] = new Vertex(position, color, normal, tangent, uv);
                }

                // Process index data
                var indices = new uint[lod.Indices.Value.Length];
                for (var i = 0; i < lod.Indices.Value.Length; i++)
                    indices[i] = (uint)lod.Indices.Value[i];

                // Process sections data
                var sections = new Section[lod.Sections.Value.Length];
                for (var i = 0; i < sections.Length; i++)
                    sections[i] = new Section(lod.Sections.Value[i].MaterialIndex, lod.Sections.Value[i].FirstIndex, lod.Sections.Value[i].NumFaces);

                // Create buffers
                var vertexBuffer = graphicsDevice.ResourceFactory.CreateBuffer(new BufferDescription((uint)(vertices.Length * Vertex.SizeOf()), BufferUsage.VertexBuffer));
                graphicsDevice.UpdateBuffer(vertexBuffer, 0, vertices);

                var indexBuffer = graphicsDevice.ResourceFactory.CreateBuffer(new BufferDescription((uint)(indices.Length * sizeof(uint)), BufferUsage.IndexBuffer));
                graphicsDevice.UpdateBuffer(indexBuffer, 0, indices);

                // Successfully created Lod instance
                lodInstance = new Lod(vertexBuffer, indexBuffer, sections, lod.IsTwoSided);
                return true;
            }
            catch (Exception ex)
            {
                return false;
            }
        }

        public void Dispose()
        {
            VertexBuffer.Dispose();
            IndexBuffer.Dispose();
        }
    }
}
