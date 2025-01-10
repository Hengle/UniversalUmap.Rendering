using CUE4Parse_Conversion.Meshes.PSK;
using CUE4Parse.UE4.Assets;
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

    public Model(GraphicsDevice graphicsDevice, CommandList commandList, ModelPipeline modelPipeline, CStaticMesh staticMesh, ResolvedObject[] materials)
    {
        CommandList = commandList;

        Lods = new Lod[staticMesh.LODs.Count];
        for (var i = 0; i < staticMesh.LODs.Count; i++)
            Lods[i] = new Lod(graphicsDevice, staticMesh.LODs[i]);
        
        LodIndex = Lods.Length - 1;
        
        //Materials
        Materials = new Material[materials.Length];
        for (var i = 0; i < Materials.Length; i++)
            if(materials[i].TryLoad(out var material))
                Materials[i] = ResourceCache.GetOrAdd(material.Owner!.Name, ()=> new Material(graphicsDevice, commandList, modelPipeline.MaterialResourceLayout, material));
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