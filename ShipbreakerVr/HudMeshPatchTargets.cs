using System;
using System.Collections.Generic;
using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ShipbreakerVr;

// Kept separate so the exact reflection lookup used at startup is regression-tested
// against the installed UI/TMP assemblies, without starting Unity or installing hooks.
internal static class HudMeshPatchTargets
{
    internal static Dictionary<MethodBase, int> Resolve()
    {
        var targets = new Dictionary<MethodBase, int>();
        void Add(Type owner, string name, int count, params Type[] parameters)
        {
            var method = owner.GetMethod(name, BindingFlags.Instance | BindingFlags.Static |
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly, null, parameters, null);
            if (method == null || method.DeclaringType != owner)
                throw new MissingMethodException(owner.FullName, name);
            targets.Add(method, count);
        }
        Add(typeof(Graphic), "DoMeshGeneration", 1);
        Add(typeof(Graphic), "DoLegacyMeshGeneration", 1);
        Add(typeof(TextMeshProUGUI), "SetMeshArrays", 1, typeof(int));
        var unicode = typeof(TMP_Text).GetNestedType("UnicodeChar", BindingFlags.Public | BindingFlags.NonPublic);
        if (unicode == null) throw new TypeLoadException("TMP_Text.UnicodeChar is unavailable.");
        Add(typeof(TextMeshProUGUI), "SetArraySizes", 1, unicode.MakeArrayType());
        Add(typeof(TextMeshProUGUI), "GenerateTextMesh", 2);
        Add(typeof(TextMeshProUGUI), "UpdateSDFScale", 2, typeof(float));
        Add(typeof(TextMeshProUGUI), "ClearMesh", 2);
        // The inherited Graphic.UpdateGeometry() is NOT the TMP mesh submission method.
        Add(typeof(TextMeshProUGUI), "UpdateGeometry", 2, typeof(Mesh), typeof(int));
        Add(typeof(TextMeshProUGUI), "UpdateVertexData", 2);
        Add(typeof(TextMeshProUGUI), "UpdateVertexData", 2, typeof(TMP_VertexDataUpdateFlags));
        return targets;
    }
}
