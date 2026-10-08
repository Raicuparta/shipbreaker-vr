using System;
using ShipbreakerVr.Tracking;
using UnityEngine;
using UnityEngine.Rendering;

namespace ShipbreakerVr;

[DefaultExecutionOrder(10000)]
public sealed class ControllerDebugRays : MonoBehaviour
{
    private readonly OpenXrTrackingProvider tracking = new OpenXrTrackingProvider();
    private InputSystemUpdateBridge inputBridge;
    private float nextDiagnosticTime;
    private string lastDiagnostic;
    private string lastVisibilityReason;
    private LineRenderer left;
    private LineRenderer right;
    private LineRenderer endpoint;
    private readonly RaycastHit[] hits = new RaycastHit[64];
    private string lastGuide;
    private Material material;
    private readonly VrCouchReticle couchReticle = new VrCouchReticle();
    private MaterialPropertyBlock properties;
    private bool failed;
    private bool leftTracked;
    private bool rightTracked;

    private void OnEnable() => Application.onBeforeRender += SampleAndDraw;
    private void LateUpdate() => SampleAndDraw();

    private void OnDisable()
    {
        Application.onBeforeRender -= SampleAndDraw;
        inputBridge?.Dispose();
        inputBridge = null;
        Hide();
    }

    private void OnDestroy()
    {
        if (material) Destroy(material);
        couchReticle.Dispose();
    }

    private void SampleAndDraw()
    {
        var reason = failed ? "diagnostics failed" :
            !VrToolPresentation.GameplayRays ? "outside gameplay" :
            !VrToolPresentation.DebugRays.Value && !VrToolPresentation.ToolGuides.Value ? "disabled in configuration" :
            !ModXrManager.IsVrEnabled ? "VR is off" :
            !Application.isFocused ? "game window is not focused" :
            !VrCamera.BodyTransform || !VrCamera.ViewCamera || !VrCamera.ViewCamera.isActiveAndEnabled ? "VR camera unavailable" : "sampling controllers";
        if (reason != lastVisibilityReason)
        {
            lastVisibilityReason = reason;
            Debug.Log($"[ShipbreakerVr] Ray visibility: {reason}");
        }
        if (reason != "sampling controllers")
        {
            Hide();
            return;
        }

        try
        {
            if (VrInputMode.CouchActive)
            {
                Hide();
                inputBridge?.Dispose(); inputBridge = null;
                var nativeCamera = LynxCameraController.MainCamera;
                if (VrToolPresentation.ToolGuides.Value && nativeCamera &&
                    VrToolPresentation.TryRange(out var couchRange, out var couchMask, out var couchTriggers, out var couchMode))
                {
                    DrawGuide(couchRange, couchMask, couchTriggers, couchMode, new Pose(nativeCamera.transform.position, nativeCamera.transform.rotation), false);
                }
                return;
            }
            couchReticle.Hide();
            if (inputBridge == null) inputBridge = new InputSystemUpdateBridge();
            inputBridge.UpdateIfNeeded();
            tracking.Sample();
            if (Time.unscaledTime >= nextDiagnosticTime)
            {
                nextDiagnosticTime = Time.unscaledTime + 5f;
                var diagnostic = tracking.DescribeDevices();
                if (diagnostic != lastDiagnostic)
                {
                    lastDiagnostic = diagnostic;
                    Debug.Log($"[ShipbreakerVr] Controller diagnostics: {diagnostic}; hostUpdates={inputBridge.HostUpdateCount}, fallbackUpdates={inputBridge.PumpCount}");
                }
            }
            if (!tracking.Head.IsValid)
            {
                Hide();
                return;
            }
            if (!material) CreateRays();
            endpoint.enabled = false;
            if (VrToolPresentation.DebugRays.Value)
            {
                Draw(left, tracking.LeftHand, Color.cyan);
                Draw(right, tracking.RightHand, new Color(1f, .5f, 0));
            }
            else
            {
                left.enabled = right.enabled = false;
                if (VrToolPresentation.ToolGuides.Value && tracking.RightHand.Aim.IsValid &&
                    VrToolPresentation.TryRange(out var range, out var mask, out var triggers, out var mode)) DrawGuide(range, mask, triggers, mode);
            }
            ReportTracking("Left", tracking.LeftHand.Aim.IsValid, ref leftTracked);
            ReportTracking("Right", tracking.RightHand.Aim.IsValid, ref rightTracked);
        }
        catch (Exception exception)
        {
            failed = true;
            Hide();
            Debug.LogError($"[ShipbreakerVr] Debug controllers disabled; original VR remains available. {exception}");
        }
    }

    private void CreateRays()
    {
        var template = VrAssetManager.LoadBundle("debugrays").LoadAsset<Material>("DebugRayMaterial");
        if (!template || !template.shader || !template.shader.isSupported)
            throw new InvalidOperationException("HDRP debug ray material is unavailable or unsupported.");
        material = new Material(template);
        // A line is a camera-facing ribbon. Draw both sides in stereo views.
        material.SetInt("_CullMode", (int)CullMode.Off);
        properties = new MaterialPropertyBlock();
        left = CreateRay("Left OpenXR aim");
        right = CreateRay("Right OpenXR aim");
        endpoint = CreateRay("Tool reach endpoint"); endpoint.positionCount = 17;
        endpoint.startWidth = endpoint.endWidth = .003f;
        Debug.Log("[ShipbreakerVr] Tool range guides ready; diagnostic rays are opt-in under Presentation.ShowDebugRays.");
    }

