using System.Numerics;
using System.Reflection;
using CUE4Parse_Conversion.Textures;
using CUE4Parse.UE4.Assets.Exports.Texture;
using SkiaSharp;
using StbImageSharp;
using Veldrid;
using PixelFormat = Veldrid.PixelFormat;

namespace UniversalUmap.Rendering.Models.Materials
{
    public class Texture : IDisposable
    {
        private bool IsDisposed;
        
        public Veldrid.Texture VeldridTexture { get; private set; }
        
    public Texture(GraphicsDevice graphicsDevice, UTexture texture)
    {
        try
        {
            InitializeTextureFromBitmap(graphicsDevice, texture, texture.Decode(RenderContext.TexturePlatform));
        }
        catch
        {
            InitializeTextureFromColor(graphicsDevice, new Vector4(1.0f));
        }
    }
    
    public Texture(GraphicsDevice graphicsDevice, Vector4 color)
    {
        InitializeTextureFromColor(graphicsDevice, color);
    }
    
    public Texture(GraphicsDevice graphicsDevice, string[] sourceNames, bool loadMips = false) //CUBEMAP
    {
        ImageResultFloat[] imageResultFloats = new ImageResultFloat[6];
        for(int i = 0; i < sourceNames.Length; i++)
            imageResultFloats[i] = LoadTexture<ImageResultFloat>(sourceNames[i], ".hdr");
        if (loadMips)
            InitializeTextureCubeMipsFromStbBitmaps(graphicsDevice, imageResultFloats);
        else
            InitializeTextureCubeFromStbBitmaps(graphicsDevice, imageResultFloats);
    }
    
    public Texture(GraphicsDevice graphicsDevice, string sourceName, string extension)
    {
        var imageResult = LoadTexture<ImageResult>(sourceName, extension);
        InitializeTextureFromStbBitmap(graphicsDevice, imageResult);
    }
    
    private void InitializeTextureFromStbBitmap(GraphicsDevice graphicsDevice, ImageResult bitmap)
    {
        VeldridTexture = graphicsDevice.ResourceFactory.CreateTexture(new TextureDescription
        {
            Width = (uint)bitmap.Width,
            Height = (uint)bitmap.Height,
            Depth = 1,
            MipLevels = 1,
            ArrayLayers = 1,
            Format = PixelFormat.R8G8B8A8UNorm,
            Type = TextureType.Texture2D,
            SampleCount = TextureSampleCount.Count1,
            Usage = TextureUsage.Sampled
        });
        graphicsDevice.UpdateTexture(VeldridTexture, bitmap.Data, 0, 0, 0, (uint)bitmap.Width, (uint)bitmap.Height, 1, 0, 0);
    }


    private void InitializeTextureFromBitmap(GraphicsDevice graphicsDevice, UTexture texture, SKBitmap bitmap)
    {
        VeldridTexture = graphicsDevice.ResourceFactory.CreateTexture(new TextureDescription
        {
            Width = (uint)bitmap.Width,
            Height = (uint)bitmap.Height,
            Depth = 1,
            MipLevels = 1, //TODO: Load all mips
            ArrayLayers = 1,
            Format = texture.SRGB ? PixelFormat.R8G8B8A8UNormSRgb : PixelFormat.R8G8B8A8UNorm,
            Type = TextureType.Texture2D,
            SampleCount = TextureSampleCount.Count1,
            Usage = TextureUsage.Sampled
        });
        graphicsDevice.UpdateTexture(VeldridTexture, bitmap.Bytes, 0, 0, 0, (uint)bitmap.Width, (uint)bitmap.Height, 1, 0, 0);


    }
    
