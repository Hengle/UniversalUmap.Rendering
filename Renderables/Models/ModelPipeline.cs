using UniversalUmap.Rendering.Renderables.Models.Materials;
using UniversalUmap.Rendering.Resources;
using Veldrid;
using Texture = UniversalUmap.Rendering.Renderables.Models.Materials.Texture;

namespace UniversalUmap.Rendering.Renderables.Models;

//cant be in Model like its done for Skybox etc since we will have multiple Model instances!
public class ModelPipeline : IDisposable
{
    private readonly GraphicsDevice GraphicsDevice;
    
    public readonly Pipeline RegularPipeline;
    public readonly Pipeline TwoSidedPipeline;

    public ResourceSet AutoTextureResourceSet;
    public ResourceSet CubeMapAndSamplerResourceSet;
    public ResourceLayout MaterialResourceLayout;

    private readonly List<IDisposable> Disposables;
    
    public ModelPipeline(GraphicsDevice graphicsDevice, ResourceLayout cameraResourceLayout, OutputDescription outputDescription, DeviceBuffer autoTextureBuffer)
    {
        GraphicsDevice = graphicsDevice;
        Disposables = [];
        
        //Create main pipeline
        var autoTextureResourceLayout = CreateAutoTextureResourceLayout(autoTextureBuffer);
        var textureSamplerResourceLayout = CreateTextureSamplerResourceLayout();
        var materialResourceLayout = CreateMaterialResourceLayout();
        var combinedResourceLayout = new[]
        {
            cameraResourceLayout, autoTextureResourceLayout, textureSamplerResourceLayout, materialResourceLayout
        };
        
        var vertexLayouts = CreateMainVertexLayouts();
        var shaders = ShaderLoader.Load(GraphicsDevice, "Model");
        Disposables.Add(shaders[0]);
        Disposables.Add(shaders[1]);
        var shaderSetDescription = new ShaderSetDescription(vertexLayouts, shaders);
        
        var depthStencilState = new DepthStencilStateDescription(
            depthTestEnabled: true,
            depthWriteEnabled: true,
            comparisonKind: ComparisonKind.LessEqual
        );
        var mainRasterizerDescription = new RasterizerStateDescription(
            cullMode: FaceCullMode.Back,
            fillMode: PolygonFillMode.Solid,
            frontFace: FrontFace.CounterClockwise,
            depthClipEnabled: true,
            scissorTestEnabled: false
        );
        RegularPipeline = GraphicsDevice.ResourceFactory.CreateGraphicsPipeline(
            new GraphicsPipelineDescription(
                BlendStateDescription.SINGLE_ALPHA_BLEND,
                depthStencilState,
                mainRasterizerDescription,
                PrimitiveTopology.TriangleList,
                shaderSetDescription,
                combinedResourceLayout,
                outputDescription
            )
        );
        Disposables.Add(RegularPipeline);
        //TWO SIDED PIPELINE
        var twoSidedRasterizerDescription = new RasterizerStateDescription(
            cullMode: FaceCullMode.None,
            fillMode: PolygonFillMode.Solid,
            frontFace: FrontFace.CounterClockwise,
            depthClipEnabled: true,
            scissorTestEnabled: false
        );
        TwoSidedPipeline = GraphicsDevice.ResourceFactory.CreateGraphicsPipeline(
            new GraphicsPipelineDescription(
                BlendStateDescription.SINGLE_ALPHA_BLEND,
                depthStencilState,
                twoSidedRasterizerDescription,
                PrimitiveTopology.TriangleList,
                shaderSetDescription,
                combinedResourceLayout,
                outputDescription
            )
        );
        Disposables.Add(TwoSidedPipeline);
    }
    
    private VertexLayoutDescription[] CreateMainVertexLayouts()
    {
        var vertexLayout = new VertexLayoutDescription(
            new VertexElementDescription("position", VertexElementSemantic.TextureCoordinate, VertexElementFormat.Float3),
            new VertexElementDescription("color", VertexElementSemantic.TextureCoordinate, VertexElementFormat.Float4),
            new VertexElementDescription("normal", VertexElementSemantic.TextureCoordinate, VertexElementFormat.Float3),
            new VertexElementDescription("tangent", VertexElementSemantic.TextureCoordinate, VertexElementFormat.Float3),
            new VertexElementDescription("uv", VertexElementSemantic.TextureCoordinate, VertexElementFormat.Float2)
        );
        //instance info layout
        var instanceLayout = new VertexLayoutDescription(
            new VertexElementDescription("matrixRow0", VertexElementSemantic.TextureCoordinate, VertexElementFormat.Float4),
            new VertexElementDescription("matrixRow1", VertexElementSemantic.TextureCoordinate, VertexElementFormat.Float4),
            new VertexElementDescription("matrixRow2", VertexElementSemantic.TextureCoordinate, VertexElementFormat.Float4),
            new VertexElementDescription("matrixRow3", VertexElementSemantic.TextureCoordinate, VertexElementFormat.Float4)
        );
        instanceLayout.InstanceStepRate = 1;
        return [vertexLayout, instanceLayout];
    }
    
