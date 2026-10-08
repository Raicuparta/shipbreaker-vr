using System;
using System.Collections.Generic;
using BBI.Unity.Game;
using HarmonyLib;
using Unity.Entities;
using UnityEngine;

namespace ShipbreakerVr;

// Only newly thrown charges receive a snapshot of the hand's launch direction.
// Resolve immediately before the native initialization system consumes velocity;
// this also covers deferred entity creation after Object.Instantiate returns.
[HarmonyPatch]
internal static class DemoChargeAimPatches
{
    private static Vector3? throwDirection;
    private sealed class Pending
    {
        internal StructurePart Part;
        internal Vector3 Direction;
        internal World World;
        internal float Created;
    }
    private static readonly List<Pending> pending = new List<Pending>();

    [HarmonyPrefix, HarmonyPatch(typeof(DemoChargeController), "ThrowDemoCharge")]
    private static void BeginThrow(out Vector3? __state)
    {
        __state = throwDirection;
        throwDirection = VrAdditionalToolControls.TryDemoAim(out var pose) ? pose.rotation * Vector3.forward : (Vector3?)null;
    }
    [HarmonyFinalizer, HarmonyPatch(typeof(DemoChargeController), "ThrowDemoCharge")]
    private static void EndThrow(Vector3? __state) => throwDirection = __state;

    [HarmonyPostfix, HarmonyPatch(typeof(DemoChargeController), "InstantiateDemoCharge", new[] { typeof(Vector3), typeof(Quaternion), typeof(Entity) })]
    private static void Spawned(Entity overrideEntity, StructurePart __result)
    {
        if (!throwDirection.HasValue || overrideEntity != Entity.Null || !__result) return;
        pending.Add(new Pending { Part = __result, Direction = throwDirection.Value,
            World = World.DefaultGameObjectInjectionWorld, Created = Time.unscaledTime });
    }

    [HarmonyPrefix, HarmonyPatch(typeof(DemoChargeInitializationSystem), "OnUpdate")]
    private static void BeforeInitialization(DemoChargeInitializationSystem __instance)
    {
        var manager = __instance.EntityManager;
        for (var i = pending.Count - 1; i >= 0; i--)
        {
            var item = pending[i];
            if (!item.Part || item.World != __instance.World || Time.unscaledTime - item.Created > 10f)
            { pending.RemoveAt(i); continue; }
            var entity = item.Part.Entity;
            if (entity == Entity.Null || !manager.Exists(entity) || !manager.HasComponent<InitDemoChargeVelocity>(entity)) continue;
            var velocity = manager.GetComponentData<InitDemoChargeVelocity>(entity);
            velocity.Direction = item.Direction;
            manager.SetComponentData(entity, velocity);
            pending.RemoveAt(i);
            Debug.Log("[ShipbreakerVr] Demo charge launch uses captured VR aim direction.");
        }
    }
}