    private LineRenderer CreateRay(string rayName)
    {
        var rayObject = new GameObject(rayName);
        rayObject.transform.SetParent(transform, false);
        var ray = rayObject.AddComponent<LineRenderer>();
        ray.sharedMaterial = material;
        ray.positionCount = 2;
        ray.useWorldSpace = true;
        ray.startWidth = ray.endWidth = 0.01f;
        ray.alignment = LineAlignment.View;
        ray.shadowCastingMode = ShadowCastingMode.Off;
        ray.receiveShadows = false;
        ray.enabled = false;
        return ray;
    }

    private void Draw(LineRenderer ray, TrackedHand hand, Color color)
    {
        ray.enabled = hand.Aim.IsValid;
        if (!hand.Aim.IsValid) return;
        // Choose a layer already visible to the current VR camera, without changing its mask.
        var mask = VrCamera.ViewCamera.cullingMask;
        var layer = 0;
        while (layer < 31 && (mask & (1 << layer)) == 0) layer++;
        ray.gameObject.layer = layer;
        var world = ShipbreakerTrackingSpace.ToWorld(tracking.Head, hand.Aim,
            VrCamera.BodyTransform.position, VrCamera.BodyTransform.rotation, VrCamera.EyeOffset);
        ray.SetPosition(0, world.position);
        ray.SetPosition(1, world.position + world.rotation * Vector3.forward * ShipbreakerVrMod.DebugRayLength.Value);
        ray.startColor = ray.endColor = Color.Lerp(color, Color.white, hand.Trigger);
        properties.SetColor("_UnlitColor", Color.Lerp(color, Color.white, hand.Trigger));
        ray.SetPropertyBlock(properties);
    }

    private void DrawGuide(float range, int mask, QueryTriggerInteraction triggers, string mode)
    {
        var pose = ShipbreakerTrackingSpace.ToWorld(tracking.Head, tracking.RightHand.Aim,
            VrCamera.BodyTransform.position, VrCamera.BodyTransform.rotation, VrCamera.EyeOffset);
        DrawGuide(range, mask, triggers, mode, pose, true);
    }

    private void DrawGuide(float range, int mask, QueryTriggerInteraction triggers, string mode, Pose pose, bool showBeam)
    {
        var direction = pose.rotation * Vector3.forward;
        var count = Physics.RaycastNonAlloc(pose.position, direction, hits, range, mask, triggers);
        // A saturated buffer cannot guarantee the closest hit. Fall back only in that case.
        var results = count == hits.Length ? Physics.RaycastAll(pose.position, direction, range, mask, triggers) : hits;
        if (results != hits) count = results.Length;
        var distance = range;
        var surface = false;
        for (var i = 0; i < count; i++)
        {
            var hit = results[i];
            if (!hit.collider || (VrToolPresentation.PlayerBody && hit.rigidbody == VrToolPresentation.PlayerBody)) continue;
            if (hit.distance <= distance) { distance = hit.distance; surface = true; }
        }
        distance = PresentationGeometry.Endpoint(range, distance);
        var point = pose.position + direction * distance;
        var color = surface ? Color.white : new Color(1f, .5f, 0);
        var status = mode + ": " + range.ToString("F2") + " m";
        if (status != lastGuide) { lastGuide = status; Debug.Log("[ShipbreakerVr] Tool guide range: " + status); }
        if (!showBeam)
        {
            couchReticle.Draw(transform, VrCamera.ViewCamera, point, color);
            return;
        }
        var layer = 0;
        while (layer < 31 && (VrCamera.ViewCamera.cullingMask & (1 << layer)) == 0) layer++;
        right.gameObject.layer = endpoint.gameObject.layer = layer;
        right.enabled = endpoint.enabled = true;
        right.startWidth = .003f; right.endWidth = .004f;
        var start = VrToolPresentation.Equipment ? VrAvatarVisuals.ToolBeamOrigin(VrToolPresentation.Equipment.CurrentEquipment, pose) : pose.position;
        // A hit between the hand and muzzle cannot produce a backwards beam through the model.
        right.enabled = showBeam && Vector3.Dot(start - pose.position, direction) <= distance;
        right.SetPosition(0, start); right.SetPosition(1, point);
        var radius = Mathf.Clamp(Vector3.Distance(point, VrCamera.ViewCamera.transform.position) * .003f, .008f, .07f);
        var camera = VrCamera.ViewCamera.transform;
        point -= direction * Mathf.Min(.01f, distance * .01f);
        for (var i = 0; i <= 16; i++)
        {
            var angle = i * Mathf.PI / 8;
            endpoint.SetPosition(i, point + radius * (camera.right * Mathf.Cos(angle) + camera.up * Mathf.Sin(angle)));
        }
        properties.SetColor("_UnlitColor", color);
        right.startColor = right.endColor = endpoint.startColor = endpoint.endColor = color;
        right.SetPropertyBlock(properties); endpoint.SetPropertyBlock(properties);
    }

    private static void ReportTracking(string hand, bool valid, ref bool previous)
    {
        if (valid == previous) return;
        previous = valid;
        Debug.Log($"[ShipbreakerVr] {hand} aim tracking: {(valid ? "valid" : "unavailable")}");
    }

    private void Hide()
    {
        couchReticle.Hide();
        tracking.Clear();
        if (left) left.enabled = false;
        if (right) right.enabled = false;
        if (endpoint) endpoint.enabled = false;
        ReportTracking("Left", false, ref leftTracked);
        ReportTracking("Right", false, ref rightTracked);
    }
}
