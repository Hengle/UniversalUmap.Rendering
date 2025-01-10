using System.Collections.Concurrent;
using System.Diagnostics;
using System.Numerics;
using System.Runtime.CompilerServices;
using CUE4Parse_Conversion.Meshes.PSK;
using CUE4Parse.UE4.Assets.Exports;
using CUE4Parse.UE4.Assets.Exports.StaticMesh;
using CUE4Parse.UE4.Assets.Exports.Texture;
using CUE4Parse.UE4.Objects.Core.Math;
using UniversalUmap.Rendering.Extensions;
using UniversalUmap.Rendering.Input;
using UniversalUmap.Rendering.Renderables;
using UniversalUmap.Rendering.Renderables.Models;
using UniversalUmap.Rendering.Renderables.Models.Materials;
using UniversalUmap.Rendering.Resources;
using Veldrid;
using Veldrid.Sdl2;
using Texture = Veldrid.Texture;

namespace UniversalUmap.Rendering;

//Host this in an Avalonia NativeControlHost
public class RenderContext : IDisposable
{
    private readonly object Monitor;
    private readonly Stopwatch Stopwatch;
    private Thread RenderThread;
    private bool Exit;

    private ModelPipeline ModelPipeline;
    private readonly List<IRenderable> Models;
    private readonly ConcurrentQueue<Component> AdditionQueue;
    
    private Camera.Camera Camera;
    
    private SkyBox SkyBox;
    private Grid Grid;
    
    private Sdl2Window Window;
    private uint Width;
    private uint Height;
    
    private GraphicsDevice GraphicsDevice;
    private Swapchain SwapChain;
    private CommandList CommandList;
    
    private DeviceBuffer AutoTextureBuffer;

    private readonly TextureSampleCount SampleCount;
    private bool Vsync;

    private Texture OffscreenColor;
    private Framebuffer OffscreenFramebuffer;
    private Pipeline FullscreenQuadPipeline;
    private DeviceBuffer FullscreenQuadPositions;

    private Texture ResolvedColor;
    private ResourceSet ResolvedColorResourceSet;

    private readonly List<IDisposable> Disposables;
    private Texture OffscreenDepth;
    private ResourceLayout ResolvedColorResourceLayout;
    private DeviceBuffer WindowResolutionBuffer;

    //do this because UniversalUmap.Rendering is a seperate module
    public static ETexturePlatform TexturePlatform;
    public static AutoTextureItem[] AutoTextureItems;

    private static RenderContext instance;

    public static RenderContext GetInstance()
    {
        return instance ??= new RenderContext();
    }

    private RenderContext()
    {
        Disposables = [];
        Models = [];
        AdditionQueue = [];
        Width = 1280;
        Height = 1024;
        Monitor = new object();
        Stopwatch = new Stopwatch();
        SampleCount = TextureSampleCount.Count2; //MSAA
        Vsync = true;
        Exit = false;
    }

    public void SetVsync(bool value)
    {
        Vsync = value;
        SwapChain.SyncToVerticalBlank = Vsync;
        OnResized();
    }

    public IntPtr Initialize(IntPtr instanceHandle)
    {
        CreateGraphicsDevice();
        
        Camera = new Camera.Camera(GraphicsDevice, new Vector3(1f, 100f, 1f), new Vector3(0f, 100f, 200f), (float)Width/Height);

        var windowHandle = CreateWindowSwapChain(instanceHandle);

        CreateFullscreenQuadPipeline();
        
        CommandList = GraphicsDevice.ResourceFactory.CreateCommandList();
        Disposables.Add(CommandList);

        AutoTextureBuffer = GraphicsDevice.ResourceFactory.CreateBuffer(new BufferDescription(AutoTextureMasks.SizeOf(), BufferUsage.UniformBuffer));
        Disposables.Add(AutoTextureBuffer);
        
        ModelPipeline = new ModelPipeline(GraphicsDevice, Camera.CameraResourceLayout, OffscreenFramebuffer.OutputDescription, AutoTextureBuffer);
        Disposables.Add(ModelPipeline);
        
        SkyBox = new SkyBox(GraphicsDevice, CommandList, OffscreenFramebuffer.OutputDescription, Camera.CameraBuffer);
        Disposables.Add(SkyBox);
        
        Grid = new Grid(GraphicsDevice, CommandList, OffscreenFramebuffer.OutputDescription, Camera.CameraBuffer);
        Disposables.Add(Grid);
        
        RenderThread = new Thread(RenderLoop) { IsBackground = true };
        Stopwatch.Start();
        RenderThread.Start();
        
        return windowHandle;
    }
    
