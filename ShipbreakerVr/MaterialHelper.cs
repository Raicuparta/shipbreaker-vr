using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

namespace ShipbreakerVr;

public static class MaterialHelper
{
    private static readonly int unityGuizTestMode = Shader.PropertyToID("unity_GUIZTestMode");

    private static void MakeMaterialDrawOnTop(Material material)
    {
        material.shader = Canvas.GetDefaultCanvasMaterial().shader;
        material.SetInt(unityGuizTestMode, (int) CompareFunction.Always);
    }

    private static void MakeGraphicDrawOnTop(Graphic graphic)
    {
        if (graphic.material == Canvas.GetDefaultCanvasMaterial())
            graphic.material = new Material(graphic.material);
        MakeMaterialDrawOnTop(graphic.material);
    }

    public static void MakeGraphicChildrenDrawOnTop(GameObject parent)
    {
        var graphics = parent.GetComponentsInChildren<Graphic>(true);
        foreach (var graphic in graphics) MakeGraphicDrawOnTop(graphic);
    }
}