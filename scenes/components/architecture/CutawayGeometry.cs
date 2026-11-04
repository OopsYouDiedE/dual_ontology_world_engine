using Godot;

/// <summary>Cross sections of the actual mesh. No gameplay state or collision is changed.</summary>
public partial class CutawayGeometry : RefCounted
{
    public ArrayMesh? Build(Mesh mesh, Transform3D transform, float elevation, int axis, Vector3 outward)
    {
        var bounds = transform * mesh.GetAabb();
        if (elevation <= bounds.Position[axis] + 0.0001f || elevation >= bounds.End[axis] - 0.0001f) return null;
        var faces = mesh.GetFaces();
        var points = new Dictionary<(long, long), Vector3>();
        var neighbours = new Dictionary<(long, long), List<(long, long)>>();
        Span<Vector3> triangle = stackalloc Vector3[3];
        Span<Vector3> hits = stackalloc Vector3[3];
        Span<(long, long)> keys = stackalloc (long, long)[2];
        for (var i = 0; i < faces.Length; i += 3)
        {
            for (var j = 0; j < 3; j++) triangle[j] = transform * faces[i + j];
            var count = 0;
            for (var j = 0; j < 3; j++)
            {
                var a = triangle[j]; var b = triangle[(j + 1) % 3];
                if ((a[axis] < elevation) == (b[axis] < elevation)) continue;
                hits[count++] = a.Lerp(b, (elevation - a[axis]) / (b[axis] - a[axis]));
            }
            if (count != 2 || hits[0].DistanceSquaredTo(hits[1]) < 0.0000000001f) continue;
            for (var j = 0; j < 2; j++)
            {
                var p = Project(hits[j], axis);
                keys[j] = ((long)Math.Round(p.X * 100000.0, MidpointRounding.AwayFromZero),
                    (long)Math.Round(p.Y * 100000.0, MidpointRounding.AwayFromZero));
                points[keys[j]] = hits[j];
                neighbours.TryAdd(keys[j], new List<(long, long)>(2));
            }
            if (!neighbours[keys[0]].Contains(keys[1])) neighbours[keys[0]].Add(keys[1]);
            if (!neighbours[keys[1]].Contains(keys[0])) neighbours[keys[1]].Add(keys[0]);
        }
        var visited = new HashSet<(long, long)>();
        var vertices = new List<Vector3>(); var normals = new List<Vector3>();
        var inverse = transform.AffineInverse();
        var normal = transform.Basis.Inverse() * outward;
        foreach (var first in neighbours.Keys)
        {
            if (visited.Contains(first)) continue;
            var loop = new List<Vector2>(); var current = first;
            (long, long)? previous = null; var closed = false;
            for (var step = 0; step <= neighbours.Count; step++)
            {
                if (!visited.Add(current)) { closed = current == first; break; }
                loop.Add(Project(points[current], axis));
                var edges = neighbours[current];
                if (edges.Count != 2) break;
                var next = edges[0] != previous ? edges[0] : edges[1];
                previous = current; current = next;
            }
            if (!closed || loop.Count < 3) continue;
            var indices = Geometry2D.TriangulatePolygon(loop.ToArray());
            for (var i = 0; i < indices.Length; i += 3)
            {
                var a = Unproject(loop[indices[i]], elevation, axis);
                var b = Unproject(loop[indices[i + 1]], elevation, axis);
                var c = Unproject(loop[indices[i + 2]], elevation, axis);
                if ((-(b - a).Cross(c - a)).Dot(outward) < 0) (b, c) = (c, b);
                vertices.Add(inverse * a); vertices.Add(inverse * b); vertices.Add(inverse * c);
                normals.Add(normal); normals.Add(normal); normals.Add(normal);
            }
        }
        if (vertices.Count == 0) return null;
        var arrays = new Godot.Collections.Array(); arrays.Resize((int)Mesh.ArrayType.Max);
        arrays[(int)Mesh.ArrayType.Vertex] = vertices.ToArray();
        arrays[(int)Mesh.ArrayType.Normal] = normals.ToArray();
        var result = new ArrayMesh(); result.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);
        return result;
    }

    private static Vector2 Project(Vector3 p, int axis) => axis switch { 0 => new(p.Z, p.Y), 2 => new(p.X, p.Y), _ => new(p.X, p.Z) };
    private static Vector3 Unproject(Vector2 p, float coordinate, int axis) => axis switch { 0 => new(coordinate, p.Y, p.X), 2 => new(p.X, p.Y, coordinate), _ => new(p.X, coordinate, p.Y) };
}
