﻿using System.Numerics;
using System.Text.RegularExpressions;
using CUE4Parse.UE4.Assets.Exports;
using CUE4Parse.UE4.Assets.Exports.Material;
using CUE4Parse.UE4.Assets.Exports.Texture;
using CUE4Parse.UE4.Assets.Objects;
using CUE4Parse.UE4.Objects.UObject;
using UniversalUmap.Rendering.Resources;
using Veldrid;

namespace UniversalUmap.Rendering.Models.Materials;

public class Material : IDisposable
{
    private readonly GraphicsDevice GraphicsDevice;
    private readonly CommandList CommandList;
    
    public bool TwoSided = false;
    private EBlendMode BlendMode = EBlendMode.BLEND_Opaque;
    private EMaterialShadingModel ShadingModel = EMaterialShadingModel.MSM_DefaultLit;
    
    private bool IsTwoSidedSet = false;
    private bool IsBlendModeSet = false;
    private bool IsShadingModelSet = false;

    private readonly Dictionary<string, UTexture> Textures = new();
    private readonly Dictionary<string, UTexture> ReferenceTextures = new();
    
    private readonly ResourceSet ResourceSet;
    
    public Material(GraphicsDevice graphicsDevice, CommandList commandList, ResourceLayout materialResourceLayout, UObject material)
    {
        GraphicsDevice = graphicsDevice;
        CommandList = commandList;
        
        LoadUMaterial(material);
        
        ResourceSet = graphicsDevice.ResourceFactory.CreateResourceSet(new ResourceSetDescription(
            materialResourceLayout,
            FindTexture("Color", new Vector4(0.18f)).VeldridTexture,
            FindTexture("Metallic", new Vector4(0.0f)).VeldridTexture,
            FindTexture("Specular", new Vector4(0.5f)).VeldridTexture,
            FindTexture("Roughness", new Vector4(0.8f)).VeldridTexture,
            FindTexture("AO", new Vector4(1.0f)).VeldridTexture,
            FindTexture("Normal", new Vector4(0.5f, 0.5f, 1, 1)).VeldridTexture,
            FindTexture("Alpha", new Vector4(1.0f)).VeldridTexture,
            FindTexture("Emissive", new Vector4(0.0f)).VeldridTexture
        ));
        
        Textures.Clear();
        ReferenceTextures.Clear();
    }

    private void LoadUMaterial(UObject material)
    {
        UObject obj = material;
        while (obj != null)
        {
            if (obj is UMaterialInstanceConstant parentInstance)
            {
                ReadUMaterialInstanceParams(parentInstance);
                obj = parentInstance.Parent;
            }
            else if (obj is UMaterial parentMaterial)
            {
                ReadUMaterialParams(parentMaterial);
                break;
            }
            else
                break;
        }
    }
    
    private void ReadUMaterialInstanceParams(UMaterialInstanceConstant material)
    {
        var basePropertyOverridesTag = material.Properties.FirstOrDefault(property => property.Name.Text == "BasePropertyOverrides");
        var basePropertyOverrides = basePropertyOverridesTag?.Tag?.GetValue(typeof(FStructFallback)) as FStructFallback;

        var twoSidedTag = basePropertyOverrides?.Properties.FirstOrDefault(property => property.Name.Text == "TwoSided");
        if (!IsTwoSidedSet)
        {
            TwoSided = (bool?)twoSidedTag?.Tag?.GenericValue ?? TwoSided;
            IsTwoSidedSet = true;
        }
        
        var blendModeTag = basePropertyOverrides?.Properties.FirstOrDefault(property => property.Name.Text == "BlendMode");
        if (!IsBlendModeSet)
        {
            if (Enum.TryParse(blendModeTag?.Tag?.GenericValue?.ToString(), out BlendMode))
                IsBlendModeSet = true;
        }
        
        var shadingModelTag = basePropertyOverrides?.Properties.FirstOrDefault(property => property.Name.Text == "ShadingModel");
        if (!IsShadingModelSet)
        {
            if (Enum.TryParse(shadingModelTag?.Tag?.GenericValue?.ToString(), out ShadingModel))
                IsShadingModelSet = true;
        }

        foreach (var textureParam in material.TextureParameterValues)
        {
            if (!textureParam.ParameterValue.TryLoad(out UTexture texture))
                continue;
            AddOrSkipProperty(Textures, textureParam.Name, texture);
        }
    }
    
