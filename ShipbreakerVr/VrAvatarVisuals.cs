using System;
using System.Collections.Generic;
using BBI;
using BBI.Unity.Game;
using BepInEx.Configuration;
using HarmonyLib;
using ShipbreakerVr.Tracking;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

namespace ShipbreakerVr;

// Apply after animation/VR camera update but before Unity skins meshes for rendering.
// Restore at frame end and early next Update, before gameplay reads native transforms.
// Never scale the player Rigidbody/colliders, change parents, or attach components to game entities.
[DefaultExecutionOrder(9500)]
internal sealed class VrAvatarVisuals : MonoBehaviour
{
    private static VrAvatarVisuals instance;
    private static ConfigEntry<bool> enabledSetting;
    private static ConfigEntry<float> toolScale;
    private static ConfigEntry<Vector3> grappleOffset;
    private static ConfigEntry<float> grappleHandleForward;
    private static ConfigEntry<float> grappleLift;
    private static ConfigEntry<Vector3> grappleTrim;
    private static ConfigEntry<Vector3> detonatorOffset;
    private static ConfigEntry<float> detonatorLift;
    private static ConfigEntry<Vector3> chargeOffset;
    private GrabController grab;
    private Animator animator;
    private readonly VrGloveVisuals gloves = new VrGloveVisuals();
    private Renderer[] bodyMeshes = new Renderer[0];
    private Renderer[] detonatorMeshes = new Renderer[0];
    private readonly VrHandPropVisuals detonator = new VrHandPropVisuals();
    private readonly VrHandPropVisuals heldCharge = new VrHandPropVisuals("Held charge");
    private Renderer[] chargeMeshes = new Renderer[0];
    private Animator demoAnimator;
    private bool chargeSearched;
    private readonly VrHeadVisibility headVisibility = new VrHeadVisibility();
    private bool failed;
    private readonly OpenXrTrackingProvider tracking = new OpenXrTrackingProvider();
    private readonly List<Tool> tools = new List<Tool>();
    private readonly List<SavedTransform> transforms = new List<SavedTransform>();
    private readonly Dictionary<LineRenderer, Vector3> beamStarts = new Dictionary<LineRenderer, Vector3>();
    private readonly Dictionary<Renderer, bool> visibility = new Dictionary<Renderer, bool>();
    private readonly Dictionary<CanvasRenderer, float> uiVisibility = new Dictionary<CanvasRenderer, float>();
    private readonly Dictionary<CutterHeatBarUIController, CanvasRenderer[]> heatBars = new Dictionary<CutterHeatBarUIController, CanvasRenderer[]>();
    private readonly Dictionary<Renderer, ShadowCastingMode> shadows = new Dictionary<Renderer, ShadowCastingMode>();
    private sealed class Tool
    {
        internal Transform Root, Muzzle;
        internal Transform MuzzleMesh;
        internal Vector3 MuzzleMeshPoint;
        internal bool Applied, ReportedAlignment;
        internal bool ReturnReady, HasNativePose;
        internal Vector3 PreviousNativePosition;
        internal Quaternion PreviousNativeRotation;
        internal readonly ToolReturnVisibility ReturnVisibility = new ToolReturnVisibility();
        internal Vector3 NativeLocalScale, LocalMuzzle;
        internal Quaternion MuzzleRotation;
        internal EquipmentController Equipment;
        internal EquipmentController.Equipment Kind;
        internal Renderer[] Renderers;
        internal CanvasRenderer[] UiRenderers;
        internal Renderer[] Shadows;
        internal LineRenderer[] Beams;
    }
    private readonly struct SavedTransform
    {
        private readonly Transform target;
        private readonly Vector3 position, scale;
        private readonly Quaternion rotation;
        internal SavedTransform(Transform value) { target = value; position = value.localPosition; rotation = value.localRotation; scale = value.localScale; }
        internal void Restore() { if (target) { target.localPosition = position; target.localRotation = rotation; target.localScale = scale; } }
    }
    internal static void Configure(ConfigFile config)
    {
        enabledSetting = config.Bind("Avatar", "DisembodiedPresentation", true, "Hide the native body/arms; show tracked tools or cached glove meshes. Disable to restore native visuals.");
        toolScale = config.Bind("Avatar", "ToolVisualScale", .65f, new ConfigDescription("Shared size relative to native size for cutter, grapple, held charge and detonator in both input modes. Legacy GrappleVisualScale is ignored; gameplay aim/range are unchanged.", new AcceptableValueRange<float>(.25f, 1.2f)));
        grappleOffset = config.Bind("Avatar", "GrappleGripOffsetMetres", new Vector3(0, -.045f, .02f), "Grapple model offset from the tracked grip, in controller aim axes; does not move the targeting ray.");
        grappleHandleForward = config.Bind("Avatar", "GrappleHandleForwardMetres", .14f, new ConfigDescription("Distance from the grapple's rear pivot to its handle, after visual scaling. Moves the model back around the tracked hand without changing aim. Fine-tune for comfort.", new AcceptableValueRange<float>(0f, .3f)));
        grappleLift = config.Bind("Avatar", "GrappleLiftMetres", .025f, new ConfigDescription("Additional vertical grapple grip correction in controller aim axes.", new AcceptableValueRange<float>(-.1f, .1f)));
        grappleTrim = config.Bind("Avatar", "GrappleFinalOffsetMetres", new Vector3(-.01f, -.01f, 0), "Final grapple model and visual beam adjustment in controller aim axes, after muzzle alignment. Gameplay targeting stays unchanged.");
        detonatorOffset = config.Bind("Avatar", "DetonatorCentreOffsetMetres", new Vector3(0, 0, .025f), "Visible detonator centre relative to tracked grip, in grip axes; native arm-animation translation is removed.");
        detonatorLift = config.Bind("Avatar", "DetonatorLiftMetres", .095f, new ConfigDescription("Additional lift of the detonator in grip axes so the hand sits around its middle.", new AcceptableValueRange<float>(-.1f, .15f)));
        chargeOffset = config.Bind("Avatar", "HeldChargeCentreOffsetMetres", new Vector3(-.015f, 0, .04f), "Held charge centre relative to tracked grip. Only presentation changes; placed/thrown charges and surface preview are untouched.");
        VrHeadVisibility.Configure(config);
    }
    private void Awake() { instance = this; gameObject.AddComponent<VrAvatarEarlyRestore>(); }
    private void OnEnable()
    {
        RenderPipelineManager.endFrameRendering += AfterFrame;
    }
    internal static void RegisterGrab(GrabController value, Animator handAnimator)
    {
        if (!instance) return;
        if (instance.grab == value && instance.animator == handAnimator) return;
        instance.Restore(); instance.headVisibility.Clear(); instance.grab = value; instance.animator = handAnimator;
        var meshes = new List<Renderer>();
        if (handAnimator) foreach (var skin in handAnimator.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            if (skin.name == "geo_arms" || skin.name == "geo_suit") meshes.Add(skin);
        instance.bodyMeshes = meshes.ToArray();
        Debug.Log($"[ShipbreakerVr] Avatar registration: grab={value.name}, animator={(handAnimator ? handAnimator.name : "missing")}");
    }
    internal static void RegisterTool(Transform root, Transform muzzle, EquipmentController equipment, EquipmentController.Equipment kind, Transform shadow, LineRenderer[] beams = null)
    {
        if (!instance || !root || !equipment) return;
        instance.tools.RemoveAll(item => !item.Root);
        foreach (var item in instance.tools) if (item.Root == root) return;
        var renderers = root.GetComponentsInChildren<Renderer>(true);
        var tool = new Tool { Beams = beams ?? new LineRenderer[0], Root = root, Muzzle = muzzle, Equipment = equipment, Kind = kind, Renderers = renderers,
            UiRenderers = root.GetComponentsInChildren<CanvasRenderer>(true),
            NativeLocalScale = root.localScale, LocalMuzzle = muzzle ? root.InverseTransformPoint(muzzle.position) : Vector3.zero,
            MuzzleRotation = muzzle ? Quaternion.Inverse(root.rotation) * muzzle.rotation : Quaternion.identity,
            Shadows = shadow ? shadow.GetComponentsInChildren<Renderer>(true) : new Renderer[0] };
        if (kind == EquipmentController.Equipment.GrappleHook)
            foreach (var filter in root.GetComponentsInChildren<MeshFilter>(true))
                if (filter.sharedMesh && filter.sharedMesh.name == "GB_GrappleTool_Piston_2")
                {
                    // Verified against the shipped mesh: its +Z cap is a 20-vertex
                    // circle concentric with the mesh's XY bounds (see MUZZLE.md).
                    // Follow this animated piece, not the approximate Gun Barrel marker.
                    var bounds = filter.sharedMesh.bounds;
                    tool.MuzzleMesh = filter.transform;
                    tool.MuzzleMeshPoint = new Vector3(bounds.center.x, bounds.center.y, bounds.max.z);
                    Debug.Log($"[ShipbreakerVr] Grapple visual muzzle: {filter.name}; mesh-local center={tool.MuzzleMeshPoint.ToString("F6")}");
                    break;
                }
        instance.tools.Add(tool);
        Debug.Log($"[ShipbreakerVr] Avatar tool registered: {kind}, root={root.name}, scale={root.lossyScale}, renderers={renderers.Length}, muzzle={(muzzle ? muzzle.name : "missing")}");
    }
    internal static void RestoreEarly() { if (instance) instance.Restore(); }
    private void LateUpdate() => BeforeCamera(default, VrCamera.ViewCamera);
    internal static void RegisterDetonator(GameObject root, Animator handAnimator)
    {
        if (!instance) return;
        instance.detonatorMeshes = root ? root.GetComponentsInChildren<Renderer>(true) : new Renderer[0];
        instance.detonator.Register(instance.detonatorMeshes);
        instance.demoAnimator = handAnimator; instance.chargeSearched = false;
        instance.chargeMeshes = new Renderer[0]; instance.heldCharge.Dispose();
    }
    private void FindHeldCharge()
    {
        if (chargeSearched || !demoAnimator) return;
        chargeSearched = true;
        var sources = new List<Renderer>(); var names = new List<string>();
        foreach (var renderer in demoAnimator.GetComponentsInChildren<Renderer>(true))
        {
            var filter = renderer.GetComponent<MeshFilter>();
            var mesh = renderer is SkinnedMeshRenderer skin ? skin.sharedMesh : filter ? filter.sharedMesh : null;
            if (!mesh || Array.IndexOf(detonatorMeshes, renderer) >= 0) continue;
            names.Add(renderer.name + "/" + mesh.name);
            var name = (renderer.name + mesh.name).Replace("_", "").Replace(" ", "").Replace("-", "").ToLowerInvariant();
            // Only the local animated hand prop; never a deployed charge or wall preview.
            if (!name.Contains("charge") || renderer.GetComponentInParent<DemoChargePreview>()) continue;
            sources.Add(renderer);
        }
        chargeMeshes = sources.ToArray(); heldCharge.Register(chargeMeshes);
        Debug.Log($"[ShipbreakerVr] Held charge discovery: matches={sources.Count}; local prop meshes={string.Join(",", names)}");
    }
    private void Hide(Renderer renderer)
    {
        if (renderer && !visibility.ContainsKey(renderer))
        { visibility.Add(renderer, renderer.forceRenderingOff); renderer.forceRenderingOff = true; }
    }
    internal static void RegisterHeatBar(CutterHeatBarUIController owner, Image main, Image idle,
        CanvasGroup group, List<Graphic> gradientGraphics, List<GameObject> equippedObjects)
    {
        if (!instance || !owner) return;
        // Only widgets explicitly owned by the native heat-bar controller. Do not
        // disable that controller or its objects: native heat updates must continue.
        var graphics = new HashSet<Graphic>();
        if (main) graphics.Add(main);
        if (idle) graphics.Add(idle);
        if (group) foreach (var graphic in group.GetComponentsInChildren<Graphic>(true)) graphics.Add(graphic);
        if (gradientGraphics != null) foreach (var graphic in gradientGraphics) if (graphic) graphics.Add(graphic);
        if (equippedObjects != null) foreach (var obj in equippedObjects)
            if (obj) foreach (var graphic in obj.GetComponentsInChildren<Graphic>(true)) graphics.Add(graphic);
        var renderers = new List<CanvasRenderer>();
        foreach (var graphic in graphics) if (graphic && graphic.canvasRenderer) renderers.Add(graphic.canvasRenderer);
        instance.heatBars[owner] = renderers.ToArray();
    }
    internal static void UnregisterHeatBar(CutterHeatBarUIController owner)
    {
        if (instance) instance.heatBars.Remove(owner);
    }
    private void Hide(CanvasRenderer renderer)
    {
        if (!renderer || uiVisibility.ContainsKey(renderer)) return;
        uiVisibility.Add(renderer, renderer.GetAlpha());
        renderer.SetAlpha(0);
    }
    private void HideTool(Tool tool)
    {
        foreach (var renderer in tool.Renderers) Hide(renderer);
        foreach (var renderer in tool.UiRenderers) Hide(renderer);
        if (tool.Kind == EquipmentController.Equipment.CuttingTool)
            foreach (var bar in heatBars)
                if (bar.Key) foreach (var renderer in bar.Value) Hide(renderer);
    }
    private void Save(Transform value) => transforms.Add(new SavedTransform(value));
    internal static bool IsToolHeld(EquipmentController equipment, EquipmentController.Equipment kind)
    {
        // CurrentEquipment remains selected while grabbing/interacting, even
        // though the native tool is moved to a rest point below the player.
        if (!equipment || equipment.IsNothingEquipped || equipment.CurrentEquipment != kind) return false;
        var hand = instance && instance.grab ? instance.grab.RightHand : null;
        return hand == null || hand.CurrentlyHoldingEquipment(kind);
    }
    private void BeforeCamera(ScriptableRenderContext context, Camera camera)
    {
        Restore();
        gloves.Hide();
        var yard = GameSession.CurrentGameState == GameSession.GameState.Gameplay && !EquipmentController.ToolMenuOpen;
        if (failed || !enabledSetting.Value || !ModXrManager.IsVrEnabled || !camera || camera != VrCamera.ViewCamera || !VrCamera.BodyTransform) return;
        try
        {
            headVisibility.Apply(animator);
            // The separate native shadow models must not become visible duplicates
            // in either input mode. Restore() preserves their game-owned settings.
            foreach (var tool in tools)
            {
                foreach (var renderer in tool.Shadows)
                    if (renderer && !shadows.ContainsKey(renderer)) { shadows.Add(renderer, renderer.shadowCastingMode); renderer.shadowCastingMode = ShadowCastingMode.ShadowsOnly; }
                if (!tool.Root) continue;
                var position = tool.Root.localPosition;
                var rotation = tool.Root.localRotation;
                var dt = Time.deltaTime;
                // Native roots spring back relative to their equipment attachment.
                // Wait for that motion to settle; do not reveal the rising tool.
                var settled = tool.HasNativePose && dt > 0 &&
                    Vector3.Distance(position, tool.PreviousNativePosition) <= .03f * dt &&
                    Quaternion.Angle(rotation, tool.PreviousNativeRotation) <= 10f * dt;
                var selected = tool.Equipment && tool.Equipment.CurrentEquipment == tool.Kind;
                var ready = tool.ReturnVisibility.Sample(selected, IsToolHeld(tool.Equipment, tool.Kind), settled, dt);
                tool.ReturnReady = yard && ready;
                tool.PreviousNativePosition = position; tool.PreviousNativeRotation = rotation; tool.HasNativePose = true;
            }
            // Couch mode keeps the game's own animated placement and muzzle axes.
            // Restore() above has already undone any prior motion-controller pose.
            if (!VrInputMode.MotionActive)
            {
                foreach (var renderer in bodyMeshes) Hide(renderer);
                foreach (var tool in tools)
                {
                    if (!tool.Root || !tool.Equipment) continue;
                    if (!tool.ReturnReady)
                    {
                        HideTool(tool);
                        continue;
                    }
                    // Use the same reduced size as motion mode while retaining
                    // the native animated muzzle position and right-stick aim.
                    if (tool.Muzzle && tool.Muzzle.IsChildOf(tool.Root))
                    {
                        var muzzle = tool.Muzzle.position;
                        Save(tool.Root);
                        tool.Root.localScale *= toolScale.Value;
                        tool.Root.position += muzzle - tool.Muzzle.position;
                    }
                }
                if (yard && IsToolHeld(VrToolPresentation.Equipment, EquipmentController.Equipment.DemoCharge))
                {
                    FindHeldCharge();
                    // Shrink only the held visual geometry around its native centre.
                    // Preserve the animation, skeleton, wall preview and placed charges.
                    if (detonator.DrawAtNativePose(toolScale.Value, camera))
                        foreach (var renderer in detonatorMeshes) Hide(renderer);
                    if (heldCharge.DrawAtNativePose(toolScale.Value, camera))
                        foreach (var renderer in chargeMeshes) Hide(renderer);
                }
                else
                {
                    FindHeldCharge();
                    foreach (var renderer in detonatorMeshes) Hide(renderer);
                    foreach (var renderer in chargeMeshes) Hide(renderer);
                }
                return;
            }
            tracking.Sample();
            // Capture native gloves before hiding the source; never rotate or scale skeleton bones.
            if (animator) gloves.TryCapture(animator);
            foreach (var renderer in bodyMeshes) Hide(renderer);
            foreach (var renderer in detonatorMeshes) Hide(renderer);
            FindHeldCharge();
            foreach (var renderer in chargeMeshes) Hide(renderer);
            var body = VrCamera.BodyTransform;
            var inHabitat = GameSession.CurrentGameState == GameSession.GameState.Hab ||
                (GameSession.CurrentGameState == GameSession.GameState.Paused && GameSession.PrevGameState == GameSession.GameState.Hab);
            var showHands = !inHabitat && yard && (!VrToolPresentation.Equipment ||
                VrToolPresentation.Equipment.CurrentEquipment == EquipmentController.Equipment.DemoCharge ||
                VrToolPresentation.Equipment.CurrentEquipment == EquipmentController.Equipment.Scanner);
            if (showHands && Application.isFocused && tracking.Head.IsValid) gloves.Draw(tracking, body, camera, !yard);
            var aimValid = Application.isFocused && tracking.Head.IsValid && tracking.RightHand.Aim.IsValid && tracking.RightHand.Grip.IsValid;
            var aim = aimValid ? ShipbreakerTrackingSpace.ToWorld(tracking.Head, tracking.RightHand.Aim, body.position, body.rotation, VrCamera.EyeOffset) : default;
            var grip = aimValid ? ShipbreakerTrackingSpace.ToWorld(tracking.Head, tracking.RightHand.Grip, body.position, body.rotation, VrCamera.EyeOffset) : default;
            if (yard && aimValid && IsToolHeld(VrToolPresentation.Equipment, EquipmentController.Equipment.DemoCharge))
            {
                var attachment = grab?.RightHand?.EquipmentGeoTransform;
                if (attachment)
                {
                    var nativeAimInHand = Quaternion.identity;
                    foreach (var tool in tools)
                        if (tool.Kind == EquipmentController.Equipment.GrappleHook && tool.Root && tool.Muzzle)
                        { nativeAimInHand = tool.MuzzleRotation; break; }
                    if (VrToolPresentation.DemoState == DemoChargeState.Detonation || VrToolPresentation.DemoState == DemoChargeState.Detonating || VrToolPresentation.DemoState == DemoChargeState.DetonationFailed)
                    {
                        detonator.Draw(attachment, grip.position + grip.rotation * (detonatorOffset.Value + Vector3.up * detonatorLift.Value),
                            aim.rotation * Quaternion.Inverse(nativeAimInHand), toolScale.Value, camera);
                    }
                    else if (VrToolPresentation.DemoState == DemoChargeState.Placement || VrToolPresentation.DemoState == DemoChargeState.Placing || VrToolPresentation.DemoState == DemoChargeState.Throwing)
                    {
                        var visible = Array.Exists(chargeMeshes, mesh => mesh && mesh.enabled && mesh.gameObject.activeInHierarchy);
                        if (visible) heldCharge.Draw(attachment, grip.position + grip.rotation * chargeOffset.Value, aim.rotation * Quaternion.Inverse(nativeAimInHand), toolScale.Value, camera);
                    }
                }
            }
            foreach (var tool in tools)
            {
                if (!tool.Root || !tool.Equipment) continue;
                if (!tool.ReturnReady || !aimValid)
                {
                    HideTool(tool);
                    continue;
                }
                // A native muzzle supplies the mesh's forward-axis correction; no guess based on mesh names.
                if (!tool.Muzzle || !tool.Muzzle.IsChildOf(tool.Root)) continue;
                Save(tool.Root);
                ResolveTool(tool, aim, grip, out var rootPose, out var targetMuzzle);
                tool.Root.localScale = tool.NativeLocalScale * toolScale.Value;
                tool.Root.SetPositionAndRotation(rootPose.position, rootPose.rotation);
                if (tool.Kind == EquipmentController.Equipment.CuttingTool)
                    tool.Root.position += targetMuzzle - tool.Muzzle.position;
                tool.Applied = true;
                // Native FX update before animation. Re-seat only the rendered beam
                // starts after the live cutter pose; collision/endpoints stay native.
                foreach (var beam in tool.Beams)
                    if (beam && beam.enabled && beam.gameObject.activeInHierarchy && beam.positionCount >= 2)
                    {
                        if (!beamStarts.ContainsKey(beam)) beamStarts.Add(beam, beam.GetPosition(0));
                        beam.SetPosition(0, beam.useWorldSpace ? tool.Muzzle.position : beam.transform.InverseTransformPoint(tool.Muzzle.position));
                    }
                if (!tool.ReportedAlignment)
                {
                    tool.ReportedAlignment = true;
                    Debug.Log($"[ShipbreakerVr] Live tool alignment: {tool.Kind}; muzzleError={Vector3.Distance(tool.Muzzle.position, targetMuzzle):F5}m; forwardAgreement={Vector3.Dot(tool.Muzzle.forward, aim.rotation * Vector3.forward):F5}");
                }
            }
        }
        catch (Exception error) { failed = true; Restore(); Debug.LogError("[ShipbreakerVr] Avatar visual adjustment disabled; native visuals restored. " + error); }
    }
    private static void ResolveTool(Tool tool, Pose aim, Pose grip, out Pose root, out Vector3 muzzle)
    {
        // Cutter child transforms animate after Awake. Never calibrate them from
        // a startup snapshot or from a root we have already moved for rendering.
        if (tool.Kind == EquipmentController.Equipment.CuttingTool && !tool.Applied)
        {
            tool.NativeLocalScale = tool.Root.localScale;
            tool.LocalMuzzle = tool.Root.InverseTransformPoint(tool.Muzzle.position);
            tool.MuzzleRotation = Quaternion.Inverse(tool.Root.rotation) * tool.Muzzle.rotation;
        }
        var rotation = aim.rotation * Quaternion.Inverse(tool.MuzzleRotation);
        var parentScale = tool.Root.parent ? tool.Root.parent.lossyScale : Vector3.one;
        var scaledMuzzle = Vector3.Scale(tool.LocalMuzzle, Vector3.Scale(tool.NativeLocalScale, parentScale)) * toolScale.Value;
        var rotatedMuzzle = rotation * scaledMuzzle;
        var desired = tool.Kind == EquipmentController.Equipment.GrappleHook
            ? PresentationGeometry.ToolPivot(grip.position, aim.rotation, grappleOffset.Value + Vector3.up * grappleLift.Value, grappleHandleForward.Value)
            : grip.position;
        var origin = PresentationGeometry.AlignedToolOrigin(desired, rotatedMuzzle, aim.position, aim.rotation * Vector3.forward);
        if (tool.Kind == EquipmentController.Equipment.GrappleHook) origin += aim.rotation * grappleTrim.Value;
        root = new Pose(origin, rotation); muzzle = origin + rotatedMuzzle;
    }
    internal static Vector3 ToolBeamOrigin(EquipmentController.Equipment kind, Pose aim)
    {
        if (!instance || instance.failed || !enabledSetting.Value || !VrInputMode.MotionActive || !VrCamera.BodyTransform) return aim.position;
        instance.tracking.Sample();
        if (!instance.tracking.Head.IsValid || !instance.tracking.RightHand.Grip.IsValid) return aim.position;
        var body = VrCamera.BodyTransform;
        var grip = ShipbreakerTrackingSpace.ToWorld(instance.tracking.Head, instance.tracking.RightHand.Grip, body.position, body.rotation, VrCamera.EyeOffset);
        foreach (var tool in instance.tools)
            if (tool.Kind == kind && tool.Root && tool.Muzzle && tool.Muzzle.IsChildOf(tool.Root))
            {
                if (tool.Applied) return tool.MuzzleMesh ? tool.MuzzleMesh.TransformPoint(tool.MuzzleMeshPoint) : tool.Muzzle.position;
                ResolveTool(tool, aim, grip, out var rootPose, out var muzzle);
                if (tool.MuzzleMesh)
                {
                    var local = tool.Root.InverseTransformPoint(tool.MuzzleMesh.TransformPoint(tool.MuzzleMeshPoint));
                    var parentScale = tool.Root.parent ? tool.Root.parent.lossyScale : Vector3.one;
                    return rootPose.position + rootPose.rotation * Vector3.Scale(local, Vector3.Scale(tool.NativeLocalScale, parentScale)) * toolScale.Value;
                }
                return muzzle;
            }
        return aim.position;
    }
    private void Restore()
    {
        headVisibility.Restore();
        foreach (var pair in beamStarts) if (pair.Key && pair.Key.positionCount >= 2) pair.Key.SetPosition(0, pair.Value);
        beamStarts.Clear();
        for (var i = transforms.Count - 1; i >= 0; i--) transforms[i].Restore();
        transforms.Clear();
        foreach (var tool in tools) tool.Applied = false;
        foreach (var pair in visibility) if (pair.Key) pair.Key.forceRenderingOff = pair.Value;
        visibility.Clear();
        foreach (var pair in uiVisibility) if (pair.Key) pair.Key.SetAlpha(pair.Value);
        uiVisibility.Clear();
        foreach (var pair in shadows) if (pair.Key) pair.Key.shadowCastingMode = pair.Value;
        shadows.Clear();

    }
    private void AfterFrame(ScriptableRenderContext context, Camera[] cameras) => Restore();
    private void OnDisable()
    {
        RenderPipelineManager.endFrameRendering -= AfterFrame;
        Restore(); gloves.Hide();
    }
    private void OnDestroy() { Restore(); gloves.Dispose(); detonator.Dispose(); heldCharge.Dispose(); headVisibility.Clear(); if (instance == this) instance = null; }
}

[DefaultExecutionOrder(-9000)]
internal sealed class VrAvatarEarlyRestore : MonoBehaviour
{
    private void Update() => VrAvatarVisuals.RestoreEarly();
    private void FixedUpdate() => VrAvatarVisuals.RestoreEarly();
}

[HarmonyPatch]
internal static class AvatarVisualPatches
{
    [HarmonyPostfix, HarmonyPatch(typeof(CutterHeatBarUIController), "Awake")]
    private static void HeatBar(CutterHeatBarUIController __instance, Image ___m_MainHeatBarFill, Image ___m_IdleHeatBarFill,
        CanvasGroup ___m_CanvasGroup, List<Graphic> ___m_GraphicsToApplyGradientTo, List<GameObject> ___m_EnableWhileEquipped)
        => VrAvatarVisuals.RegisterHeatBar(__instance, ___m_MainHeatBarFill, ___m_IdleHeatBarFill,
            ___m_CanvasGroup, ___m_GraphicsToApplyGradientTo, ___m_EnableWhileEquipped);
    [HarmonyPrefix, HarmonyPatch(typeof(CutterHeatBarUIController), "OnDestroy")]
    private static void RemoveHeatBar(CutterHeatBarUIController __instance) => VrAvatarVisuals.UnregisterHeatBar(__instance);
    [HarmonyPostfix, HarmonyPatch(typeof(GrabController), "Awake")]
    private static void Grab(GrabController __instance, HandGrab.HandSettings ___m_HandSettings) => VrAvatarVisuals.RegisterGrab(__instance, ___m_HandSettings.HandAnimationController);
    [HarmonyPostfix, HarmonyPatch(typeof(GrabController), "Update")]
    private static void EnsureGrab(GrabController __instance, HandGrab.HandSettings ___m_HandSettings) => VrAvatarVisuals.RegisterGrab(__instance, ___m_HandSettings.HandAnimationController);
    [HarmonyPostfix, HarmonyPatch(typeof(GunController), "Awake")]
    private static void Gun(Transform ___m_GrapplingGun, Transform ___m_GrapplingGunShadow, EquipmentController ___m_EquipmentController, GrapplingHook ___m_GrapplingHook)
    {
        var muzzle = ___m_GrapplingHook ? AccessTools.Field(typeof(GrapplingHook), "m_GrapplingGunBarrel").GetValue(___m_GrapplingHook) as Transform : null;
        VrAvatarVisuals.RegisterTool(___m_GrapplingGun, muzzle, ___m_EquipmentController, EquipmentController.Equipment.GrappleHook, ___m_GrapplingGunShadow);
    }
    [HarmonyPostfix, HarmonyPatch(typeof(CuttingToolController), "Awake")]
    private static void Cutter(Transform ___m_CuttingTool, Transform ___m_CuttingToolShadow, EquipmentController ___m_EquipmentController, ScalpelController ___m_ScalpelController, CuttingController ___m_CuttingController)
    {
        var fx = ___m_ScalpelController ? ___m_ScalpelController.GetComponent<ScalpelFXController>() : null;
        var muzzle = fx ? AccessTools.Field(typeof(ScalpelFXController), "m_CutOriginTransform").GetValue(fx) as Transform : null;
        var splitFx = ___m_CuttingController ? ___m_CuttingController.GetComponent<CuttingFXController>() : null;
        var beams = new[] {
            fx ? AccessTools.Field(typeof(ScalpelFXController), "m_CuttingBeamLineRenderer").GetValue(fx) as LineRenderer : null,
            splitFx ? AccessTools.Field(typeof(CuttingFXController), "m_CuttingBeamLineRendererA").GetValue(splitFx) as LineRenderer : null,
            splitFx ? AccessTools.Field(typeof(CuttingFXController), "m_CuttingBeamLineRendererB").GetValue(splitFx) as LineRenderer : null };
        VrAvatarVisuals.RegisterTool(___m_CuttingTool, muzzle, ___m_EquipmentController, EquipmentController.Equipment.CuttingTool, ___m_CuttingToolShadow, beams);
    }
}
