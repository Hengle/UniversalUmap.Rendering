using System.Numerics;
using System.Runtime.CompilerServices;
using UniversalUmap.Rendering.Renderables.Models.Materials;
using UniversalUmap.Rendering.Resources;
using Veldrid;

namespace UniversalUmap.Rendering.Renderables;

public class SkyBox : IRenderable
{
    private readonly CommandList CommandList;
    
    private readonly DeviceBuffer VertexBuffer;
    private readonly DeviceBuffer IndexBuffer;

    private readonly Pipeline Pipeline;
    private readonly ResourceSet ResourceSet;

    private readonly List<IDisposable> Disposables;

    public SkyBox(GraphicsDevice graphicsDevice, CommandList commandList,  OutputDescription outputDescription, DeviceBuffer cameraBuffer)
    {
        CommandList = commandList;
        Disposables = [];
        
        VertexBuffer = graphicsDevice.ResourceFactory.CreateBuffer(new BufferDescription((uint)Vertices.Length * (uint)Unsafe.SizeOf<Vector3>(), BufferUsage.VertexBuffer));
        Disposables.Add(VertexBuffer);
        graphicsDevice.UpdateBuffer(VertexBuffer, 0, Vertices);

        IndexBuffer = graphicsDevice.ResourceFactory.CreateBuffer(new BufferDescription((uint)Indices.Length * (uint)Unsafe.SizeOf<ushort>(), BufferUsage.IndexBuffer));
        Disposables.Add(IndexBuffer);
        graphicsDevice.UpdateBuffer(IndexBuffer, 0, Indices);
        
        var textureCube = new TextureCube(graphicsDevice, ["radiance_posx", "radiance_negx", "radiance_posy", "radiance_negy", "radiance_posz", "radiance_negz"], true);
        Disposables.Add(textureCube);
        
        var vertexLayout = new VertexLayoutDescription(
            new VertexElementDescription("Position", VertexElementSemantic.TextureCoordinate, VertexElementFormat.Float3)
        );

        var shaders = ShaderLoader.Load(graphicsDevice, "SkyBox");
        Disposables.Add(shaders[0]);
        Disposables.Add(shaders[1]);

        var resourceLayout = graphicsDevice.ResourceFactory.CreateResourceLayout(
            new ResourceLayoutDescription(
            new ResourceLayoutElementDescription("cameraUbo", ResourceKind.UniformBuffer, ShaderStages.Vertex),
                new ResourceLayoutElementDescription("cubeTexture", ResourceKind.TextureReadOnly, ShaderStages.Fragment),
                new ResourceLayoutElementDescription("pointSampler", ResourceKind.Sampler, ShaderStages.Fragment)
            )
        );
        Disposables.Add(resourceLayout);
        var pipelineDescription = new GraphicsPipelineDescription(
            BlendStateDescription.SINGLE_DISABLED,
            new DepthStencilStateDescription
            {
                DepthTestEnabled = true,
                DepthWriteEnabled = false,
                DepthComparison = ComparisonKind.LessEqual,
            },
            new RasterizerStateDescription(
                FaceCullMode.None,
                PolygonFillMode.Solid,
                FrontFace.Clockwise,
                depthClipEnabled: true,
                scissorTestEnabled: false
            ),
            PrimitiveTopology.TriangleList,
            new ShaderSetDescription(
                [vertexLayout],
                shaders
            ),
            resourceLayout,
            outputDescription
        );
        Pipeline = graphicsDevice.ResourceFactory.CreateGraphicsPipeline(ref pipelineDescription);
        Disposables.Add(Pipeline);
        ResourceSet = graphicsDevice.ResourceFactory.CreateResourceSet(new ResourceSetDescription(resourceLayout, cameraBuffer, textureCube.VeldridTexture, graphicsDevice.Aniso4XSampler));
        Disposables.Add(ResourceSet);
    }

    public void Render()
    {
        CommandList.SetVertexBuffer(0, VertexBuffer);
        CommandList.SetIndexBuffer(IndexBuffer, IndexFormat.UInt16);
        CommandList.SetPipeline(Pipeline);
        CommandList.SetGraphicsResourceSet(0, ResourceSet);
        CommandList.DrawIndexed((uint)Indices.Length);
    }
    
     private static readonly Vector3[] Vertices =
     [
         // Top
            new Vector3(-100.0f,100.0f,-100.0f),
            new Vector3(100.0f,100.0f,-100.0f),
            new Vector3(100.0f,100.0f,100.0f),
            new Vector3(-100.0f,100.0f,100.0f),
            // Bottom
            new Vector3(-100.0f,-100.0f,100.0f),
            new Vector3(100.0f,-100.0f,100.0f),
            new Vector3(100.0f,-100.0f,-100.0f),
            new Vector3(-100.0f,-100.0f,-100.0f),
            // Left
            new Vector3(-100.0f,100.0f,-100.0f),
            new Vector3(-100.0f,100.0f,100.0f),
            new Vector3(-100.0f,-100.0f,100.0f),
            new Vector3(-100.0f,-100.0f,-100.0f),
            // Right
            new Vector3(100.0f,100.0f,100.0f),
            new Vector3(100.0f,100.0f,-100.0f),
            new Vector3(100.0f,-100.0f,-100.0f),
            new Vector3(100.0f,-100.0f,100.0f),
            // Back
            new Vector3(100.0f,100.0f,-100.0f),
            new Vector3(-100.0f,100.0f,-100.0f),
            new Vector3(-100.0f,-100.0f,-100.0f),
            new Vector3(100.0f,-100.0f,-100.0f),
            // Front
            new Vector3(-100.0f,100.0f,100.0f),
            new Vector3(100.0f,100.0f,100.0f),
            new Vector3(100.0f,-100.0f,100.0f),
            new Vector3(-100.0f,-100.0f,100.0f)
     ];
        private static readonly ushort[] Indices =
        [
            0,1,2, 0,2,3,
            4,5,6, 4,6,7,
            8,9,10, 8,10,11,
            12,13,14, 12,14,15,
            16,17,18, 16,18,19,
            20,21,22, 20,22,23
        ];
        
        public void Dispose()
        {
            Disposables.Reverse();
            foreach(var disposable in Disposables)
                disposable.Dispose();
        }
}
            
            