using CUE4Parse_Conversion.Meshes;
using CUE4Parse_Conversion.Meshes.PSK;
using CUE4Parse.UE4.Assets.Exports;
using CUE4Parse.UE4.Assets.Exports.StaticMesh;
using UniversalUmap.Rendering.Renderables.Models.Materials;
using UniversalUmap.Rendering.Resources;
using Veldrid;

namespace UniversalUmap.Rendering.Renderables.Models;

public class Model : IRenderable
{
    private readonly CommandList CommandList;
    
    public readonly Lod[] Lods;
    public readonly Material[] Materials;
    public readonly int LodIndex;

    public Model(GraphicsDevice graphicsDevice, CommandList commandList, ModelPipeline modelPipeline, UStaticMesh originalMesh)
    {
        CommandList = commandList;
        
        originalMesh.TryConvert(out CStaticMesh staticMesh);
        
        LodIndex = 0;
        Lods = new Lod[staticMesh.LODs.Count];
        foreach (var lod in staticMesh.LODs)
        {
            if (!lod.SkipLod)
            {
                Lods[0] = new Lod(graphicsDevice, lod);
                break;
            }
        }
        
        //Materials
        Materials = new Material[originalMesh.Materials.Length];
        for (var i = 0; i < Materials.Length; i++)
            if (originalMesh.Materials[i] != null && originalMesh.Materials[i].TryLoad(out var material))
                Materials[i] = ResourceCache.GetOrAdd(material.Outer!.Name, ()=> new Material(graphicsDevice, commandList, modelPipeline.MaterialResourceLayout, material));
    }
    
    public Model(GraphicsDevice graphicsDevice, CommandList commandList, ModelPipeline modelPipeline, CStaticMesh staticMesh, UObject material)
    {
        CommandList = commandList;
        
        LodIndex = 0;
        Lods = new Lod[staticMesh.LODs.Count];
        foreach (var lod in staticMesh.LODs)
        {
            if (!lod.SkipLod)
            {
                Lods[0] = new Lod(graphicsDevice, lod);
                break;
            }
        }
        
        //Materials
        Materials = new Material[1];
        Materials[0] = ResourceCache.GetOrAdd(material.Outer!.Name, ()=> new Material(graphicsDevice, commandList, modelPipeline.MaterialResourceLayout, material));
    }

    public void Render()
    {
        CommandList.SetVertexBuffer(0, Lods[LodIndex].VertexBuffer);
        CommandList.SetIndexBuffer(Lods[LodIndex].IndexBuffer, IndexFormat.UInt32);
    }

    public void Dispose()
    {
        foreach (var lod in Lods)
            lod.Dispose();
    }
}