using System.Numerics;
using System.Text.RegularExpressions;
using CUE4Parse.UE4.Assets.Exports;
using CUE4Parse.UE4.Assets.Exports.Material;
using CUE4Parse.UE4.Assets.Exports.Texture;
using CUE4Parse.UE4.Assets.Objects;
using CUE4Parse.UE4.Objects.UObject;
using UniversalUmap.Rendering.Resources;
using Veldrid;

namespace UniversalUmap.Rendering.Renderables.Models.Materials;

public class Material : IDisposable
{
    private readonly GraphicsDevice GraphicsDevice;
    private readonly CommandList CommandList;
    private readonly ResourceSet ResourceSet;
    public bool? TwoSided;

    public Material(GraphicsDevice graphicsDevice, CommandList commandList, ResourceLayout materialResourceLayout, UObject material)
    {
        GraphicsDevice = graphicsDevice;
        CommandList = commandList;

        var textures = new Dictionary<string, UTexture>();
        var referenceTextures = new Dictionary<string, UTexture>();

        LoadUMaterial(material, textures, referenceTextures);

        var color = GetOrDefault("Color", new Vector4(0.18f), textures, referenceTextures).VeldridTexture;
        var metallic = GetOrDefault("Metallic", new Vector4(0.0f), textures, referenceTextures).VeldridTexture;
        var specular = GetOrDefault("Specular", new Vector4(0.5f), textures, referenceTextures).VeldridTexture;
        var roughness = GetOrDefault("Roughness", new Vector4(0.8f), textures, referenceTextures).VeldridTexture;
        var ao = GetOrDefault("AO", new Vector4(1.0f), textures, referenceTextures).VeldridTexture;
        var normal = GetOrDefault("Normal", new Vector4(0.5f, 0.5f, 1.0f, 1.0f), textures, referenceTextures).VeldridTexture;
        var alpha = GetOrDefault("Alpha", new Vector4(1.0f), textures, referenceTextures).VeldridTexture;
        var emissive = GetOrDefault("Emissive", new Vector4(0.0f), textures, referenceTextures).VeldridTexture;
        
        ResourceSet = graphicsDevice.ResourceFactory.CreateResourceSet(new ResourceSetDescription(
            materialResourceLayout, color, metallic, specular, roughness, ao, normal, alpha, emissive
        ));
        
        textures.Clear();
        referenceTextures.Clear();
    }

    private void LoadUMaterial(UObject material, Dictionary<string, UTexture> textures, Dictionary<string, UTexture> referenceTextures)
    {
        UObject obj = material;
        while (obj != null)
        {
            if (obj is UMaterialInstanceConstant parentInstanceConstant)
            {
                ReadUMaterialInstanceConstantParams(parentInstanceConstant, textures);
                obj = parentInstanceConstant.Parent;
            }
            else if (obj is UMaterialInstance parentInstance)
            {
                ReadUMaterialInstanceParams(parentInstance, textures);
                obj = parentInstance.Parent;
            }
            else if (obj is UMaterial parentMaterial)
            {
                ReadUMaterialParams(parentMaterial, textures, referenceTextures);
                break;
            }
            else
                break;
        }
        
        TwoSided ??= false;
    }

    private void ReadUMaterialInstanceConstantParams(UMaterialInstanceConstant material, Dictionary<string, UTexture> textures)
    {
        if (material.TryGetValue(out FStructFallback basePropertyOverrides, "BasePropertyOverrides"))
            if (basePropertyOverrides.TryGetValue(out bool twoSided, "TwoSided"))
                TwoSided ??= twoSided;
        
        foreach (var textureParam in material.TextureParameterValues)
            if (textureParam != null && textureParam.ParameterValue.TryLoad(out UTexture texture))
                AddOrSkipProperty(textures, textureParam.Name, texture);
    }

    private void ReadUMaterialInstanceParams(UMaterialInstance material, Dictionary<string, UTexture> textures)
    {
        if (material.TryGetValue(out FStructFallback basePropertyOverrides, "BasePropertyOverrides"))
            if (basePropertyOverrides.TryGetValue(out bool twoSided, "TwoSided"))
                TwoSided ??= twoSided;
    }

