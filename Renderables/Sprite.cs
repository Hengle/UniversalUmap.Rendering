using System.Numerics;
using System.Runtime.CompilerServices;
using UniversalUmap.Rendering.Resources;
using Veldrid;

namespace UniversalUmap.Rendering.Renderables;

public class Sprite : IRenderable
{
    private readonly CommandList CommandList;
    
    private readonly DeviceBuffer VertexBuffer;

    private readonly Pipeline Pipeline;
    private readonly ResourceSet ResourceSet;
    private readonly ResourceSet CameraResourceSet;

    private readonly List<IDisposable> Disposables;

    public Sprite(GraphicsDevice graphicsDevice, CommandList commandList,  OutputDescription outputDescription, ResourceSet cameraResourceSet, ResourceLayout cameraResourceLayout, DeviceBuffer cameraBuffer)
    {
        CommandList = commandList;
        CameraResourceSet = cameraResourceSet;
        Disposables = [];
        
        VertexBuffer = graphicsDevice.ResourceFactory.CreateBuffer(new BufferDescription((uint)Vertices.Length * (uint)Unsafe.SizeOf<SimpleVertex>(), BufferUsage.VertexBuffer));
        Disposables.Add(VertexBuffer);
        graphicsDevice.UpdateBuffer(VertexBuffer, 0, Vertices);
        
        var vertexLayout = new VertexLayoutDescription(
            new VertexElementDescription("Position", VertexElementSemantic.TextureCoordinate, VertexElementFormat.Float2)
        );

        var shaders = ShaderLoader.Load(graphicsDevice, "Sprite");
        Disposables.Add(shaders[0]);
        Disposables.Add(shaders[1]);

        var resourceLayout = graphicsDevice.ResourceFactory.CreateResourceLayout(
            new ResourceLayoutDescription(
                new ResourceLayoutElementDescription("spriteParameters", ResourceKind.UniformBuffer, ShaderStages.Vertex)
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
            [cameraResourceLayout, resourceLayout],
            outputDescription
        );
        Pipeline = graphicsDevice.ResourceFactory.CreateGraphicsPipeline(ref pipelineDescription);
        Disposables.Add(Pipeline);
        
        ResourceSet = graphicsDevice.ResourceFactory.CreateResourceSet(new ResourceSetDescription(resourceLayout));
        Disposables.Add(ResourceSet);
    }

    public void Render()
    {
        CommandList.SetVertexBuffer(0, VertexBuffer);
        CommandList.SetPipeline(Pipeline);
        CommandList.SetGraphicsResourceSet(0, CameraResourceSet);
        CommandList.SetGraphicsResourceSet(1, ResourceSet);

        CommandList.Draw(4);
    }
    
    private static readonly Vector2[] Vertices =
    [
        new(-1, -1),
        new(1, -1),
        new(-1, 1),
        new(1, 1),
    ];
    
    public void Dispose()
    {
        Disposables.Reverse();
        foreach(var disposable in Disposables)
            disposable.Dispose();
    }
}