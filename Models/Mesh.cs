using CUE4Parse_Conversion.Meshes.PSK;
using CUE4Parse.UE4.Assets;
using UniversalUmap.Rendering.Models.Materials;
using UniversalUmap.Rendering.Resources;
using Veldrid;

namespace UniversalUmap.Rendering.Models;

public class Mesh : IDisposable
{
    private readonly CommandList CommandList;
    
    public readonly LOD[] Lods;
    public readonly Material[] Materials;

    public Mesh(GraphicsDevice graphicsDevice, CommandList commandList, ModelPipeline modelPipeline, CStaticMesh staticMesh, ResolvedObject[] materials)
    {
        CommandList = commandList;

        Lods = new LOD[staticMesh.LODs.Count];
        for (var i = 0; i < staticMesh.LODs.Count; i++)
            Lods[i] = new LOD(graphicsDevice, staticMesh.LODs[i]);
        
        //Materials
        Materials = new Material[materials.Length];
        for (var i = 0; i < Materials.Length; i++)
            if(materials[i].TryLoad(out var material))
                Materials[i] = ResourceCache.GetOrAdd(material.Owner!.Name, ()=> new Material(graphicsDevice, commandList, modelPipeline.MaterialResourceLayout, material));
    }
    
    
    public void Render(int lodIndex)
    {
        CommandList.SetVertexBuffer(0, Lods[lodIndex].VertexBuffer);
        CommandList.SetIndexBuffer(Lods[lodIndex].IndexBuffer, IndexFormat.UInt32);
    }

    public void Dispose()
    {
        foreach (var lod in Lods)
            lod.Dispose();
    }
}