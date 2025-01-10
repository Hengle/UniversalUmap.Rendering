using System.Numerics;
using System.Runtime.CompilerServices;
using UniversalUmap.Rendering.Resources;
using Veldrid;

namespace UniversalUmap.Rendering.Renderables;

public class Grid : IRenderable
{
    private readonly CommandList CommandList;
    
    private readonly DeviceBuffer VertexBuffer;
    private readonly DeviceBuffer IndexBuffer;

    private readonly Pipeline Pipeline;
    private readonly ResourceSet ResourceSet;

    private readonly List<IDisposable> Disposables;

    public Grid(GraphicsDevice graphicsDevice, CommandList commandList,  OutputDescription outputDescription, DeviceBuffer cameraBuffer)
    {
        CommandList = commandList;
        Disposables = [];
        
        VertexBuffer = graphicsDevice.ResourceFactory.CreateBuffer(new BufferDescription((uint)Vertices.Length * (uint)Unsafe.SizeOf<Vector3>(), BufferUsage.VertexBuffer));
        Disposables.Add(VertexBuffer);
        graphicsDevice.UpdateBuffer(VertexBuffer, 0, Vertices);

        IndexBuffer = graphicsDevice.ResourceFactory.CreateBuffer(new BufferDescription((uint)Indices.Length * (uint)Unsafe.SizeOf<ushort>(), BufferUsage.IndexBuffer));
        Disposables.Add(IndexBuffer);
        graphicsDevice.UpdateBuffer(IndexBuffer, 0, Indices);
        
        var vertexLayout = new VertexLayoutDescription(
            new VertexElementDescription("Position", VertexElementSemantic.TextureCoordinate, VertexElementFormat.Float3)
        );
        
        var shaders = ShaderLoader.Load(graphicsDevice, "Grid");
        Disposables.Add(shaders[0]);
        Disposables.Add(shaders[1]);

        var resourceLayout = graphicsDevice.ResourceFactory.CreateResourceLayout(
            new ResourceLayoutDescription(
                new ResourceLayoutElementDescription("cameraUbo", ResourceKind.UniformBuffer, ShaderStages.Vertex | ShaderStages.Fragment)
            )
        );
        Disposables.Add(resourceLayout);
        var pipelineDescription = new GraphicsPipelineDescription(
            BlendStateDescription.SINGLE_ALPHA_BLEND,
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
            [resourceLayout],
            outputDescription
        );
        Pipeline = graphicsDevice.ResourceFactory.CreateGraphicsPipeline(ref pipelineDescription);
        Disposables.Add(Pipeline);
        ResourceSet = graphicsDevice.ResourceFactory.CreateResourceSet(new ResourceSetDescription(resourceLayout, cameraBuffer));
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
    
    private static readonly ushort[] Indices = [0, 1, 2, 1, 0, 3];
    private static readonly Vector3[] Vertices =
    [
        new(1f,  1f, 0f),
        new(-1f, -1f, 0f),
        new(-1f,  1f, 0f),
        new(1f, -1f, 0)
    ];
    
    public void Dispose()
    {
        Disposables.Reverse();
        foreach(var disposable in Disposables)
            disposable.Dispose();
    }
}
            
            