    public void LoadAutoTexture(AutoTextureItem[] autoTextureItems)
    {
        AutoTextureItems = autoTextureItems;
        var autoTextureMasks = new AutoTextureMasks();
        foreach (var item in AutoTextureItems)
        {
            var mask = new Vector4(item.R ? 1.0f : 0.0f, item.G ? 1.0f : 0.0f, item.B ? 1.0f : 0.0f, item.A ? 1.0f : 0.0f);
            switch (item.Parameter)
            {
                case "Color":
                    autoTextureMasks.Color = mask;
                    break;
                case "Metallic":
                    autoTextureMasks.Metallic = mask;
                    break;
                case "Specular":
                    autoTextureMasks.Specular = mask;
                    break;
                case "Roughness":
                    autoTextureMasks.Roughness = mask;
                    break;
                case "AO":
                    autoTextureMasks.AO = mask;
                    break;
                case "Normal":
                    autoTextureMasks.Normal = mask;
                    break;
                case "Emissive":
                    autoTextureMasks.Emissive = mask;
                    break;
                case "Alpha":
                    autoTextureMasks.Alpha = mask;
                    break;
            }
        }
        GraphicsDevice.UpdateBuffer(AutoTextureBuffer, 0, autoTextureMasks);
    }
    
    public void Load(UObject component, UStaticMesh mesh, FTransform[] transforms, UObject[] overrideMaterials)
    {
        Camera.JumpPosition = new Vector3(transforms[0].Translation.X, transforms[0].Translation.Z, transforms[0].Translation.Y);
        AdditionQueue.Enqueue(new Component(Camera.Frustum, ModelPipeline, GraphicsDevice, CommandList, Camera.CameraResourceSet, component, mesh, transforms, overrideMaterials));
    }
    
    public void Load(CStaticMesh mesh)
    {
        AdditionQueue.Enqueue(new Component(Camera.Frustum, ModelPipeline, GraphicsDevice, CommandList, Camera.CameraResourceSet, mesh));
    }

    public void Clear()
    {
        lock (Monitor)
        {
            foreach (var model in Models)
                model.Dispose();
            Models.Clear();
            ResourceCache.Clear();
        }
        Camera.JumpPosition = Vector3.Zero;
    }
    
    private IntPtr CreateWindowSwapChain(IntPtr instanceHandle)
    {
        Window = new Sdl2Window(
            "UniversalUmap 3D Viewer",
            0,
            0,
            (int)Width,
            (int)Height,
            SDL_WindowFlags.OpenGL | SDL_WindowFlags.Resizable ,
            false)
        {
            Visible = false,
            WindowState = WindowState.Normal
        };
        NativeWindowExtensions.MakeBorderless(Window.Handle);
        
        Window.MouseDown += @event =>
        {
            if (@event.MouseButton == MouseButton.Right)
            {
                InputTracker.UpdateRightClickMousePosition();
                Sdl2Native.SDL_SetRelativeMouseMode(true);
            }
        };
        Window.MouseUp += @event =>
        {
            if (@event.MouseButton == MouseButton.Right)
            {
                Sdl2Native.SDL_SetRelativeMouseMode(false);
                Window.SetMousePosition(InputTracker.RightClickMousePosition);
            }
        };

        Window.Resized += OnResized;
        
        var swapchainSource = SwapchainSource.CreateWin32(Window.Handle, instanceHandle);
        var swapchainDescription = new SwapchainDescription(
            swapchainSource,
            Width,
            Height,
            PixelFormat.R32Float,
            Vsync,
            false
        );
        SwapChain = GraphicsDevice.ResourceFactory.CreateSwapchain(ref swapchainDescription);
        Disposables.Add(SwapChain);
        return Window.Handle;
    }
    
    private void OnResized()
    {
        Width = (uint)Window.Width;
        Height = (uint)Window.Height;
        SwapChain.Resize(Width, Height);
        Camera.Resize(Width, Height);
        CreateOffscreenFramebuffer(recreate: true);
        CreateResolvedColorResourceSet(recreate: true);
        GraphicsDevice.UpdateBuffer(WindowResolutionBuffer, 0, new Vector4(1f / Width, 1f / Height, 0, 0));
    }

    private void CreateGraphicsDevice()
    {
        var options = new GraphicsDeviceOptions
        {
            PreferStandardClipSpaceYDirection = true,
            PreferDepthRangeZeroToOne = true
        };
        GraphicsDevice = GraphicsDevice.CreateD3D11(options);
        Disposables.Add(GraphicsDevice);
    }
    
