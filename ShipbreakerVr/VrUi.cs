using BBI.Unity.Game;
using UnityEngine;
using UnityEngine.UI;

namespace ShipbreakerVr;

public class VrUi : MonoBehaviour
{
    private const float forwardOffset = 3f;
    private Canvas canvas;
    private Vector3 initialPosition;
    private RenderMode initialRenderMode;
    private Quaternion initialRotation;
    private Vector3 initialScale;
    private Transform target;

    private void Awake()
    {
        target = (VrCamera.MainCamera ? VrCamera.MainCamera : Camera.main)?.transform;
        canvas = GetComponent<Canvas>();
        initialPosition = transform.position;
        initialRotation = transform.rotation;
        initialScale = transform.localScale;
        initialRenderMode = canvas.renderMode;
        MaterialHelper.MakeGraphicChildrenDrawOnTop(gameObject);
    }

    private void LateUpdate()
    {
        if (!target) return;

        transform.position = target.position + target.forward * forwardOffset;
        transform.rotation = target.rotation;
    }

    private void OnEnable()
    {
        ModXrManager.OnVrModeChange += OnVrModeChange;
    }

    private void OnDisable()
    {
        ModXrManager.OnVrModeChange -= OnVrModeChange;
    }

    private void OnVrModeChange(bool isVrEnabled)
    {
        if (isVrEnabled)
            SetUpCanvas();
        else
            ResetCanvas();
    }

    private void SetUpCanvas()
    {
        transform.localScale = Vector3.one * 0.0025f;
        canvas.renderMode = RenderMode.WorldSpace;
        SetBehaviourEnabled(canvas.GetComponent<CanvasScalerImprover>(), false);
        SetBehaviourEnabled(canvas.GetComponent<CanvasScaler>(), false);
    }

    private void ResetCanvas()
    {
        transform.position = initialPosition;
        transform.rotation = initialRotation;
        transform.localScale = initialScale;
        canvas.renderMode = initialRenderMode;
        SetBehaviourEnabled(canvas.GetComponent<CanvasScaler>(), true);
        SetBehaviourEnabled(canvas.GetComponent<CanvasScalerImprover>(), true);
    }

    private static void SetBehaviourEnabled(MonoBehaviour behaviour, bool enabled)
    {
        if (!behaviour) return;
        behaviour.enabled = enabled;
    }
}