    private void ReadUMaterialParams(UMaterial material, Dictionary<string, UTexture> textures, Dictionary<string, UTexture> referenceTextures)
    {
        TwoSided ??= material.TwoSided;
        
        if (material.CachedExpressionData != null && material.CachedExpressionData.TryGetValue(out FStructFallback cachedParameters, "Parameters")  && cachedParameters != null && cachedParameters.TryGetAllValues(out FStructFallback[] runtimeEntries, "RuntimeEntries"))
            if (cachedParameters.TryGetValue(out FPackageIndex[] textureValues, "TextureValues") && textureValues != null && runtimeEntries.Length > 2 && runtimeEntries[2] != null && runtimeEntries[2].TryGetValue(out FMaterialParameterInfo[] textureInfos, "ParameterInfos")  && textureInfos != null)
                for (var i = 0; i < textureInfos.Length && i < textureValues.Length; i++)
                    if (textureValues[i] != null && textureValues[i].TryLoad(out UTexture texture))
                        AddOrSkipProperty(textures, textureInfos[i].Name.Text, texture);

        //Ref Textures
        foreach (var texture in material.ReferencedTextures)
            if(texture != null) //this can be null!!
                AddOrSkipProperty(referenceTextures, texture.Name, texture);

        //Expressions
        for (var i = 0; i < material.Expressions.Length; i++)
            if (material.Expressions[i].TryLoad(out var expression))
                switch (expression)
                {
                    case UMaterialExpressionTextureSampleParameter textureSampleParam:
                        if (textureSampleParam.Texture != null)
                            AddOrSkipProperty(textures, textureSampleParam.ParameterName == "None" ? "Texture" + i : textureSampleParam.ParameterName.Text, textureSampleParam.Texture);
                        break;
                    case UMaterialExpressionTextureSample textureSample:
                        if (textureSample.Texture != null)
                            AddOrSkipProperty(referenceTextures, textureSample.Texture.Name, textureSample.Texture);
                        break;
                }
    }

    private void AddOrSkipProperty<TValue>(Dictionary<string, TValue> dictionary, string key, TValue value)
    {
        if (key == null || value == null || dictionary.ContainsKey(key))
            return;
        dictionary.Add(key, value);
    }
    
    private Texture GetOrDefault(string parameterName, Vector4 defaultColor, Dictionary<string, UTexture> textures, Dictionary<string, UTexture> referenceTextures)
    {
        var item = RenderContext.AutoTextureItems.FirstOrDefault(item => item.Parameter == parameterName);
        if (item != null && !string.IsNullOrWhiteSpace(item.Name))
        {
            try
            {
                var names = new Regex(item.Name, RegexOptions.IgnoreCase);
                var blacklist = string.IsNullOrWhiteSpace(item.Blacklist) ? new Regex(".^$") : new Regex(item.Blacklist, RegexOptions.IgnoreCase);
                foreach (var texParam in textures)
                    if (names.IsMatch(texParam.Key) && !blacklist.IsMatch(texParam.Key))
                        return ResourceCache.GetOrAdd(texParam.Value.Owner!.Name, () => new Texture(GraphicsDevice, texParam.Value));
                foreach (var texParam in referenceTextures)
                    if (names.IsMatch(texParam.Key) && !blacklist.IsMatch(texParam.Key))
                        return ResourceCache.GetOrAdd(texParam.Value.Owner!.Name, () => new Texture(GraphicsDevice, texParam.Value));
            }
            catch (Exception e)
            {
                // ignored
            }
        }
        return ResourceCache.GetOrAdd("Fallback_" + parameterName, ()=> new Texture(GraphicsDevice, defaultColor));
    }

    public void Render()
    {
        CommandList.SetGraphicsResourceSet(3, ResourceSet);
    }

    public void Dispose()
    {
        ResourceSet.Dispose();
    }
}