﻿using CUE4Parse.Utils;
using UniversalUmap.Rendering.Renderables.Models;
using UniversalUmap.Rendering.Renderables.Models.Materials;

namespace UniversalUmap.Rendering.Resources;

public static class ResourceCache
{
    private static readonly object Monitor = new();
    private static readonly Dictionary<string, Model> Meshes = new();
    private static readonly Dictionary<string, Material> Materials = new();
    private static readonly Dictionary<string, Texture> Textures = new();
    
    public static Model GetOrAdd(string key, Func<Model> valueFactory)
    {
        lock (Monitor)
            return Meshes.GetOrAdd(key, valueFactory);
    }
    
    public static Material GetOrAdd(string key, Func<Material> valueFactory)
    {
        lock (Monitor)
            return Materials.GetOrAdd(key, valueFactory);
    }

    public static Texture GetOrAdd(string key, Func<Texture> valueFactory)
    {
        lock (Monitor)
            return Textures.GetOrAdd(key, valueFactory);
    }

    public static void Clear()
    {
        lock (Monitor)
        {
            foreach (var mesh in Meshes)
                mesh.Value.Dispose();
            Meshes.Clear();
            
            foreach (var material in Materials)
                material.Value.Dispose();
            Materials.Clear();
            
            foreach (var texture in Textures)
                texture.Value.Dispose();
            Textures.Clear();
        }
    }
}