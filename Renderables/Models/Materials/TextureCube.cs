using StbImageSharp;
using Veldrid;
using PixelFormat = Veldrid.PixelFormat;
using Texture = UniversalUmap.Rendering.Renderables.Models.Materials.Texture;

namespace UniversalUmap.Rendering.Renderables.Models.Materials
{
    public class TextureCube : Texture
    {
        public TextureCube(GraphicsDevice graphicsDevice, string[] sourceNames, bool loadMips = false)
        {
            ImageResultFloat[] imageResultFloats = new ImageResultFloat[6];
            for (var i = 0; i < sourceNames.Length; i++)
                imageResultFloats[i] = LoadTexture<ImageResultFloat>(sourceNames[i], ".hdr");
            if (loadMips)
                InitializeTextureCubeMipsFromStbBitmaps(graphicsDevice, imageResultFloats);
            else
                InitializeTextureCubeFromStbBitmaps(graphicsDevice, imageResultFloats);
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
    }
}