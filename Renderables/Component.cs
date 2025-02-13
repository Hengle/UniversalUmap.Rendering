using System.Numerics;
using CUE4Parse_Conversion.Meshes;
using CUE4Parse_Conversion.Meshes.PSK;
using CUE4Parse.UE4.Assets.Exports;
using CUE4Parse.UE4.Assets.Exports.StaticMesh;
using CUE4Parse.UE4.Objects.Core.Math;
using UniversalUmap.Rendering.Camera;
using UniversalUmap.Rendering.Input;
using UniversalUmap.Rendering.Renderables.Models;
using UniversalUmap.Rendering.Renderables.Models.Materials;
using UniversalUmap.Rendering.Resources;
using Veldrid;

namespace UniversalUmap.Rendering.Renderables;

public class Component : IRenderable
{
    private readonly ModelPipeline ModelPipeline;
    private readonly GraphicsDevice GraphicsDevice;
    private readonly CommandList CommandList;
    private readonly ResourceSet CameraResourceSet;
    private readonly Frustum Frustum;

    
    private Models.Model Model;
    private Material[] OverrideMaterials;
    private bool TwoSided;
    
    private DeviceBuffer TransformBuffer;
    private Vector3[][] Bounds;
    private uint InstanceCount;

    public Component(Frustum frustum, ModelPipeline modelPipeline, GraphicsDevice graphicsDevice, CommandList commandList, ResourceSet cameraResourceSet, CStaticMesh staticMesh)
    {
        ModelPipeline = modelPipeline;
        GraphicsDevice = graphicsDevice;
        CommandList = commandList;
        CameraResourceSet = cameraResourceSet;
        Frustum = frustum;
        
        OverrideMaterials = [];

        TransformBuffer = GraphicsDevice.ResourceFactory.CreateBuffer(new BufferDescription(InstanceInfo.SizeOf(), BufferUsage.VertexBuffer));
        GraphicsDevice.UpdateBuffer(TransformBuffer, 0, new InstanceInfo(FTransform.Identity));
        InstanceCount = 1;
        
        Model = ResourceCache.GetOrAdd(staticMesh.GetHashCode().ToString(), ()=> new Model(GraphicsDevice, CommandList, ModelPipeline, staticMesh, staticMesh.LODs[0]?.Sections?.Value[0]?.Material?.Load()));
    }
    
    public Component(Frustum frustum, ModelPipeline modelPipeline, GraphicsDevice graphicsDevice, CommandList commandList, ResourceSet cameraResourceSet, UObject component, UStaticMesh staticMesh, FTransform[] transforms, UObject[] overrideMaterials)
    {
        ModelPipeline = modelPipeline;
        GraphicsDevice = graphicsDevice;
        CommandList = commandList;
        CameraResourceSet = cameraResourceSet;
        Frustum = frustum;
        
        OverrideMaterials = new Material[overrideMaterials.Length];
        for (var i = 0; i < OverrideMaterials.Length; i++)
            if (overrideMaterials[i] != null)
                OverrideMaterials[i] = ResourceCache.GetOrAdd(overrideMaterials[i].Outer!.Name, ()=> new Material(graphicsDevice, commandList, ModelPipeline.MaterialResourceLayout, overrideMaterials[i]));
        
        Model = ResourceCache.GetOrAdd(staticMesh.Outer!.Name, ()=> new Model(GraphicsDevice, CommandList, ModelPipeline, staticMesh));
        
        TwoSided = component.Outer!.GetOrDefault<bool>("bMirrored") || component.GetOrDefault<bool>("bDisallowMeshPaintPerInstance");
        
        var instanceInfos = new InstanceInfo[transforms.Length];
        for (var i = 0; i < transforms.Length; i++)
        {
            TwoSided |= (transforms[i].Scale3D.X < 0 || transforms[i].Scale3D.Y < 0 || transforms[i].Scale3D.Z < 0);
            instanceInfos[i] = new InstanceInfo(transforms[i]);
        }
        InstanceCount = (uint)transforms.Length;
        TransformBuffer = GraphicsDevice.ResourceFactory.CreateBuffer(new BufferDescription((uint)(instanceInfos.Length * InstanceInfo.SizeOf()), BufferUsage.VertexBuffer));
        GraphicsDevice.UpdateBuffer(TransformBuffer, 0, instanceInfos);
        
        Bounds = new Vector3[instanceInfos.Length][];
        for (var i = 0; i < instanceInfos.Length; i++)
            Bounds[i] = CalculateBounds(instanceInfos[i].Matrix, staticMesh.RenderData!.Bounds!);
    }
    
