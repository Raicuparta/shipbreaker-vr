using System.Collections.Generic;
using UnityEngine;
using ShipbreakerVr.Tracking;

namespace ShipbreakerVr;

// A four-corner panel cannot bend: equal-depth corners leave two flat triangles.
// Refine large plain and sliced UI panels; leave large text meshes alone. Preserve all streams used by UI/TMP shaders.
internal static class VrUiQuadSubdivision
{
    internal static void Refine(Mesh mesh, Vector3 worldScale)
    {
        if (mesh.vertexCount < 4 || mesh.vertexCount > 256 || mesh.subMeshCount != 1 || mesh.GetTopology(0) != MeshTopology.Triangles) return;
        var src = mesh.vertices; var tris = mesh.GetTriangles(0);
        if (tris.Length % 3 != 0) return;
        var ns = mesh.normals; var ts = mesh.tangents; var cs = mesh.colors;
        var uv = new List<Vector4>[8]; var outUv = new List<Vector4>[8];
        for (var channel = 0; channel < 8; channel++)
        { uv[channel] = new List<Vector4>(); mesh.GetUVs(channel, uv[channel]); outUv[channel] = new List<Vector4>(); }
        var vs = new List<Vector3>(); var normals = new List<Vector3>(); var tangents = new List<Vector4>();
        var colors = new List<Color>(); var indices = new List<int>();
        for (var tri = 0; tri < tris.Length; tri += 3)
        {
            var a = tris[tri]; var b = tris[tri + 1]; var c = tris[tri + 2]; var start = vs.Count;
            var span = Mathf.Max(Vector3.Scale(src[a] - src[b], worldScale).magnitude,
                Mathf.Max(Vector3.Scale(src[b] - src[c], worldScale).magnitude, Vector3.Scale(src[c] - src[a], worldScale).magnitude));
            var steps = Mathf.Clamp(Mathf.CeilToInt(span / .12f), 1, 16);
            for (var row = 0; row <= steps; row++) for (var col = 0; col <= steps - row; col++)
            {
                var wb = (float)row / steps; var wc = (float)col / steps; var wa = 1 - wb - wc;
                vs.Add(src[a] * wa + src[b] * wb + src[c] * wc);
                if (ns.Length == src.Length) normals.Add((ns[a] * wa + ns[b] * wb + ns[c] * wc).normalized);
                if (ts.Length == src.Length) tangents.Add(ts[a] * wa + ts[b] * wb + ts[c] * wc);
                if (cs.Length == src.Length) colors.Add(cs[a] * wa + cs[b] * wb + cs[c] * wc);
                for (var channel = 0; channel < 8; channel++) if (uv[channel].Count == src.Length)
                    outUv[channel].Add(uv[channel][a] * wa + uv[channel][b] * wb + uv[channel][c] * wc);
            }
            PresentationGeometry.AppendTriangleGrid(indices, steps, start);
        }
        mesh.Clear(); if (vs.Count > 65535) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        mesh.SetVertices(vs); mesh.SetTriangles(indices, 0);
        if (normals.Count > 0) mesh.SetNormals(normals);
        if (tangents.Count > 0) mesh.SetTangents(tangents);
        if (colors.Count > 0) mesh.SetColors(colors);
        for (var channel = 0; channel < 8; channel++) if (outUv[channel].Count > 0) mesh.SetUVs(channel, outUv[channel]);
        mesh.RecalculateBounds();
    }
}