    private void RenderLoop()
    {
        double previousTime = Stopwatch.Elapsed.TotalSeconds;
    
        //FPS Counter
        double fpsTimer = 0.0;
    
        while (!Exit)
        {
            double currentTime = Stopwatch.Elapsed.TotalSeconds;
            double deltaTime = currentTime - previousTime;
            previousTime = currentTime;
            
            Render(deltaTime);
            
#if DEBUG
            fpsTimer += deltaTime;
            if (fpsTimer >= 1.0)
            {
                Console.WriteLine($"FPS: {1 / deltaTime}");
                fpsTimer = 0.0;
            }
#endif
        }
    }
    
    private void Render(double deltaTime)
    {
        //Update input
        InputTracker.Update(Window);
        //Update Camera
        Camera.Update(deltaTime);
        
        CommandList.Begin();

        //Render to offscreen framebuffer
        CommandList.SetFramebuffer(OffscreenFramebuffer);
        CommandList.ClearDepthStencil(1);
        CommandList.ClearColorTarget(0, RgbaFloat.CLEAR);
        
        while (AdditionQueue.TryDequeue(out var model))
            lock(Monitor)
                Models.Add(model);
        
        lock(Monitor)
            foreach (var model in Models)
                model.Render();

        SkyBox.Render();
        Grid.Render();

        CommandList.ResolveTexture(OffscreenColor, ResolvedColor);
        
        CommandList.SetFramebuffer(SwapChain.Framebuffer);
        CommandList.ClearDepthStencil(1);
        CommandList.ClearColorTarget(0, RgbaFloat.CLEAR);

        //Set fullscreen quad pipeline
        CommandList.SetPipeline(FullscreenQuadPipeline);
        CommandList.SetVertexBuffer(0, FullscreenQuadPositions);

        CommandList.SetGraphicsResourceSet(0, ResolvedColorResourceSet);

        //Draw fullscreen quad
        CommandList.Draw(4);

        CommandList.End();
        GraphicsDevice.SubmitCommands(CommandList);

        //Present the image
        GraphicsDevice.SwapBuffers(SwapChain);
    }
    
    public void Dispose()
    {
        Exit = true;
        RenderThread.Join();
        
        Window.Close();
        
        Disposables.Reverse();
        foreach (var disposable in Disposables)
            disposable.Dispose();

        Clear();
        
        instance = null;
    }
    
    private VertexLayoutDescription[] CreateFullscreenQuadVertexLayouts()
    {
        var vertexLayoutPositions = new VertexLayoutDescription(new VertexElementDescription("Positions", VertexElementSemantic.TextureCoordinate, VertexElementFormat.Float2));
        return [vertexLayoutPositions];
    }
    
    private void CreateOffscreenFramebuffer(bool recreate = false)
    {
        if (recreate)
        {
            Disposables.Remove(OffscreenDepth);
            OffscreenDepth.Dispose();
        }
        OffscreenDepth = GraphicsDevice.ResourceFactory.CreateTexture(new TextureDescription
        {
            Width = SwapChain.Framebuffer.Width,
            Height = SwapChain.Framebuffer.Height,
            Depth = 1,
            MipLevels = 1,
            ArrayLayers = 1,
            Format = PixelFormat.R32Float,
            Type = TextureType.Texture2D,
            SampleCount = SampleCount,
            Usage = TextureUsage.DepthStencil
        });
        Disposables.Add(OffscreenDepth);
        
        if (recreate)
        {
            Disposables.Remove(OffscreenColor);
            OffscreenColor.Dispose();
        }
        OffscreenColor = GraphicsDevice.ResourceFactory.CreateTexture(new TextureDescription
        {
            Width = SwapChain.Framebuffer.Width,
            Height = SwapChain.Framebuffer.Height,
            Depth = 1,
            MipLevels = 1,
            ArrayLayers = 1,
            Format = PixelFormat.B8G8R8A8UNorm,
            Type = TextureType.Texture2D,
            SampleCount = SampleCount,
            Usage = TextureUsage.RenderTarget | TextureUsage.Sampled
        });
        Disposables.Add(OffscreenColor);
        
        if (recreate)
        {
            Disposables.Remove(OffscreenFramebuffer);
            OffscreenFramebuffer.Dispose();
        }
        OffscreenFramebuffer = GraphicsDevice.ResourceFactory.CreateFramebuffer(new FramebufferDescription(OffscreenDepth, OffscreenColor));
        Disposables.Add(OffscreenFramebuffer);
    }
    