    public void Render()
    {
        if (Bounds != null)
        {
            //only render if at least one instance is visible
            var isVisible = false;
            for (var i = 0; i < InstanceCount; i++)
                if (IsInFrustum(Bounds[i]))
                    isVisible = true;

            if (!isVisible)
                return;
        }
        
        CommandList.SetPipeline(ModelPipeline.RegularPipeline);
        CommandList.SetGraphicsResourceSet(0, CameraResourceSet);
        CommandList.SetGraphicsResourceSet(1, ModelPipeline.AutoTextureResourceSet);
        CommandList.SetGraphicsResourceSet(2, ModelPipeline.CubeMapAndSamplerResourceSet);
        CommandList.SetVertexBuffer(1, TransformBuffer);
        
        Model.Render();
        
        foreach (var section in Model.Lods[Model.LodIndex].Sections)
        {
            var i = section.MaterialIndex;
            Material material = null;
            if (i >= 0 && i < OverrideMaterials.Length && OverrideMaterials[i] != null)
                material = OverrideMaterials[i];
            else if (i >= 0 && i < Model.Materials.Length && Model.Materials[i] != null)
                material = Model.Materials[i];

            if (material != null)
            {
                material.Render();
                TwoSided = TwoSided || (material.TwoSided ?? false) || Model.Lods[0].IsTwoSided;
            }
            
            CommandList.SetPipeline(TwoSided ? ModelPipeline.TwoSidedPipeline : ModelPipeline.RegularPipeline);
            CommandList.DrawIndexed(section.IndexCount, InstanceCount, section.FirstIndex, 0, 0);
        }
    }

    private double CalculateLODIndex(float relativeSize, int numberOfLODs)
    {
        float lodIndex = (1 - relativeSize) * (numberOfLODs - 1);
        lodIndex = lodIndex * lodIndex / (numberOfLODs - 1);  // Apply a soft quadratic scaling
        return lodIndex;
    }
    
    bool IsVisibleAndCalculateScreenSize(Vector4[] worldAABB, Matrix4x4 viewProjectionMatrix, out float relativeSize)
    {
        // Initialize bounds to extreme values to update with NDC values
        float minX = 1, minY = 1;
        float maxX = -1, maxY = -1;

        bool isInsideFrustum = false;
    
        foreach (var corner in worldAABB)
        {
            // Transform world AABB corner to clip space using the view-projection matrix
            Vector4 clipSpaceCorner = Vector4.Transform(corner, viewProjectionMatrix);

            // Convert from clip space to normalized device coordinates (NDC)
            float ndcX = clipSpaceCorner.X / clipSpaceCorner.W;
            float ndcY = clipSpaceCorner.Y / clipSpaceCorner.W;
            float ndcZ = clipSpaceCorner.Z / clipSpaceCorner.W;

            // Check if this corner is inside the frustum
            if (ndcX >= -1 && ndcX <= 1 && ndcY >= -1 && ndcY <= 1 && ndcZ >= -1 && ndcZ <= 1)
            {
                isInsideFrustum = true;
            }

            // Update bounds for calculating relative size later (expand to encompass all corners)
            minX = Math.Min(minX, ndcX);
            maxX = Math.Max(maxX, ndcX);
            minY = Math.Min(minY, ndcY);
            maxY = Math.Max(maxY, ndcY);
        }

        if (!isInsideFrustum)
        {
            relativeSize = 0;
            return false;
        }

        // Calculate width and height in NDC space (from -1 to 1 range)
        float width = (maxX - minX) / 2.0f;  // NDC width from [-1, 1] to [0, 1]
        float height = (maxY - minY) / 2.0f; // NDC height from [-1, 1] to [0, 1]

        // Relative size is the area of the AABB in screen space
        relativeSize = width * height;

        return true;
    }
    
    private bool IsInFrustum(Vector3[] corners)
    {
        var origin = corners[0];
        var sphereRadius = corners[1].X;
        foreach (var plane in Frustum.FrustumPlanes)
        {
            bool allOutside = true;
            if (Vector3.Dot(plane.Normal, origin) + plane.D >= -sphereRadius)
                allOutside = false;
            else //Sphere is intersecting, check if maybe all corners outside
            {
                for (var i = 2; i < corners.Length; i++)
                {
                    if (Vector3.Dot(plane.Normal, corners[i]) + plane.D >= 0)
                    {
                        allOutside = false;
                        break; //One corner inside, so all cant be outside
                    }
                }
            }
            if (allOutside)
                return false;
        }
        return true;
    }
    
    private Vector3[] CalculateBounds(Matrix4x4 transform, FBoxSphereBounds originalBounds)
    {
        var instanceBounds = new Vector3[10]; //also save origin and sphere radius per instance
        instanceBounds[0] = Vector3.Transform(
            new Vector3(
                originalBounds.Origin.X,
                originalBounds.Origin.Z,
                originalBounds.Origin.Y),
            transform
        );
        instanceBounds[1] = new Vector3(originalBounds.SphereRadius);
        var index = 2;
        int[] signs = [-1, 1];
        foreach (var signX in signs)
        foreach (var signY in signs)
        foreach (var signZ in signs)
        {
            var localCorner = originalBounds.Origin + new Vector3(
                originalBounds.BoxExtent.X * signX,
                originalBounds.BoxExtent.Y * signY,
                originalBounds.BoxExtent.Z * signZ
            );
            instanceBounds[index++] = Vector3.Transform(new Vector3(localCorner.X, localCorner.Z, localCorner.Y), transform);
        }
        return instanceBounds;
    }
    
    public void Dispose()
    {
        TransformBuffer.Dispose();
    }
}