    private T LoadTexture<T>(string sourceName, string extension) where T : class
    {
        var resourceName = $"UniversalUmap.Rendering.Assets.Textures.{sourceName}{extension}";
        using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName))
        {
            if (typeof(T) == typeof(ImageResultFloat))
                return ImageResultFloat.FromStream(stream, ColorComponents.RedGreenBlueAlpha) as T;
            if (typeof(T) == typeof(ImageResult))
                return ImageResult.FromStream(stream, ColorComponents.RedGreenBlueAlpha) as T;
        }
        return null;
    }
    
    private void InitializeTextureCubeMipsFromStbBitmaps(GraphicsDevice graphicsDevice, ImageResultFloat[] bitmaps)
    {
        var firstBitmap = bitmaps[0];
        var width = firstBitmap.Width;
        var height = firstBitmap.Height;

        var mipLevels = -1;
        int mipWidth = width;
        while (mipWidth > 1)
        {
            mipWidth >>= 1;
            mipLevels++;
        }

        VeldridTexture = graphicsDevice.ResourceFactory.CreateTexture(new TextureDescription
        {
            Width = (uint)height,
            Height = (uint)height,
            Depth = 1,
            MipLevels = (uint)mipLevels,
            ArrayLayers = 1,
            Format = PixelFormat.R32G32B32A32Float,
            Type = TextureType.Texture2D,
            SampleCount = TextureSampleCount.Count1,
            Usage = TextureUsage.Sampled | TextureUsage.Cubemap
        });

        for (uint cubeIndex = 0; cubeIndex < 6; cubeIndex++)
        {
            mipWidth = height;
            for (int mipLevel = 0; mipLevel < mipLevels; mipLevel++)
            {
                float[] mipData = ExtractMipData(bitmaps[cubeIndex], mipLevel, mipWidth);
                graphicsDevice.UpdateTexture(VeldridTexture, mipData, 0, 0, 0, (uint)mipWidth, (uint)mipWidth, 1, (uint)mipLevel, cubeIndex);
                mipWidth >>= 1; // Halve the width for the next mip level
            }
        }
    }
    
    private void InitializeTextureCubeFromStbBitmaps(GraphicsDevice graphicsDevice, ImageResultFloat[] bitmaps)
    {
        var firstBitmap = bitmaps[0];
        var width = (uint)firstBitmap.Width;
        var height = (uint)firstBitmap.Height;
        VeldridTexture = graphicsDevice.ResourceFactory.CreateTexture(new TextureDescription
        {
            Width = height,
            Height = height,
            Depth = 1,
            MipLevels = 1,
            ArrayLayers = 1,
            Format = PixelFormat.R32G32B32A32Float,
            Type = TextureType.Texture2D,
            SampleCount = TextureSampleCount.Count1,
            Usage = TextureUsage.Sampled | TextureUsage.Cubemap
        });

        for (uint cubeIndex = 0; cubeIndex < 6; cubeIndex++)
            graphicsDevice.UpdateTexture(VeldridTexture, bitmaps[cubeIndex].Data, 0, 0, 0, width, height, 1, 0, cubeIndex);
    }
    
    private float[] ExtractMipData(ImageResultFloat bitmap, int mipLevel, int mipWidth)
    {
        float[] mipData = new float[mipWidth * mipWidth * 4];
    
        int startY = 0;
        int startX = 0;
        for (int i = 0; i < mipLevel; ++i)
            startX += bitmap.Height / (1 << i);

        int mipIndex = 0;
        for (int y = 0; y < mipWidth; ++y)
            for (int x = 0; x < mipWidth; ++x)
            {
                int currentX = startX + x;
                int currentY = startY + y;
                
                int index = (currentY * bitmap.Width + currentX) * 4;
            
                mipData[mipIndex++] = bitmap.Data[index];
                mipData[mipIndex++] = bitmap.Data[index + 1];
                mipData[mipIndex++] = bitmap.Data[index + 2];
                mipData[mipIndex++] = bitmap.Data[index + 3];
            }
        
        return mipData;
    }
    
    private void InitializeTextureFromColor(GraphicsDevice graphicsDevice, Vector4 color)
    {
        var bytes = new byte[4];
        bytes[0] = (byte)(color.X * 255); // R
        bytes[1] = (byte)(color.Y * 255); // G
        bytes[2] = (byte)(color.Z * 255); // B
        bytes[3] = (byte)(color.W * 255); // A

        VeldridTexture = graphicsDevice.ResourceFactory.CreateTexture(new TextureDescription
        {
            Width = 1,
            Height = 1,
            Depth = 1,
            MipLevels = 1,
            ArrayLayers = 1,
            Format = PixelFormat.R8G8B8A8UNorm,
            Type = TextureType.Texture2D,
            SampleCount = TextureSampleCount.Count1,
            Usage = TextureUsage.Sampled
        });
        graphicsDevice.UpdateTexture(VeldridTexture, bytes, 0, 0, 0, 1, 1, 1, 0, 0);
    }
        
        public void Dispose()
        {
            if(IsDisposed)
                return;
            
            VeldridTexture.Dispose();
            IsDisposed = true;
        }
    }
}
