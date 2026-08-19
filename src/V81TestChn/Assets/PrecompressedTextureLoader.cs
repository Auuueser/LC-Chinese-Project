using System;
using System.IO;
using UnityEngine;

namespace V81TestChn;

internal static class PrecompressedTextureLoader
{
    public static Texture2D? LoadBc1(
        string path,
        int width,
        int height,
        bool mipChain,
        out string? error) =>
        LoadCompressed(path, width, height, mipChain, TextureFormat.DXT1, 8, "DXT1/BC1", out error);

    public static Texture2D? LoadBc3(
        string path,
        int width,
        int height,
        bool mipChain,
        out string? error)
        => LoadCompressed(path, width, height, mipChain, TextureFormat.DXT5, 16, "DXT5/BC3", out error);

    public static Texture2D? LoadBc7(
        string path,
        int width,
        int height,
        bool mipChain,
        out string? error)
        => LoadCompressed(path, width, height, mipChain, TextureFormat.BC7, 16, "BC7", out error);

    private static Texture2D? LoadCompressed(
        string path,
        int width,
        int height,
        bool mipChain,
        TextureFormat format,
        int bytesPerBlock,
        string formatName,
        out string? error)
    {
        error = null;
        Texture2D? texture = null;
        try
        {
            if (!SystemInfo.SupportsTextureFormat(format))
            {
                error = $"{formatName} is not supported by this graphics device";
                return null;
            }

            var file = new FileInfo(path);
            if (!file.Exists)
            {
                error = "precompressed texture file is missing";
                return null;
            }

            var expectedBytes = CalculateCompressedByteCount(width, height, mipChain, bytesPerBlock);
            if (file.Length != expectedBytes)
            {
                error = $"expected {expectedBytes} bytes, got {file.Length}";
                return null;
            }

            var data = File.ReadAllBytes(path);
            texture = new Texture2D(width, height, format, mipChain);
            texture.LoadRawTextureData(data);
            texture.Apply(updateMipmaps: false, makeNoLongerReadable: true);
            return texture;
        }
        catch (Exception ex)
        {
            DestroyUnityObject(texture);
            error = $"{ex.GetType().Name}: {ex.Message}";
            return null;
        }
    }

    public static long CalculateBc3ByteCount(int width, int height, bool mipChain)
        => CalculateCompressedByteCount(width, height, mipChain, 16);

    public static long CalculateBc7ByteCount(int width, int height, bool mipChain)
        => CalculateCompressedByteCount(width, height, mipChain, 16);

    public static long CalculateBc1ByteCount(int width, int height, bool mipChain)
        => CalculateCompressedByteCount(width, height, mipChain, 8);

    private static long CalculateCompressedByteCount(
        int width,
        int height,
        bool mipChain,
        int bytesPerBlock)
    {
        long total = 0;
        while (true)
        {
            total += (long)Math.Max(1, (width + 3) / 4) * Math.Max(1, (height + 3) / 4) * bytesPerBlock;
            if (!mipChain || (width == 1 && height == 1))
            {
                return total;
            }

            width = Math.Max(1, width / 2);
            height = Math.Max(1, height / 2);
        }
    }

    private static void DestroyUnityObject(UnityEngine.Object? value)
    {
        if (value == null)
        {
            return;
        }

        if (Application.isPlaying)
        {
            UnityEngine.Object.Destroy(value);
        }
        else
        {
            UnityEngine.Object.DestroyImmediate(value);
        }
    }
}
