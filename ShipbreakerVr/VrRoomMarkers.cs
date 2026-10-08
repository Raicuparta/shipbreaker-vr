using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using BBI.Unity.Game;
using BBI.Unity.Game.UI;
using BepInEx.Configuration;
using HarmonyLib;
using Unity.Entities;
using Unity.Transforms;
using UnityEngine;
using ShipbreakerVr.Tracking;

namespace ShipbreakerVr;

// Preserve the game's marker entities, localized labels, mode gating and pressure widgets.
// Only the final managed placement changes; Burst jobs and game assets stay untouched.
internal sealed class VrRoomMarkers : MonoBehaviour
{
    private static ConfigEntry<bool> enabledSetting;
    private static ConfigEntry<float> angularScale;
    internal static bool Available;
    private static bool failed;
    private static readonly Dictionary<Transform, Entry> entries = new Dictionary<Transform, Entry>();
    private static readonly List<Transform> dead = new List<Transform>();
    private sealed class Entry
    {
        internal Transform Transform;
        internal EntityBlueprintComponent Blueprint;
        internal Canvas Canvas;
        internal Vector3 Pixels, NativeScale, WorldPoint;
        internal Quaternion NativeRotation;
        internal bool Applied, HasWorld;
    }
    internal static void Configure(ConfigFile config)
    {
        enabledSetting = config.Bind("Scanner", "WorldRoomMarkers", true, "Position native room names/pressure widgets at their room in VR. Original scanner unlocks and mode visibility remain authoritative.");
        angularScale = config.Bind("Scanner", "RoomMarkerMetresPerPixelAtOneMetre", .00065f,
            new ConfigDescription("Room-label size grows with distance up to 20 metres for readability; does not move the marker away from its room.", new AcceptableValueRange<float>(.0002f, .002f)));
    }
    private static bool Active => Available && !failed && enabledSetting.Value && ModXrManager.IsVrEnabled && VrCamera.ViewCamera && VrCamera.ViewCamera.isActiveAndEnabled;
    internal static Camera MarkerCamera() => Active ? VrCamera.ViewCamera : LynxCameraController.MainCamera;
    internal static bool IsRoomGraphic(Component graphic) => graphic && graphic.GetComponentInParent<RoomInfoMarker>();
    private static Entry Get(Transform target)
    {
        if (entries.TryGetValue(target, out var entry)) return entry;
        if (!target.GetComponent<RoomInfoMarker>()) return null;
        var blueprint = target.GetComponent<EntityBlueprintComponent>();
        if (!blueprint) return null;
        var canvas = target.GetComponentInParent<Canvas>();
        entry = new Entry { Transform = target, Blueprint = blueprint, Canvas = canvas ? canvas.rootCanvas : null,
            Pixels = target.position, NativeScale = target.localScale, NativeRotation = target.localRotation };
        entries.Add(target, entry);
        Debug.Log("[ShipbreakerVr] Room marker adapter: " + target.name + "; native pressure widgets retained");
        return entry;
    }
    internal static void SetPosition(Transform target, Vector3 pixels)
    {
        if (!Active) { target.position = pixels; return; }
        try
        {
            var entry = Get(target);
            if (entry == null) { target.position = pixels; return; }
            entry.Pixels = pixels;
            entry.HasWorld = ReadWorld(entry, out entry.WorldPoint);
            if (entry.HasWorld) Place(entry);
            else target.position = pixels;
        }
        catch (Exception error) { Fail(error); target.position = pixels; }
    }
    internal static void SetScale(Transform target, Vector3 scale)
    {
        if (!Active) { target.localScale = scale; return; }
        try
        {
            var entry = Get(target);
            if (entry == null) { target.localScale = scale; return; }
            entry.NativeScale = scale;
            if (entry.HasWorld) Place(entry); else target.localScale = scale;
        }
        catch (Exception error) { Fail(error); target.localScale = scale; }
    }
    private static bool ReadWorld(Entry entry, out Vector3 point)
    {
        point = default;
        var blueprint = entry.Blueprint;
        if (!blueprint || !blueprint.Initialized) return false;
        var manager = blueprint.EntityManager;
        var entity = blueprint.Entity;
        if (!manager.Exists(entity) || !manager.HasComponent<UIMarkerTrackPosition>(entity)) return false;
        var source = manager.GetComponentData<UIMarkerTrackPosition>(entity).Value;
        if (!manager.Exists(source)) return false;
        if (manager.HasComponent<CenterOfMass>(source)) point = manager.GetComponentData<CenterOfMass>(source).World;
        else if (manager.HasComponent<LocalToWorld>(source)) point = (Vector3)manager.GetComponentData<LocalToWorld>(source).Position;
        else if (manager.HasComponent<Translation>(source)) point = manager.GetComponentData<Translation>(source).Value;
        else return false;
        if (manager.HasComponent<UIMarkerWorldSpaceOffset>(entity)) point += (Vector3)manager.GetComponentData<UIMarkerWorldSpaceOffset>(entity).Value;
        return !(float.IsNaN(point.x) || float.IsNaN(point.y) || float.IsNaN(point.z) || float.IsInfinity(point.x) || float.IsInfinity(point.y) || float.IsInfinity(point.z));
    }
    private static void Place(Entry entry)
    {
        var view = VrCamera.ViewCamera.transform;
        var target = entry.Transform;
        target.SetPositionAndRotation(entry.WorldPoint, view.rotation);
        var distance = Vector3.Distance(view.position, entry.WorldPoint);
        var parentScale = target.parent ? target.parent.lossyScale : Vector3.one;
        target.localScale = RoomMarkerGeometry.LocalScale(entry.NativeScale, parentScale, distance, angularScale.Value);
        entry.Applied = true;
    }
    internal static void Prepare(Canvas canvas)
    {
        if (!Active) { Restore(); return; }
        try
        {
            foreach (var entry in entries.Values)
                if (entry.Transform && entry.HasWorld && entry.Canvas == canvas) Place(entry);
        }
        catch (Exception error) { Fail(error); }
    }
    private static void Fail(Exception error)
    {
        failed = true; Restore();
        Debug.LogWarning("[ShipbreakerVr] Room marker adapter stopped; native placement restored. " + error);
    }
    private static void Restore()
    {
        foreach (var entry in entries.Values)
        {
            if (!entry.Transform || !entry.Applied) continue;
            entry.Transform.position = entry.Pixels;
            entry.Transform.localScale = entry.NativeScale;
            entry.Transform.localRotation = entry.NativeRotation;
            entry.Applied = false;
        }
    }
    private void Update()
    {
        if (!Active) Restore();
        dead.Clear();
        foreach (var pair in entries) if (!pair.Key) dead.Add(pair.Key);
        foreach (var key in dead) entries.Remove(key);
    }
    private void OnDisable() => Restore();
    private void OnDestroy() { Restore(); entries.Clear(); }
}

