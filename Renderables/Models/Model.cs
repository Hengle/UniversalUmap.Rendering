using CUE4Parse_Conversion.Meshes;
using CUE4Parse_Conversion.Meshes.PSK;
using CUE4Parse.UE4.Assets.Exports.StaticMesh;
using UniversalUmap.Rendering.Renderables.Models.Materials;
using UniversalUmap.Rendering.Resources;
using Veldrid;

namespace UniversalUmap.Rendering.Renderables.Models;

public class Model : IRenderable
{
    private readonly CommandList CommandList;

    public readonly int LodIndex = 0;

    public readonly Lod[] Lods;
    public readonly Material[] Materials;

    private Model(CommandList commandList, Lod[] lods, Material[] materials)
    {
        CommandList = commandList;
        Lods = lods;
        Materials = materials;
    }

    public static bool TryCreate(GraphicsDevice graphicsDevice, CommandList commandList, ModelPipeline modelPipeline, UStaticMesh originalMesh, out Model model)
    {
        model = null;
        try
        {
            if (!originalMesh.TryConvert(out CStaticMesh staticMesh) || staticMesh.LODs == null)
                return false;

            // LODs
            var lods = new List<Lod>();
            foreach (var lod in staticMesh.LODs)
                if (lod != null && !lod.SkipLod && Lod.TryCreate(graphicsDevice, lod, out var createdLod))
                    lods.Add(createdLod);

            if (lods.Count == 0)
                return false;

            // Materials
            var materials = new Material[originalMesh.Materials.Length];
            for (var i = 0; i < materials.Length; i++)
                if (originalMesh.Materials[i] != null && originalMesh.Materials[i].TryLoad(out var material) && material.Outer != null)
                    materials[i] = ResourceCache.GetOrAdd(material.Outer.Name, () => new Material(graphicsDevice, commandList, modelPipeline.MaterialResourceLayout, material));

            model = new Model(commandList, lods.ToArray(), materials);
        }
        catch
        {
            return false;
        }

        return true;
    }

    public static bool TryCreate(GraphicsDevice graphicsDevice, CommandList commandList, ModelPipeline modelPipeline, CStaticMesh staticMesh, out Model model)
    {
        model = null;
        try
        {
            // LODs
            if (!Lod.TryCreate(graphicsDevice, staticMesh.LODs[0], out var createdLod))
                return false;

            var material = staticMesh.LODs[0].Sections.Value[0].Material?.Load();
            if (material == null)
                return false;

            // Materials
            var materials = new[] { ResourceCache.GetOrAdd(material.Outer.Name, () => new Material(graphicsDevice, commandList, modelPipeline.MaterialResourceLayout, material)) };

            model = new Model(commandList, [createdLod], materials);
        }
        catch (Exception ex)
        {
            return false;
        }

        return true;
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