    private void ReadUMaterialParams(UMaterial material)
    {
        if (!IsTwoSidedSet)
        {
            TwoSided = material.TwoSided;
            IsTwoSidedSet = true;
        }
    
        if (!IsBlendModeSet)
        {
            BlendMode = material.BlendMode;
            IsBlendModeSet = true;
        }
    
        if (!IsShadingModelSet)
        {
            ShadingModel = material.ShadingModel;
            IsShadingModelSet = true;
        }
        
        var cachedExpressionDataTag = material.Properties.FirstOrDefault(property => property.Name.Text == "CachedExpressionData");
        var cachedExpressionData = cachedExpressionDataTag?.Tag?.GetValue(typeof(FStructFallback)) as FStructFallback;
        var parametersTag = cachedExpressionData?.Properties.FirstOrDefault(property => property.Name.Text == "Parameters");
        var cachedParameters = parametersTag?.Tag?.GetValue(typeof(FStructFallback)) as FStructFallback;
        var textureValuesTag = cachedParameters?.Properties.FirstOrDefault(property => property.Name.Text == "TextureValues");
        var textureRefs = textureValuesTag?.Tag?.GetValue(typeof(FPackageIndex[])) as FPackageIndex[];

        UTexture[] textureValues = null;
        if (textureRefs != null)
        {
            textureValues = new UTexture[textureRefs.Length];
            for (var i = 0; i < textureRefs.Length; i++)
                textureValues[i] = textureRefs[i].Load<UTexture>(); 
        }
        
        var runtimeEntriesTags = cachedParameters?.Properties.Where(property => property.Name.Text == "RuntimeEntries").ToArray();
        if (runtimeEntriesTags != null)
            if (runtimeEntriesTags.Length > 2 && textureValues != null)
                WriteParameters(Textures, runtimeEntriesTags[2], textureValues);

        //Ref Textures
        foreach (var texture in material.ReferencedTextures)
            if(texture != null) //this can be null!!
                AddOrSkipProperty(ReferenceTextures, texture.Name, texture);
        
        //Expressions
        for (var i = 0; i < material.Expressions.Length; i++)
        {
            if (!material.Expressions[i].TryLoad(out var expression))
                continue;
            switch (expression)
            {
                case UMaterialExpressionTextureSampleParameter textureSampleParam:
                    if (textureSampleParam.Texture == null)
                        continue;
                    AddOrSkipProperty(Textures, textureSampleParam.ParameterName == "None" ? "Texture" + i : textureSampleParam.ParameterName.Text, textureSampleParam.Texture);
                    break;
                case UMaterialExpressionTextureSample textureSample:
                    if (textureSample.Texture == null)
                        continue;
                    AddOrSkipProperty(ReferenceTextures, textureSample.Texture.Name, textureSample.Texture);
                    break;
            }
        }
    }
    
    private void AddOrSkipProperty<TValue>(Dictionary<string, TValue> dictionary, string key, TValue value)
    {
        if (key == null || value == null || dictionary.ContainsKey(key))
            return;
        dictionary.Add(key, value);
    }

    private void WriteParameters<T>(Dictionary<string, T> dict, FPropertyTag runtimeEntryTag, T[] parameterValues)
    {
        var runtimeEntrySet = runtimeEntryTag.Tag?.GetValue(typeof(FStructFallback)) as FStructFallback;
        var parameterInfosTag = runtimeEntrySet?.Properties.FirstOrDefault(property => property.Name.Text == "ParameterInfos");
        var parameterInfos = parameterInfosTag?.Tag?.GetValue(typeof(FMaterialParameterInfo[])) as FMaterialParameterInfo[];
        if (parameterInfos == null) return;
        for (var i = 0; i < parameterInfos.Length && i < parameterValues.Length; i++)
            AddOrSkipProperty(dict, parameterInfos[i].Name.Text, parameterValues[i]);
    }

    private Texture FindTexture(string parameterName, Vector4 defaultColor)
    {
        var item = RenderContext.AutoTextureItems.FirstOrDefault(item => item.Parameter == parameterName);
        if (item != null && !string.IsNullOrWhiteSpace(item.Name))
        {
            try
            {
                var names = new Regex(item.Name, RegexOptions.IgnoreCase);
                var blacklist = string.IsNullOrWhiteSpace(item.Blacklist) ? new Regex(".^$") : new Regex(item.Blacklist, RegexOptions.IgnoreCase);
                foreach (var texParam in Textures)
                    if (names.IsMatch(texParam.Key) && !blacklist.IsMatch(texParam.Key))
                        return ResourceCache.GetOrAdd(texParam.Value.Owner!.Name, () => new Texture(GraphicsDevice, texParam.Value));
                foreach (var texParam in ReferenceTextures)
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