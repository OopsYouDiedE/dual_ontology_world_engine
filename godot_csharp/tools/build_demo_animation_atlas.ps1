param(
    [Parameter(Mandatory = $true)]
    [string[]]$Actor
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
Add-Type -ReferencedAssemblies System.Drawing.Common,System.Drawing.Primitives,System.Collections,System.Private.Windows.GdiPlus,System.Private.Windows.Core -TypeDefinition @'
using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Collections.Generic;

public static class DemoAnimationAtlasBuilder {
    static readonly string[] Dirs = { "front", "back", "left" };
    static readonly string[] Actions = { "idle", "walk", "talk", "inspect", "interact", "sit", "break", "cast" };
    static readonly int[] Counts = { 1, 8, 3, 4, 4, 4, 6, 7 };
    static readonly int[][] SourceRows = { new[] { 0 }, new[] { 1 }, new[] { 0 }, new[] { 0 }, new[] { 2 }, new[] { 2 }, new[] { 3 }, new[] { 4 } };
    static readonly int[][] SourceColumns = { new[] { 0 }, new[] { 0, 1, 2, 3, 4, 5, 6, 7 }, new[] { 1, 2, 3 }, new[] { 4, 5, 6, 7 }, new[] { 0, 1, 2, 3 }, new[] { 4, 5, 6, 7 }, new[] { 0, 1, 2, 3, 4, 5 }, new[] { 0, 1, 2, 3, 4, 5, 6 } };

    static Rectangle FindBounds(Bitmap image, int x0, int y0, int x1, int y1) {
        int minX = x1, minY = y1, maxX = -1, maxY = -1;
        for (int y = y0; y < y1; y++)
            for (int x = x0; x < x1; x++)
                if (image.GetPixel(x, y).A >= 192) {
                    minX = Math.Min(minX, x); minY = Math.Min(minY, y);
                    maxX = Math.Max(maxX, x); maxY = Math.Max(maxY, y);
                }
        return maxX < 0 ? Rectangle.Empty : Rectangle.FromLTRB(minX, minY, maxX + 1, maxY + 1);
    }

    static string RectText(Rectangle r) {
        return r.IsEmpty ? "empty" : "(" + r.X + "," + r.Y + "," + r.Width + "," + r.Height + ")";
    }

    // Painter's full sheet has uneven row gutters. Uniform cells can include a
    // shoe fragment from the preceding action. Select the principal vertical
    // ink band before registering its feet; keep all pixels inside that band.
    static Rectangle PainterFrameBounds(Bitmap image, int x0, int y0, int x1, int y1) {
        int start = -1, lastInk = -1, bestStart = y0, bestEnd = y0, bestHeight = 0;
        for (int y = y0; y <= y1 + 3; y++) {
            bool ink = false;
            if (y < y1) for (int x = x0; x < x1; x++)
                if (image.GetPixel(x, y).A >= 192) { ink = true; break; }
            if (ink) { if (start < 0) start = y; lastInk = y; }
            if (start >= 0 && y - lastInk >= 3) {
                int height = lastInk + 1 - start;
                if (height > bestHeight) { bestHeight = height; bestStart = start; bestEnd = lastInk + 1; }
                start = -1;
            }
        }
        return FindBounds(image, x0, bestStart, x1, bestEnd);
    }

    static int[] FindFiveRowEdges(Bitmap image, string path) {
        int[] edges = new int[6];
        edges[0] = 0; edges[5] = image.Height;
        for (int row = 1; row < 5; row++) {
            int theory = (int)Math.Round(row * image.Height / 5.0);
            int lo = Math.Max(0, (int)Math.Floor(theory - image.Height * 0.06));
            int hi = Math.Min(image.Height, (int)Math.Ceiling(theory + image.Height * 0.06));
            int bestStart = -1, bestLength = 0, runStart = -1;
            for (int y = lo; y < hi; y++) {
                bool clear = true;
                for (int x = 0; x < image.Width; x++)
                    if (image.GetPixel(x, y).A >= 192) { clear = false; break; }
                if (clear && runStart < 0) runStart = y;
                if ((!clear || y == hi - 1) && runStart >= 0) {
                    int end = clear && y == hi - 1 ? y + 1 : y;
                    int length = end - runStart;
                    if (length > bestLength) { bestLength = length; bestStart = runStart; }
                    runStart = -1;
                }
            }
            if (bestStart < 0) throw new InvalidDataException("No transparent row gutter near theoretical boundary " + row + "/5 in " + path);
            edges[row] = bestStart + bestLength / 2;
        }
        return edges;
    }

    static double FootCenterX(Bitmap image, Rectangle bounds) {
        int bandHeight = Math.Max(1, (int)Math.Ceiling(bounds.Height * 0.08));
        int footMin = bounds.Right, footMax = -1;
        int y0 = Math.Max(bounds.Top, bounds.Bottom - bandHeight);
        for (int y = y0; y < bounds.Bottom; y++)
            for (int x = bounds.Left; x < bounds.Right; x++)
                if (image.GetPixel(x, y).A >= 192) { footMin = Math.Min(footMin, x); footMax = Math.Max(footMax, x); }
        if (footMax < footMin) throw new InvalidDataException("No alpha pixels in the bottom 8% foot band.");
        return (footMin + footMax + 1) / 2.0;
    }

    public static string Build(string root, string actor) {
        string sourceDir = Path.Combine(root, "godot_csharp", "AssetsSource", "Internal", "Characters", "DemoAnimation");
        string approvedAtlasPath = Path.Combine(sourceDir, "approved-atlas", actor + ".png");
        if (File.Exists(approvedAtlasPath)) return BuildApprovedAtlas(root, actor, approvedAtlasPath);
        string fullPath = Path.Combine(sourceDir, actor + ".png");
        var sources = new Bitmap[3];
        var sourcePaths = new string[3];
        var individual = new bool[3];
        try {
            bool anyIndividual = false;
            for (int d = 0; d < 3; d++) {
                string directionPath = Path.Combine(sourceDir, actor + "-" + Dirs[d] + ".png");
                if (File.Exists(directionPath)) { sourcePaths[d] = directionPath; individual[d] = true; anyIndividual = true; }
                else if (File.Exists(fullPath)) sourcePaths[d] = fullPath;
                else throw new FileNotFoundException("Missing direction source and complete source atlas for " + actor + " " + Dirs[d], directionPath);
                sources[d] = new Bitmap(sourcePaths[d]);
                Bitmap image = sources[d];
                if (individual[d]) {
                    if (image.Width < 8 || image.Height < 5) throw new InvalidDataException(actor + " " + Dirs[d] + ": direction sheet is too small for an 8x5 layout: " + image.Width + "x" + image.Height);
                } else if (image.Width < 8 || image.Height < 24 || image.Width * 3 != image.Height) {
                    throw new InvalidDataException(actor + ": complete source dimensions must have a 1:3 aspect ratio and fit an 8x24 grid; got " + image.Width + "x" + image.Height + ".");
                }
            }
            var bounds = new Rectangle[3, 8, 8];
            var rowEdges = new int[3][];
            var report = new List<string>();
            report.Add("actor=" + actor + " alphaThreshold=192 outputLayout=8x24 directionSheets=" + anyIndividual);
            for (int d = 0; d < 3; d++) {
                Bitmap image = sources[d];
                rowEdges[d] = individual[d] ? FindFiveRowEdges(image, sourcePaths[d]) : null;
                report.Add(Dirs[d] + " source=" + sourcePaths[d] + " size=" + image.Width + "x" + image.Height + " layout=" + (individual[d] ? "8x5" : "8x24") + (individual[d] ? " rowEdges=" + String.Join(",", rowEdges[d]) : ""));
                for (int action = 0; action < 8; action++) {
                    for (int frame = 0; frame < 8; frame++) {
                        if (individual[d] && frame >= SourceColumns[action].Length) {
                            bounds[d, action, frame] = Rectangle.Empty;
                            report.Add(Dirs[d] + "/" + Actions[action] + " frame=" + frame + " unused (kept transparent; no mapped source cell)");
                            continue;
                        }
                        int sourceRow, sourceColumn;
                        int x0, x1, y0, y1;
                        if (individual[d]) {
                            sourceRow = SourceRows[action][0];
                            sourceColumn = SourceColumns[action][frame];
                            x0 = (int)Math.Round(sourceColumn * image.Width / 8.0);
                            x1 = (int)Math.Round((sourceColumn + 1) * image.Width / 8.0);
                            y0 = rowEdges[d][sourceRow]; y1 = rowEdges[d][sourceRow + 1];
                        } else {
                            sourceRow = d * 8 + action;
                            sourceColumn = frame;
                            x0 = (int)Math.Round(frame * image.Width / 8.0);
                            x1 = (int)Math.Round((frame + 1) * image.Width / 8.0);
                            y0 = (int)Math.Round(sourceRow * image.Height / 24.0);
                            y1 = (int)Math.Round((sourceRow + 1) * image.Height / 24.0);
                        }
                        bounds[d, action, frame] = actor == "painter" && !individual[d]
                            ? PainterFrameBounds(image, x0, y0, x1, y1)
                            : FindBounds(image, x0, y0, x1, y1);
                        bool required = frame < Counts[action];
                        string entry = Dirs[d] + "/" + Actions[action] + " frame=" + frame + " sourceCell=(" + sourceColumn + "," + sourceRow + ") bounds=" + RectText(bounds[d, action, frame]);
                        if (required && bounds[d, action, frame].IsEmpty) throw new InvalidDataException(actor + ": required frame is empty: " + entry + " source=" + sourcePaths[d]);
                        report.Add(entry + (required ? " required" : " unused (kept transparent)"));
                    }
                }
            }

            for (int d = 0; d < 3; d++) {
                Rectangle idle = bounds[d, 0, 0];
                double directionScale = 43.0 / idle.Height;
                report.Add(Dirs[d] + " idleHeight=" + idle.Height + " directionScale=" + directionScale.ToString("0.######", System.Globalization.CultureInfo.InvariantCulture));
            }

            using (var atlas = new Bitmap(512, 1536, PixelFormat.Format32bppArgb)) {
                var resources = new List<string>();
                var animations = new List<string>();
                int totalFrames = 0;
                for (int d = 0; d < 3; d++) {
                    Bitmap source = sources[d];
                    double scale = 43.0 / bounds[d, 0, 0].Height;
                    for (int a = 0; a < 8; a++) {
                        var entries = new List<string>();
                        for (int f = 0; f < Counts[a]; f++) {
                            Rectangle b = bounds[d, a, f];
                            double footX = FootCenterX(source, b);
                            int width = (int)Math.Round(b.Width * scale, MidpointRounding.AwayFromZero);
                            int height = (int)Math.Round(b.Height * scale, MidpointRounding.AwayFromZero);
                            int originX = 32 + (int)Math.Round((b.Left - footX) * scale, MidpointRounding.AwayFromZero);
                            int originY = 55 - height;
                            if (originX < 0 || originY < 0 || originX + width > 64 || originY + height > 64)
                                throw new InvalidDataException(actor + ": scaled frame exceeds 64x64 cell at " + Dirs[d] + "/" + Actions[a] + "/" + f + " target=(" + originX + "," + originY + "," + width + "," + height + ")");
                            int row = d * 8 + a;
                            for (int y = 0; y < height; y++) for (int x = 0; x < width; x++) {
                                int sx = b.Left + Math.Min(b.Width - 1, (int)((x + 0.5) * b.Width / width));
                                int sy = b.Top + Math.Min(b.Height - 1, (int)((y + 0.5) * b.Height / height));
                                Color c = source.GetPixel(sx, sy);
                                if (c.A >= 192) atlas.SetPixel(f * 64 + originX + x, row * 64 + originY + y, Color.FromArgb(255, c.R, c.G, c.B));
                            }
                            string id = "f_" + d + "_" + a + "_" + f;
                            resources.Add("[sub_resource type=\"AtlasTexture\" id=\"" + id + "\"]\natlas = ExtResource(\"1\")\nregion = Rect2(" + (f * 64) + ", " + (row * 64) + ", 64, 64)\n");
                            entries.Add("{\"duration\": 1.0, \"texture\": SubResource(\"" + id + "\")}");
                            report.Add(Dirs[d] + "/" + Actions[a] + " frame=" + f + " targetBounds=(" + originX + "," + originY + "," + width + "," + height + ") footAnchor=(32,55)");
                            totalFrames++;
                        }
                        bool loop = a == 0 || a == 1;
                        string speed = a == 1 ? "10.0" : "7.0";
                        animations.Add("{\"frames\": [" + String.Join(", ", entries) + "], \"loop\": " + (loop ? "true" : "false") + ", \"name\": &\"" + Actions[a] + "_" + Dirs[d] + "\", \"speed\": " + speed + "}");
                    }
                }
                string outDir = Path.Combine(root, "godot_csharp", "ShareAsset", "Internal", "Characters", "animation_v2");
                Directory.CreateDirectory(outDir);
                atlas.Save(Path.Combine(outDir, actor + ".png"), ImageFormat.Png);
                File.WriteAllText(Path.Combine(outDir, actor + ".tres"), "[gd_resource type=\"SpriteFrames\" load_steps=" + (totalFrames + 2) + " format=3]\n\n[ext_resource type=\"Texture2D\" path=\"res://ShareAsset/Internal/Characters/animation_v2/" + actor + ".png\" id=\"1\"]\n\n" + String.Join("\n", resources) + "\n[resource]\nanimations = [" + String.Join(",\n", animations) + "]\n");
                report.Add("validation=passed frames=" + totalFrames + " animations=24 atlas=512x1536 cell=64x64 anchor=(32,55)");
                string reportDir = Path.Combine(root, "artifacts", "demo-animation");
                Directory.CreateDirectory(reportDir);
                File.WriteAllLines(Path.Combine(reportDir, actor + "-bounds.txt"), report);
                return actor + ": generated " + totalFrames + " frame entries, 24 animations, 512x1536 atlas; bounds/validation: " + actor + "-bounds.txt";
            }
        } finally {
            for (int d = 0; d < sources.Length; d++) if (sources[d] != null) sources[d].Dispose();
        }
    }

    static string BuildApprovedAtlas(string root, string actor, string approvedPath) {
        byte[] atlasBytes = File.ReadAllBytes(approvedPath);
        var report = new List<string> {
            "actor=" + actor + " generationMode=approved-atlas-identity-copy outputLayout=8x24",
            "approvedAtlasSource=" + Path.GetFullPath(approvedPath),
            "upstreamImageGenSource=" + Path.GetFullPath(Path.Combine(Path.GetDirectoryName(Path.GetDirectoryName(approvedPath)), actor + ".png"))
        };
        var resources = new List<string>();
        var animations = new List<string>();
        int totalFrames = 0;
        using (var stream = new MemoryStream(atlasBytes))
        using (var atlas = new Bitmap(stream)) {
            if (atlas.Width != 512 || atlas.Height != 1536)
                throw new InvalidDataException(actor + ": approved atlas must be 512x1536; got " + atlas.Width + "x" + atlas.Height + ".");
            report.Add("approvedAtlasDimensions=" + atlas.Width + "x" + atlas.Height + " cell=64x64 anchor=(32,55)");
            for (int d = 0; d < 3; d++) {
                for (int a = 0; a < 8; a++) {
                    var entries = new List<string>();
                    int row = d * 8 + a;
                    for (int f = 0; f < Counts[a]; f++) {
                        Rectangle bounds = FindBounds(atlas, f * 64, row * 64, (f + 1) * 64, (row + 1) * 64);
                        if (bounds.IsEmpty)
                            throw new InvalidDataException(actor + ": approved atlas has an empty required cell: " + Dirs[d] + "/" + Actions[a] + "/" + f + ".");
                        string id = "f_" + d + "_" + a + "_" + f;
                        resources.Add("[sub_resource type=\"AtlasTexture\" id=\"" + id + "\"]\natlas = ExtResource(\"1\")\nregion = Rect2(" + (f * 64) + ", " + (row * 64) + ", 64, 64)\n");
                        entries.Add("{\"duration\": 1.0, \"texture\": SubResource(\"" + id + "\")}");
                        report.Add(Dirs[d] + "/" + Actions[a] + " frame=" + f + " atlasCell=(" + f + "," + row + ") bounds=" + RectText(bounds) + " preserved=true");
                        totalFrames++;
                    }
                    bool loop = a == 0 || a == 1;
                    string speed = a == 1 ? "10.0" : "7.0";
                    animations.Add("{\"frames\": [" + String.Join(", ", entries) + "], \"loop\": " + (loop ? "true" : "false") + ", \"name\": &\"" + Actions[a] + "_" + Dirs[d] + "\", \"speed\": " + speed + "}");
                }
            }
        }

        string outDir = Path.Combine(root, "godot_csharp", "ShareAsset", "Internal", "Characters", "animation_v2");
        Directory.CreateDirectory(outDir);
        File.WriteAllBytes(Path.Combine(outDir, actor + ".png"), atlasBytes);
        File.WriteAllText(Path.Combine(outDir, actor + ".tres"), "[gd_resource type=\"SpriteFrames\" load_steps=" + (totalFrames + 2) + " format=3]\n\n[ext_resource type=\"Texture2D\" path=\"res://ShareAsset/Internal/Characters/animation_v2/" + actor + ".png\" id=\"1\"]\n\n" + String.Join("\n", resources) + "\n[resource]\nanimations = [" + String.Join(",\n", animations) + "]\n");
        report.Add("validation=passed mode=approved-atlas-identity-copy frames=" + totalFrames + " animations=24 atlas=512x1536 cell=64x64 anchor=(32,55)");
        string reportDir = Path.Combine(root, "artifacts", "demo-animation");
        Directory.CreateDirectory(reportDir);
        File.WriteAllLines(Path.Combine(reportDir, actor + "-bounds.txt"), report);
        return actor + ": preserved approved atlas byte-for-byte; generated " + totalFrames + " frame entries and 24 animations; bounds/validation: " + actor + "-bounds.txt";
    }
}
'@

$atlasRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
foreach ($atlasActor in $Actor) {
    if ([string]::IsNullOrWhiteSpace($atlasActor) -or $atlasActor -notmatch '^[A-Za-z0-9_-]+$') {
        throw "Actor names may contain only letters, numbers, underscores, and hyphens: '$atlasActor'"
    }
    [DemoAnimationAtlasBuilder]::Build($atlasRoot, $atlasActor)
}