[HarmonyPatch]
internal static class RoomMarkerPatches
{
    internal static void InstallOptional()
    {
        var harmony = new Harmony("ShipbreakerVr.RoomMarkers");
        try { harmony.CreateClassProcessor(typeof(RoomMarkerPatches)).Patch(); VrRoomMarkers.Available = true; Debug.Log("[ShipbreakerVr] Room marker hooks ready: VR view visibility and world-positioned native labels/pressure widgets."); }
        catch (Exception error) { harmony.UnpatchSelf(); VrRoomMarkers.Available = false; Debug.LogWarning("[ShipbreakerVr] Room marker hooks unavailable; core VR retained. " + error); }
    }
    private static IEnumerable<MethodBase> TargetMethods()
    {
        yield return AccessTools.Method(typeof(UIMarkerTrackPositionSystem), "OnUpdate");
        yield return AccessTools.Method(typeof(UIMarkerSetPositionSystem), "OnUpdate");
    }
    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, MethodBase original)
    {
        var code = new List<CodeInstruction>(instructions); var cameras = 0; var positions = 0; var scales = 0;
        foreach (var instruction in code)
        {
            var method = instruction.operand as MethodInfo; string replacement = null;
            if (method == AccessTools.PropertyGetter(typeof(LynxCameraController), "MainCamera")) { replacement = "MarkerCamera"; cameras++; }
            else if (method == AccessTools.PropertySetter(typeof(Transform), "position")) { replacement = "SetPosition"; positions++; }
            else if (method == AccessTools.PropertySetter(typeof(Transform), "localScale")) { replacement = "SetScale"; scales++; }
            if (replacement == null) continue;
            instruction.opcode = OpCodes.Call; instruction.operand = AccessTools.Method(typeof(VrRoomMarkers), replacement);
        }
        var tracking = original.DeclaringType == typeof(UIMarkerTrackPositionSystem);
        if (tracking ? cameras != 4 || positions != 0 || scales != 0 : cameras != 0 || positions != 2 || scales != 2)
            throw new InvalidOperationException($"Room marker hook changed: {original.Name} camera={cameras}, position={positions}, scale={scales}");
        return code;
    }
}