    private ResourceLayout CreateTextureSamplerResourceLayout()
    {
        //resource layout
        var cubeMapAndSamplerResourceLayout = GraphicsDevice.ResourceFactory.CreateResourceLayout(new ResourceLayoutDescription(
            new ResourceLayoutElementDescription("irradianceTextureCube", ResourceKind.TextureReadOnly, ShaderStages.Fragment),
            new ResourceLayoutElementDescription("radianceTextureCube", ResourceKind.TextureReadOnly, ShaderStages.Fragment),
            new ResourceLayoutElementDescription("brdfLutTexture", ResourceKind.TextureReadOnly, ShaderStages.Fragment),
            new ResourceLayoutElementDescription("aniso4xSampler", ResourceKind.Sampler, ShaderStages.Fragment)
        ));
        Disposables.Add(cubeMapAndSamplerResourceLayout);
        
        var irradianceTextureCube = new TextureCube(GraphicsDevice, ["irradiance_posx", "irradiance_negx", "irradiance_posy", "irradiance_negy", "irradiance_posz", "irradiance_negz"]);
        Disposables.Add(irradianceTextureCube);
        
        var radianceTextureCube = new TextureCube(GraphicsDevice, ["radiance_posx", "radiance_negx", "radiance_posy", "radiance_negy", "radiance_posz", "radiance_negz"], true);
        Disposables.Add(radianceTextureCube);
        
        var brdfLutTexture = new Texture(GraphicsDevice, "ibl_brdf_lut", ".png");
        Disposables.Add(brdfLutTexture);
        
        //resource set
        CubeMapAndSamplerResourceSet = GraphicsDevice.ResourceFactory.CreateResourceSet(
            new ResourceSetDescription(cubeMapAndSamplerResourceLayout, irradianceTextureCube.VeldridTexture, radianceTextureCube.VeldridTexture, brdfLutTexture.VeldridTexture, GraphicsDevice.Aniso4XSampler));
        Disposables.Add(CubeMapAndSamplerResourceSet);
        return cubeMapAndSamplerResourceLayout;
    }
    
    private ResourceLayout CreateAutoTextureResourceLayout(DeviceBuffer autoTextureBuffer)
    {
        var autoTextureResourceLayout = GraphicsDevice.ResourceFactory.CreateResourceLayout(new ResourceLayoutDescription(
            new ResourceLayoutElementDescription("autoTextureUbo", ResourceKind.UniformBuffer, ShaderStages.Fragment))
        );
        Disposables.Add(autoTextureResourceLayout);
        // Create the resource set
        AutoTextureResourceSet = GraphicsDevice.ResourceFactory.CreateResourceSet(new ResourceSetDescription(autoTextureResourceLayout, autoTextureBuffer));
        Disposables.Add(AutoTextureResourceSet);
        return autoTextureResourceLayout;
    }
    
    private ResourceLayout CreateMaterialResourceLayout()
    {
        MaterialResourceLayout = GraphicsDevice.ResourceFactory.CreateResourceLayout(new ResourceLayoutDescription(
            new ResourceLayoutElementDescription("ColorTexture", ResourceKind.TextureReadOnly, ShaderStages.Fragment),
            new ResourceLayoutElementDescription("MetallicTexture", ResourceKind.TextureReadOnly, ShaderStages.Fragment),
            new ResourceLayoutElementDescription("SpecularTexture", ResourceKind.TextureReadOnly, ShaderStages.Fragment),
            new ResourceLayoutElementDescription("RoughnessTexture", ResourceKind.TextureReadOnly, ShaderStages.Fragment),
            new ResourceLayoutElementDescription("AoTexture", ResourceKind.TextureReadOnly, ShaderStages.Fragment),
            new ResourceLayoutElementDescription("NormalTexture", ResourceKind.TextureReadOnly, ShaderStages.Fragment),
            new ResourceLayoutElementDescription("AlphaTexture", ResourceKind.TextureReadOnly, ShaderStages.Fragment),
            new ResourceLayoutElementDescription("EmissiveTexture", ResourceKind.TextureReadOnly, ShaderStages.Fragment)
        ));
        Disposables.Add(MaterialResourceLayout);
        return MaterialResourceLayout;
    }
    
    public void Dispose()
    {
        Disposables.Reverse();
        foreach (var disposable in Disposables)
            disposable.Dispose();
    }
}