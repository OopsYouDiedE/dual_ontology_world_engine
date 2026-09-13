using Godot;
using System.Collections.Generic;

namespace DualOntologyWorldEngine.Character;

public static class DstSpriteLoader
{
    private const string WilsonPath = "res://ShareAsset/assets/character/wilson_official/";
    private const string FallbackPath = "res://ShareAsset/assets/character/dst_template/";
    private static readonly Dictionary<string, Texture2D> TextureCache = [];

    public static Texture2D? LoadTexture(string relativeOrResPath)
    {
        if (TextureCache.TryGetValue(relativeOrResPath, out var cached))
            return cached;

        string resPath = relativeOrResPath.StartsWith("res://")
            ? relativeOrResPath
            : WilsonPath + relativeOrResPath;

        Texture2D? texture = TryLoad(resPath);
        if (texture == null && !relativeOrResPath.StartsWith("res://"))
        {
            texture = TryLoad(FallbackPath + relativeOrResPath);
        }

        if (texture == null)
        {
            string globalPath = ProjectSettings.GlobalizePath(resPath);
            var image = Image.LoadFromFile(globalPath);
            if (image != null)
            {
                texture = ImageTexture.CreateFromImage(image);
            }
        }

        if (texture != null)
        {
            TextureCache[relativeOrResPath] = texture;
        }
        else
        {
            GD.PrintErr($"[DstSpriteLoader] Failed to load texture at {resPath}");
        }

        return texture;
    }

    private static Texture2D? TryLoad(string path)
    {
        if (ResourceLoader.Exists(path))
        {
            return ResourceLoader.Load<Texture2D>(path);
        }
        string globalPath = ProjectSettings.GlobalizePath(path);
        if (System.IO.File.Exists(globalPath))
        {
            var image = Image.LoadFromFile(globalPath);
            if (image != null)
            {
                return ImageTexture.CreateFromImage(image);
            }
        }
        return null;
    }
}