    private void CreateResolvedColorResourceSet(bool recreate = false)
    {
        if (recreate)
        {
            Disposables.Remove(ResolvedColorResourceLayout);
            ResolvedColorResourceLayout.Dispose();
        }
        ResolvedColorResourceLayout = GraphicsDevice.ResourceFactory.CreateResourceLayout(new ResourceLayoutDescription(
            new ResourceLayoutElementDescription("TextureColor", ResourceKind.TextureReadOnly, ShaderStages.Fragment),
            new ResourceLayoutElementDescription("SamplerColor", ResourceKind.Sampler, ShaderStages.Fragment),
            new ResourceLayoutElementDescription("WindowResolution", ResourceKind.UniformBuffer, ShaderStages.Fragment)
        ));
        Disposables.Add(ResolvedColorResourceLayout);
        
        if (recreate)
        {
            Disposables.Remove(ResolvedColor);
            ResolvedColor.Dispose();
        }
        ResolvedColor = GraphicsDevice.ResourceFactory.CreateTexture(new TextureDescription
        {
            Width = SwapChain.Framebuffer.Width,
            Height = SwapChain.Framebuffer.Height,
            Depth = 1,
            MipLevels = 1,
            ArrayLayers = 1,
            Format = PixelFormat.B8G8R8A8UNorm,
            Type = TextureType.Texture2D,
            SampleCount = TextureSampleCount.Count1,
            Usage = TextureUsage.RenderTarget | TextureUsage.Sampled,
        });
        Disposables.Add(ResolvedColor);
        
        if (recreate)
        {
            Disposables.Remove(WindowResolutionBuffer);
            WindowResolutionBuffer.Dispose();
        }
        WindowResolutionBuffer = GraphicsDevice.ResourceFactory.CreateBuffer(new BufferDescription((uint)Unsafe.SizeOf<Vector4>(), BufferUsage.UniformBuffer));
        GraphicsDevice.UpdateBuffer(WindowResolutionBuffer, 0, new Vector4(1f / Width, 1f / Height, 0, 0));
        Disposables.Add(WindowResolutionBuffer);
        
        
        if (recreate)
        {
            Disposables.Remove(ResolvedColorResourceSet);
            ResolvedColorResourceSet.Dispose();
        }
        ResolvedColorResourceSet = GraphicsDevice.ResourceFactory.CreateResourceSet(new ResourceSetDescription(ResolvedColorResourceLayout, ResolvedColor, GraphicsDevice.PointSampler, WindowResolutionBuffer));
        Disposables.Add(ResolvedColorResourceSet);
    }

    private void CreateFullscreenQuadPipeline()
    {
        CreateOffscreenFramebuffer();
        CreateFullscreenQuadBuffers();
        
        var vertexLayouts = CreateFullscreenQuadVertexLayouts();
        var shaders = ShaderLoader.Load(GraphicsDevice, "FullscreenQuad");
        Disposables.Add(shaders[0]);
        Disposables.Add(shaders[1]);
        
        CreateResolvedColorResourceSet();
        var pipelineDescription = new GraphicsPipelineDescription(
            BlendStateDescription.SINGLE_ALPHA_BLEND,
            new DepthStencilStateDescription
            {
                DepthTestEnabled = true,
                DepthWriteEnabled = true,
                DepthComparison = ComparisonKind.LessEqual,
            },
            new RasterizerStateDescription
            {
                CullMode = FaceCullMode.None,
                FillMode = PolygonFillMode.Solid,
                FrontFace = FrontFace.CounterClockwise,
                DepthClipEnabled = true,
                ScissorTestEnabled = false,
            },
            PrimitiveTopology.TriangleStrip,
            new ShaderSetDescription(vertexLayouts, shaders),
            [ResolvedColorResourceLayout],
            SwapChain.Framebuffer.OutputDescription
        );
        FullscreenQuadPipeline = GraphicsDevice.ResourceFactory.CreateGraphicsPipeline(pipelineDescription);
        Disposables.Add(FullscreenQuadPipeline);
    }

    private void CreateFullscreenQuadBuffers()
    {
        var fullscreenQuadPositions = new Vector2[]
        {
            new(-1, -1),
            new(1, -1),
            new(-1, 1),
            new(1),
        };
        FullscreenQuadPositions = GraphicsDevice.ResourceFactory.CreateBuffer(new BufferDescription((uint)(Unsafe.SizeOf<Vector2>() * fullscreenQuadPositions.Length), BufferUsage.VertexBuffer));
        Disposables.Add(FullscreenQuadPositions);
        GraphicsDevice.UpdateBuffer(FullscreenQuadPositions, 0, fullscreenQuadPositions);
    }
}
