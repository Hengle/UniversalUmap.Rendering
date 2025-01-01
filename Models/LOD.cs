using System.Numerics;
using CUE4Parse_Conversion.Meshes.PSK;
using UniversalUmap.Rendering.Resources;
using Veldrid;

namespace UniversalUmap.Rendering.Models;

public class LOD : IDisposable
{
    public readonly DeviceBuffer VertexBuffer;
    public readonly DeviceBuffer IndexBuffer;
    public readonly Section[] Sections;
    public readonly bool IsTwoSided;

    public LOD(GraphicsDevice graphicsDevice, CStaticMeshLod lod)
    {
        IsTwoSided = lod.IsTwoSided;
        //vertex
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
        
        //index
        var indices = new uint[lod.Indices.Value.Length];
        for (var i = 0; i < lod.Indices.Value.Length; i++)
            indices[i] = (uint)lod.Indices.Value[i];

        //Sections
        Sections = new Section[lod.Sections.Value.Length];
        for (var i = 0; i < Sections.Length; i++)
            Sections[i] = new Section(lod.Sections.Value[i].MaterialIndex, lod.Sections.Value[i].FirstIndex, lod.Sections.Value[i].NumFaces);
        
        //vertex buffer
        VertexBuffer = graphicsDevice.ResourceFactory.CreateBuffer(new BufferDescription((uint)(vertices.Length * Vertex.SizeOf()), BufferUsage.VertexBuffer));
        graphicsDevice.UpdateBuffer(VertexBuffer, 0, vertices);
        
        //index buffer
        IndexBuffer = graphicsDevice.ResourceFactory.CreateBuffer(new BufferDescription((uint)(indices.Length * sizeof(uint)), BufferUsage.IndexBuffer));
        graphicsDevice.UpdateBuffer(IndexBuffer, 0, indices);
    }


    public void Dispose()
    {
        VertexBuffer.Dispose();
        IndexBuffer.Dispose();
    }
}