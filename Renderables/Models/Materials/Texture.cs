using System.Numerics;
using System.Reflection;
using CUE4Parse_Conversion.Textures;
using CUE4Parse.UE4.Assets.Exports.Texture;
using SkiaSharp;
using StbImageSharp;
using Veldrid;
using PixelFormat = Veldrid.PixelFormat;

namespace UniversalUmap.Rendering.Renderables.Models.Materials
{
    public class Texture : IDisposable
    {
        private bool IsDisposed;

        public Veldrid.Texture VeldridTexture { get; protected set; }
        
        protected Texture() { }

        public Texture(GraphicsDevice graphicsDevice, UTexture texture)
        {
            try
            {
                InitializeTextureFromUTexture(graphicsDevice, texture);
            }
            catch
            {
                if(VeldridTexture != null)
                    VeldridTexture.Dispose();
                InitializeTextureFromColor(graphicsDevice, new Vector4(1.0f));
            }
        }

        public Texture(GraphicsDevice graphicsDevice, Vector4 color)
        {
            InitializeTextureFromColor(graphicsDevice, color);
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

        private void InitializeTextureFromUTexture(GraphicsDevice graphicsDevice, UTexture texture)
        {
            // Decode the texture data to SKBitmap
            var skBitmap = texture.Decode(RenderContext.TexturePlatform).ToSkBitmap();
            if (skBitmap == null)
                return;
            
            // Determine the PixelFormat based on SKBitmap ColorType
            PixelFormat textureFormat = PixelFormat.R8G8B8A8UNorm;  // Default format
            switch (skBitmap.ColorType)
            {
                case SKColorType.Rgba8888:
                    textureFormat = texture.SRGB ? PixelFormat.R8G8B8A8UNormSRgb : PixelFormat.R8G8B8A8UNorm;
                    break;

                case SKColorType.Bgra8888:
                    textureFormat = texture.SRGB ? PixelFormat.B8G8R8A8UNormSRgb : PixelFormat.B8G8R8A8UNorm;
                    break;

                case SKColorType.Gray8:
                    textureFormat = PixelFormat.R8UNorm;
                    break;

                case SKColorType.Alpha8:
                    textureFormat = PixelFormat.R8UNorm;
                    break;
                default:
                    throw new NotImplementedException($"Unsupported SKColorType: {skBitmap.ColorType}");
            }

            // Create Veldrid Texture using the determined format
            VeldridTexture = graphicsDevice.ResourceFactory.CreateTexture(new TextureDescription
            {
                Width = (uint)texture.PlatformData.SizeX,
                Height = (uint)texture.PlatformData.SizeY,
                Depth = 1,
                MipLevels = (uint)Math.Min(6, texture.PlatformData.Mips.Length),
                ArrayLayers = 1,
                Format = textureFormat,
                Type = TextureType.Texture2D,
                SampleCount = TextureSampleCount.Count1,
                Usage = TextureUsage.Sampled | TextureUsage.GenerateMipmaps
            });

            // Update the texture with the SKBitmap bytes
            graphicsDevice.UpdateTexture(VeldridTexture, skBitmap.Bytes, 0, 0, 0, (uint)skBitmap.Width, (uint)skBitmap.Height, 1, 0, 0);
            
            // Generate mipmaps
            var commandList = graphicsDevice.ResourceFactory.CreateCommandList();
            commandList.Begin();
            commandList.GenerateMipmaps(VeldridTexture);
            commandList.End();
            graphicsDevice.SubmitCommands(commandList);
            commandList.Dispose();
        }
        
        protected T LoadTexture<T>(string sourceName, string extension) where T : class
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
            if (IsDisposed)
                return;

            VeldridTexture.Dispose();
            IsDisposed = true;
        }
    }
}