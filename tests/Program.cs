using System;
using System.Collections.Generic;
using ShipbreakerVr;
using ShipbreakerVr.Tracking;
using UnityEngine;
using UnityEngine.XR;

internal static class Program
{
    private static int checks;
    private static readonly Quaternion Identity = new Quaternion(0, 0, 0, 1);
    private static readonly InputTrackingState Full = InputTrackingState.Position | InputTrackingState.Rotation;

    private static void CheckPresentation()
    {
        var returning = new ToolReturnVisibility();
        Check(returning.Sample(true, true, false, .01f), "Initially equipped tool is visible without a mod-added wait");
        Check(!returning.Sample(true, false, true, .05f), "Stowed selected tool remains invisible even at a stationary rest point");
        for (var i = 0; i < 20; i++) returning.Sample(true, true, false, .05f);
        Check(!returning.Sample(true, true, false, .05f), "An equipped tool still rising from the feet is not revealed by elapsed time alone");
        returning.Sample(true, true, true, .05f);
        Check(!returning.Sample(true, true, true, .05f), "A briefly stationary return pose does not reveal the tool");
        Check(returning.Sample(true, true, true, .05f), "Settled equipped tool becomes visible");
        Check(returning.Sample(true, true, false, .05f), "Normal shake after return does not flicker the tool");
        Check(!returning.Sample(true, false, true, .05f), "A second grab immediately hides and resets the tool");
        Check(!returning.Sample(true, true, true, 1f), "A hitch cannot skip the return gate");
        Check(!returning.Sample(true, true, true, 0f), "Paused animation cannot finish the return gate");
        Check(!returning.Sample(true, true, true, float.NaN), "Invalid frame time cannot reveal the tool");
        Check(!returning.Sample(false, false, false, .01f), "Switching away clears a pending stow return");
        Check(returning.Sample(true, true, false, .01f), "Switching back shows the held tool immediately even while native pose is moving");
        var nextTool = new ToolReturnVisibility();
        Check(!nextTool.Sample(false, false, false, .01f), "An unselected tool stays hidden");
        Check(nextTool.Sample(true, true, false, .01f), "Newly selected held tool has no minimum delay");
        Check(!nextTool.Sample(true, false, false, .01f), "New selection cannot reveal a tool while the hand is still occupied");
        Check(!nextTool.Sample(true, true, false, .01f), "Release after switching during a grab still waits for return");
        var nearAim = PresentationGeometry.CouchMarkerSize(3);
        var farAim = PresentationGeometry.CouchMarkerSize(200);
        Check(Math.Abs(nearAim.x / 3 - farAim.x / 200) < .00001f, "Couch aiming ring retains angular radius at 200 m grapple range");
        Check(Math.Abs(nearAim.y / 3 - farAim.y / 200) < .00001f, "Couch aiming stroke stays readable at 200 m instead of shrinking to a fixed 3 mm");
        var closeAim = PresentationGeometry.CouchMarkerSize(.1f);
        Check(closeAim.x >= .006f && closeAim.y >= .0015f, "Very close target retains a usable minimum aim marker");
        var eye = new Vector3(10, 20, 30);
        var target = eye + new Vector3(30, 0, 200);
        var clippedAim = PresentationGeometry.CouchMarkerPosition(eye, target, .03f, 100);
        Check((clippedAim - eye).magnitude < 100 && Vector3.Dot((clippedAim - eye).normalized, (target - eye).normalized) > .9999f, "Reticle beyond camera far clip stays visible without changing its aim bearing");
        var closeTarget = eye + Vector3.forward * .1f;
        Check((PresentationGeometry.CouchMarkerPosition(eye, closeTarget, .2f, 100) - eye).magnitude > .2f, "Near target cannot put couch reticle inside camera near clip");
        var visibleTarget = eye + Vector3.forward * 10;
        Check((PresentationGeometry.CouchMarkerPosition(eye, visibleTarget, .03f, 100) - visibleTarget).sqrMagnitude < .00001f, "Visible target retains its real stereo depth");
        Check(PresentationGeometry.IsCriticalHelmetEffect("FX_ShatteredGlass_Helmet_01(Clone)"), "Actual critical red helmet prefab uses critical fitting");
        Check(!PresentationGeometry.IsCriticalHelmetEffect("FX_VisorCrack(Clone)"), "Larger white quad must not receive critical red enlargement");
        Check(!PresentationGeometry.IsCriticalHelmetEffect("FX_VisorCrack_Small_04(Clone)"), "Small white cracks retain native size");
        var criticalFit = PresentationGeometry.CriticalHelmetFit(new Vector2(.34f, .24f), .2f, new Rect(-33, -34.5f, 66, 58), 20);
        Check(.34f * criticalFit.x >= 1.3199f && .24f * criticalFit.y >= 1.1599f, "Critical mesh covers both headset views with room for its rounded boundary");
        Check(Math.Abs(PresentationGeometry.DamageAxisScale(Vector3.forward, criticalFit) - 1) < .00001f, "Critical fitting does not scale the visor depth axis");
        Check(Math.Abs(PresentationGeometry.DamageAxisScale(Vector3.up, criticalFit) - criticalFit.y) < .00001f, "Native critical mesh vertical axis receives vertical fit");
        Check(PresentationGeometry.DamageSpriteBounds(new Vector3(.01f, -.015f, .1f), new Vector2(.02f, .03f), 20, out var nativeCrack), "Native white crack can move to comfortable depth");
        Check(Math.Abs(nativeCrack.width / 20 - .02f / .1f) < .00001f && Math.Abs(nativeCrack.height / 20 - .03f / .1f) < .00001f, "White crack preserves original angular size independently of XR FOV");
        Check(Math.Abs(nativeCrack.center.x / 20 - .01f / .1f) < .00001f && Math.Abs(nativeCrack.center.y / 20 + .015f / .1f) < .00001f, "White crack retains native offset about head forward, not asymmetric coverage center");
        Check(PresentationGeometry.DamageSpriteBounds(new Vector3(.01f, -.015f, .1f), new Vector2(.02f, .03f), 10, out var closerCrack) && Math.Abs(closerCrack.width * 2 - nativeCrack.width) < .00001f, "Changing visual depth alone cannot enlarge white cracks");
        Check(!PresentationGeometry.DamageSpriteBounds(new Vector3(0, 0, -1), Vector2.one, 20, out _), "Behind-camera damage is rejected");
        Check(!PresentationGeometry.DamageSpriteBounds(new Vector3(float.NaN, 0, 1), Vector2.one, 20, out _), "Invalid native damage position cannot break presentation");
        var leftEye = Matrix4x4.identity; leftEye.m22 = -1; leftEye.m03 = -.032f;
        var rightEye = leftEye; rightEye.m03 = .032f;
        var projection = Matrix4x4.identity; projection.m32 = -1; projection.m33 = 0;
        var leftProjection = projection; leftProjection.m02 = -.2f; leftProjection.m12 = -.1f;
        var rightProjection = projection; rightProjection.m02 = .3f; rightProjection.m12 = .2f;
        Check(PresentationGeometry.DamageProjectionPoint(leftProjection, leftEye, new Vector2(-1, 1), 1.15f, out var leftCorner), "Left XR render projection intersects common damage plane");
        Check(Math.Abs(leftCorner.x - (-1.412f)) < .00001f && Math.Abs(leftCorner.y - 1.035f) < .00001f, "Damage coverage retains asymmetric projection and left eye translation");
        Check(PresentationGeometry.DamageProjectionPoint(rightProjection, rightEye, new Vector2(1, -1), 1.15f, out var rightCorner), "Right XR render projection intersects common damage plane");
        Check(Math.Abs(rightCorner.x - 1.527f) < .00001f && Math.Abs(rightCorner.y + .92f) < .00001f, "Right coverage uses its own frustum and eye translation without mirroring");
        leftEye.m23 = .15f;
        Check(PresentationGeometry.DamageProjectionPoint(projection, leftEye, Vector2.one, 1.15f, out var shifted) && Math.Abs(shifted.x - .968f) < .00001f, "Eye depth offset intersects the shared head plane rather than parallel eye planes");
        Check(!PresentationGeometry.DamageProjectionPoint(projection, Matrix4x4.identity, Vector2.one, 1.15f, out _), "Backward eye ray cannot produce invalid coverage");
        Check(!PresentationGeometry.DamageProjectionPoint(projection, rightEye, new Vector2(float.NaN, 1), 1.15f, out _), "Non-finite XR corner is rejected");
        Check(!PresentationGeometry.DamageProjectionPoint(Matrix4x4.zero, rightEye, Vector2.one, 20, out _), "Absent XR projection cannot poison coverage");
        Check(!PresentationGeometry.DamageProjectionPoint(Matrix4x4.identity, rightEye, Vector2.one, 20, out _), "Identity placeholder is not treated as a perspective eye projection");
        Check(PresentationGeometry.DamageProjectionPoint(rightProjection, rightEye, new Vector2(1, -1), 20, out var farCorner) && Math.Abs(farCorner.x - 26.032f) < .0001f && Math.Abs(farCorner.y + 16) < .0001f, "Valid XR data works after missing data and scales to far damage depth");
        var cantedEye = Matrix4x4.identity;
        cantedEye.m00 = .8f; cantedEye.m02 = -.6f; cantedEye.m20 = -.6f; cantedEye.m22 = -.8f;
        Check(PresentationGeometry.DamageProjectionPoint(projection, cantedEye, new Vector2(1, 0), 20, out var cantedCorner) && Math.Abs(cantedCorner.x - 140) < .001f, "Canted eye coverage intersects the head plane instead of assuming parallel eye views");
        var curveCache = new HudCurveCache();
        var relative = Matrix4x4.identity;
        var pixelScale = new Vector3(.001f, .001f, .001f);
        Check(!curveCache.Matches(relative, pixelScale, Vector2.zero, 1.8f), "Empty curve cache cannot reuse mesh");
        curveCache.Store(relative, pixelScale, Vector2.zero, 1.8f);
        Check(curveCache.Matches(relative, pixelScale, Vector2.zero, 1.8f), "Unchanged local HUD geometry can reuse mesh while root moves");
        var animated = relative; animated.m03 = .000001f;
        Check(!curveCache.Matches(animated, pixelScale, Vector2.zero, 1.8f), "Even small child translation invalidates geometry");
        animated = relative; animated.m01 = .01f;
        Check(!curveCache.Matches(animated, pixelScale, Vector2.zero, 1.8f), "Child rotation/shear invalidates geometry");
        Check(!curveCache.Matches(relative, pixelScale, Vector2.one, 1.8f), "Changed canvas center invalidates geometry");
        Check(!curveCache.Matches(relative, pixelScale, Vector2.zero, 2f), "Changed visor radius invalidates geometry");
        Check(!curveCache.Matches(relative, pixelScale * 1.02f, Vector2.zero, 1.8f), "HUD size preference invalidates geometry");
        Check(curveCache.Matches(relative, pixelScale * 1.0000005f, Vector2.zero, 1.8f), "Rigid-root lossy-scale rounding does not cause rebuild");
        Check(!curveCache.Matches(relative, pixelScale * 1.000003f, Vector2.zero, 1.8f), "Scale drift is measured from saved key, not previous hit");
        Check(!curveCache.Matches(relative, Vector3.zero, Vector2.zero, 1.8f), "Collapsed animation scale cannot reuse cache");
        animated = relative; animated.m00 = float.NaN;
        Check(!curveCache.Matches(animated, pixelScale, Vector2.zero, 1.8f), "Invalid transform cannot reuse cache");
        curveCache.Invalidate();
        Check(!curveCache.Matches(relative, pixelScale, Vector2.zero, 1.8f), "New native mesh submission invalidates unchanged layout");
        var viewport = new Rect(-960, -540, 1920, 1080);
        var innerClip = new Rect(-300, -200, 600, 400);
        Check(PresentationGeometry.IntersectClip(viewport, innerClip) == innerClip, "Frontend clipping preserves a smaller native scroll mask");
        var partialClip = PresentationGeometry.IntersectClip(viewport, new Rect(800, -100, 400, 200));
        Check(partialClip == new Rect(800, -100, 160, 200), "Ship list crossing right edge is clipped to frontend viewport");
        Check(PresentationGeometry.IntersectClip(viewport, new Rect(1000, 0, 300, 300)) == Rect.zero, "Off-panel ship card yields empty clip rather than disabling clipping");
        Check(PresentationGeometry.IntersectClip(viewport, viewport) == viewport, "Viewport clipping does not shrink visible menu");
        Check(PresentationGeometry.IntersectClip(viewport, new Rect(960, 0, 30, 20)) == Rect.zero, "A card touching the viewport edge cannot leak beyond it");
        var aimOrigin = new Vector3(3, 4, 5);
        foreach (var rotation in new[] { Identity, new Quaternion(0, .70710678f, 0, .70710678f),
            new Quaternion(.70710678f, 0, 0, .70710678f), new Quaternion(0, 0, .70710678f, .70710678f) })
        {
            var direction = rotation * Vector3.forward;
            var desired = aimOrigin + rotation * new Vector3(.03f, -.08f, -.12f);
            var muzzleOffset = rotation * new Vector3(-.015f, .1f, .3f);
            var aligned = PresentationGeometry.AlignedToolOrigin(desired, muzzleOffset, aimOrigin, direction);
            Near(aligned + muzzleOffset, aimOrigin + direction * .18f, "Muzzle lies on aim line while retaining forward grip calibration");
            Near(Vector3.Cross(aligned + muzzleOffset - aimOrigin, direction), Vector3.zero, "Beam has no transverse muzzle error after rotation");
            var extents = new Vector3(.12f, .3f, .18f); var center = new Vector3(.4f, -.2f, .7f);
            var topOrigin = PresentationGeometry.TopAlignedPropOrigin(aimOrigin, rotation, center, extents, .65f, Vector3.up);
            var maxHeight = float.NegativeInfinity;
            for (var corner = 0; corner < 8; corner++)
            {
                var point = center + new Vector3((corner & 1) == 0 ? -extents.x : extents.x,
                    (corner & 2) == 0 ? -extents.y : extents.y, (corner & 4) == 0 ? -extents.z : extents.z);
                maxHeight = Math.Max(maxHeight, Vector3.Dot(topOrigin + rotation * (point * .65f) - aimOrigin, Vector3.up));
            }
            Check(Math.Abs(maxHeight) < .00001f, "Rotated detonator bounds top stays at tool aim height");
        }
        Near(PresentationGeometry.VisorPoint(0, 0, 1.8f), Vector3.zero, "Spherical HUD center does not move");
        Near(PresentationGeometry.VisorPoint(.8f, .5f, 0), new Vector3(.8f, .5f, 0), "Zero spherical radius restores flat coordinates");
        Near(PresentationGeometry.VisorPoint(.8f, .5f, float.NaN), new Vector3(.8f, .5f, 0), "Invalid radius cannot poison UI vertices");
        var cornerPoint = PresentationGeometry.VisorPoint(.8f, .5f, 1.8f);
        var oppositePoint = PresentationGeometry.VisorPoint(-.8f, -.5f, 1.8f);
        Near(oppositePoint, new Vector3(-cornerPoint.x, -cornerPoint.y, cornerPoint.z), "Spherical HUD is symmetric");
        Check(Math.Abs((cornerPoint + Vector3.forward * 1.8f).magnitude - 1.8f) < .00001f, "HUD vertices lie on actual spherical surface");
        Check(cornerPoint.x < .8f && cornerPoint.y < .5f && cornerPoint.z < -.1f, "Curvature changes lateral coordinates as well as depth");
        var topPoint = PresentationGeometry.VisorPoint(0, .5f, 1.8f);
        Check(cornerPoint.y / (1.3f + cornerPoint.z) > topPoint.y / (1.3f + topPoint.z), "Top edge visibly bows in perspective instead of remaining a straight rectangle");
        Check(PresentationGeometry.VisorPoint(.8f, .5f, 3f).z > cornerPoint.z, "Increasing radius flattens spherical curve");
        Near(PresentationGeometry.VisorPoint(.8f, .5f, .5f), cornerPoint, "Tiny configured radius respects the curvature safety limit");
        foreach (var rank in new[] { 0, 1, 999, int.MaxValue })
        {
            Check(PresentationGeometry.UiSortOrder(false, rank) < PresentationGeometry.LoadingBackdropOrder, "Normal HUD stays behind static during loading, rank " + rank);
            Check(PresentationGeometry.UiSortOrder(true, rank) > PresentationGeometry.LoadingBackdropOrder, "Loading root and nested text remain above static after every sorter callback, rank " + rank);
        }
        Check(PresentationGeometry.UiSortOrder(true, 1) > PresentationGeometry.UiSortOrder(true, 0), "Loading child order is preserved inside reserved text band");
        Check(PresentationGeometry.UiSortOrder(true, int.MaxValue) < 32767, "Loading order remains valid in Unity signed-short range");
        Check(PresentationGeometry.VisorDepth(0, .5f, 2.4f) < 0, "Visor wraps vertically as well as horizontally");
        Check(Math.Abs(PresentationGeometry.VisorDepth(1, .5f, 2.4f) - PresentationGeometry.VisorDepth(-1, -.5f, 2.4f)) < .00001f, "Visor is symmetric across both axes");
        Check(PresentationGeometry.VisorDepth(1, .5f, 0) == 0, "Zero visor radius leaves flat UI");
        foreach (var steps in new[] { 1, 2, 16 })
        {
            var grid = new List<Vector2>(); var indices = new List<int>();
            for (var row = 0; row <= steps; row++) for (var col = 0; col <= steps - row; col++)
                grid.Add(new Vector2((float)row / steps, (float)col / steps));
            PresentationGeometry.AppendTriangleGrid(indices, steps, 5);
            Check(indices.Count == 3 * steps * steps, "UI subdivision triangle count at " + steps);
            var valid = true; var area = 0f;
            for (var i = 0; i < indices.Count; i += 3)
            {
                for (var j = 0; j < 3; j++) valid &= indices[i+j] >= 5 && indices[i+j] < grid.Count + 5;
                if (!valid) break;
                var a = grid[indices[i]-5]; var b = grid[indices[i+1]-5]; var c = grid[indices[i+2]-5];
                var signedArea = ((b.x-a.x)*(c.y-a.y)-(b.y-a.y)*(c.x-a.x))*.5f;
                valid &= signedArea > 0; area += signedArea;
            }
            Check(valid, "UI subdivision indices and front-face winding at " + steps);
            Check(Math.Abs(area - .5f) < .00001f, "UI subdivision covers original triangle without holes at " + steps);
        }
        Check(PresentationGeometry.HudDepth(0, 8) == 0, "HUD center keeps its established distance");
        Check(Math.Abs(PresentationGeometry.HudDepth(1f, 8) + .0625f) < .00001f, "2m HUD edges wrap toward viewer by only 6.25cm");
        Check(PresentationGeometry.HudDepth(-1f, 8) == PresentationGeometry.HudDepth(1f, 8), "Helmet curve is symmetric");
        Check(PresentationGeometry.HudDepth(1, 0) == 0, "Zero radius disables curve");
        Check(PresentationGeometry.HandAxes(Vector3.zero, new Vector3(0,0,.1f), new Vector3(-.025f,0,.1f), new Vector3(.025f,0,.1f), false, out var forward, out var up), "Right glove frame valid");
        Near(forward, Vector3.forward, "Right fingers point forward"); Near(up, Vector3.up, "Right glove back faces up");
        Check(PresentationGeometry.HandAxes(Vector3.zero, new Vector3(0,0,.1f), new Vector3(.025f,0,.1f), new Vector3(-.025f,0,.1f), true, out forward, out up), "Left glove frame valid");
        Near(forward, Vector3.forward, "Left fingers point forward"); Near(up, Vector3.up, "Left glove back faces up without reflection");
        Check(!PresentationGeometry.HandAxes(Vector3.zero, Vector3.zero, Vector3.zero, Vector3.zero, true, out forward, out up), "Uninitialized hand pose rejected");
        var q = new Quaternion(0, .70710678f, 0, .70710678f);
        var grip = new Vector3(5, 2, 3);
        var offset = new Vector3(0, -.045f, .02f);
        var propCenter = new Vector3(.4f, -.2f, 1.1f);
        var propOrigin = PresentationGeometry.CentredPropOrigin(grip, q, propCenter, .65f);
        Near(propOrigin + q * (propCenter * .65f), grip, "Native arm translation cannot push prop centre away from tracked grip");
        Near(PresentationGeometry.CentredPropOrigin(grip, q, Vector3.zero, .65f), grip, "Centred source prop retains tracked position");
        var offsetCenter = propCenter + new Vector3(3, 2, 1);
        Near(PresentationGeometry.CentredPropOrigin(grip, q, offsetCenter, .65f) + q * (offsetCenter * .65f), grip, "Animated source translation leaves prop centre stable");
        Near(PresentationGeometry.ToolPivot(grip, Identity, offset + Vector3.up * .025f, .14f), grip + new Vector3(0, -.02f, -.12f), "Grapple lift raises model without changing forward calibration");
        Check(PresentationGeometry.CompareUiOrder(-1, 20000, 0, 0) < 0, "Native UI layer precedence is preserved when raising all panels over world geometry");
        Check(PresentationGeometry.CompareUiOrder(0, 10, 0, 2) > 0, "Popup order stays above helmet on the same native layer");
        Near(PresentationGeometry.ToolPivot(grip, Identity, offset, .14f), grip + new Vector3(0, -.045f, -.12f), "Grapple rear pivot moves backward to seat middle handle");
        Near(PresentationGeometry.ToolPivot(grip, q, offset, .14f), grip + new Vector3(-.12f, -.045f, 0), "Handle correction follows controller yaw, not world forward");
        Near(PresentationGeometry.ToolPivot(grip, q, offset, 0), grip + q * offset, "Zero handle correction retains prior calibration");
        Check(PresentationGeometry.HandAxes(new Vector3(5,2,3), new Vector3(5,2,3)+q*new Vector3(0,0,.1f), new Vector3(5,2,3)+q*new Vector3(-.025f,0,.1f), new Vector3(5,2,3)+q*new Vector3(.025f,0,.1f), false, out forward, out up), "Glove extraction supports translated/rotated native skeletons");
        Near(forward, Vector3.right, "Rotated glove frame follows fingers"); Near(up, Vector3.up, "Rotated glove back is stable");
        Check(PresentationGeometry.Endpoint(10, 4) == 4, "Surface caps guide before max reach");
        Check(PresentationGeometry.Endpoint(10, 30) == 10, "Beyond-range hit cannot extend guide");
        Check(PresentationGeometry.Endpoint(10, -1) == 10, "No hit shows full reach");
        Check(PresentationGeometry.Endpoint(15, -1) == 15, "Upgrade changes full reach");
        Check(PresentationGeometry.Endpoint(10, 0) == 0, "Contact does not project through surface");
        Check(!PresentationGeometry.ValidRange(float.NaN) && !PresentationGeometry.ValidRange(float.PositiveInfinity) && !PresentationGeometry.ValidRange(0), "Missing/invalid ranges never create a guessed guide");
    }

    private static void CheckHudPatchTargets()
    {
        var mouseMethod = typeof(UnityEngine.EventSystems.StandaloneInputModule).GetMethod("ProcessMouseEvent",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic,
            null, new[] { typeof(int) }, null);
        Check(mouseMethod != null && mouseMethod.ReturnType == typeof(void), "Menu mouse suppression resolves the exact native int overload");
        var targets = HudMeshPatchTargets.Resolve();
        Check(targets.Count == 10, "All ten UI/TMP mesh targets resolve using production startup lookup");
        var geometryCount = 0;
        foreach (var entry in targets)
        {
            var method = entry.Key;
            var expectedOwner = method.Name == "DoMeshGeneration" || method.Name == "DoLegacyMeshGeneration" ?
                typeof(UnityEngine.UI.Graphic) : typeof(TMPro.TextMeshProUGUI);
            Check(method.DeclaringType == expectedOwner, "Mesh hook stays on declared owner: " + method.Name);
            if (method.Name != "UpdateGeometry") continue;
            geometryCount++;
            var parameters = method.GetParameters();
            Check(parameters.Length == 2 && parameters[0].ParameterType == typeof(Mesh) && parameters[1].ParameterType == typeof(int),
                "Regression: UpdateGeometry targets TMP (Mesh,int), never inherited Graphic ()");
        }
        Check(geometryCount == 1, "Only the intended UpdateGeometry overload is targeted");
    }

    private static void Main()
    {
        try { Run(); }
        catch (Exception error) { Console.Error.WriteLine(error); Environment.ExitCode = 1; }
    }
    private static void Run()
    {
        LegacyJoystickChecks.Run(Check);
        InputModeChecks.Run(Check);
        CheckSplitGamepad();
        CheckHudPatchTargets();
        CheckPresentation();
        CheckHaptics();
        var baseRoomScale = new Vector3(.5f, .5f, .5f);
        var panelScale = new Vector3(.001f, .002f, .003f);
        var roomLocal = RoomMarkerGeometry.LocalScale(baseRoomScale, panelScale, 10, .00065f);
        Near(Vector3.Scale(roomLocal, panelScale), Vector3.one * .00325f, "Room label world size is independent of parent HUD scale");
        Near(RoomMarkerGeometry.LocalScale(baseRoomScale, Vector3.one, 0, .00065f), Vector3.one * .00065f, "Nearby room labels have a bounded minimum size");
        Near(RoomMarkerGeometry.LocalScale(baseRoomScale, Vector3.one, 1000, .00065f), Vector3.one * .0065f, "Distant room labels stop growing beyond 20 metres");
        Near(RoomMarkerGeometry.LocalScale(baseRoomScale, -panelScale, 10, .00065f), roomLocal, "Reflected parent scale cannot invert label sizing");
        var zeroParent = RoomMarkerGeometry.LocalScale(baseRoomScale, Vector3.zero, 10, .00065f);
        Check(!float.IsInfinity(zeroParent.x) && !float.IsNaN(zeroParent.x), "Zero parent scale cannot introduce infinity into room labels");
        CheckNewAimingAndToggle();
        var head = new TrackedPose(new Vector3(1, 1.7f, 2), Identity);
        var hand = new TrackedPose(new Vector3(1.3f, 1.2f, 2.5f), Identity);
        var body = new Vector3(10, 20, 30);
        var eye = new Vector3(0, 0, .2f);
        var world = ShipbreakerTrackingSpace.ToWorld(head, hand, body, Identity, eye);
        Near(world.position, new Vector3(10.3f, 19.5f, 30.7f), "Standing-height offset is removed");

        var yaw90 = new Quaternion(0, .70710678f, 0, .70710678f);
        world = ShipbreakerTrackingSpace.ToWorld(head, hand, body, yaw90, eye);
        Near(world.position, new Vector3(10.7f, 19.5f, 29.7f), "Body rotation affects position once");
        Near(world.rotation * Vector3.forward, Vector3.right, "Body rotation affects aim once");

        var turnedHead = new TrackedPose(head.Position, yaw90);
        var unturned = ShipbreakerTrackingSpace.ToWorld(head, hand, body, Identity, eye);
        var turned = ShipbreakerTrackingSpace.ToWorld(turnedHead, hand, body, Identity, eye);
        Near(turned.position, unturned.position, "Head rotation cannot swing world-space hands");
        Near(turned.rotation * Vector3.forward, Vector3.forward, "Aim cannot inherit HMD rotation twice");

        var offset = new Vector3(7, 3, -6);
        var recentered = ShipbreakerTrackingSpace.ToWorld(new TrackedPose(head.Position + offset, Identity),
            new TrackedPose(hand.Position + offset, Identity), body, Identity, eye);
        Near(recentered.position, unturned.position, "Common tracking-origin translations cancel");
        Check(TrackedPose.FromSample(true, Full, hand.Position, Identity).IsValid, "Valid six-DOF pose accepted");
        Check(!TrackedPose.FromSample(false, Full, hand.Position, Identity).IsValid, "Tracking loss rejected");
        Check(!TrackedPose.FromSample(true, InputTrackingState.Rotation, hand.Position, Identity).IsValid, "Orientation-only pose rejected");
        Check(!TrackedPose.FromSample(true, InputTrackingState.Position, hand.Position, Identity).IsValid, "Position-only pose rejected");
        Check(!TrackedPose.FromSample(true, Full, new Vector3(float.NaN, 0, 0), Identity).IsValid, "Nonfinite position rejected");
        Check(!TrackedPose.FromSample(true, Full, hand.Position, default).IsValid, "Zero quaternion rejected");
        Check(!default(TrackedPose).IsValid, "Cleared pose is invalid");
        var updates = new InputUpdateWatchdog(100);
        Check(!updates.TryPump(100) && !updates.TryPump(101), "Wait for the host to start input updates");
        Check(updates.TryPump(102), "Pump input when the host does not update");
        Check(!updates.TryPump(102), "LateUpdate and before-render cannot pump twice in one frame");
        Check(updates.TryPump(103), "Continue once per frame without host updates");
        updates.ObserveExternalUpdate(104);
        Check(!updates.TryPump(104) && !updates.TryPump(105), "Host recovery suspends fallback updates");
        Check(updates.TryPump(106), "Resume fallback if host updates stop again");
        Check(NativeAimFeature.Classify("pointer/position") == NativeAimFeatureKind.Position, "Native pointer action position recognized");
        Check(NativeAimFeature.Classify("PointerRotation") == NativeAimFeatureKind.Rotation, "XR SDK pointer rotation alias recognized");
        Check(NativeAimFeature.Classify("pointer/isTracked") == NativeAimFeatureKind.IsTracked, "Independent pointer validity recognized");
        Check(NativeAimFeature.Classify("pointer/trackingState") == NativeAimFeatureKind.TrackingState, "Independent pointer tracking flags recognized");
        Check(NativeAimFeature.Classify("DevicePosition") == NativeAimFeatureKind.None && NativeAimFeature.Classify("devicePose/position") == NativeAimFeatureKind.None, "Grip position cannot become aim position");
        Check(NativeAimFeature.Classify("CenterEyeRotation") == NativeAimFeatureKind.None && NativeAimFeature.Classify("TrackingState") == NativeAimFeatureKind.None, "Head and overall device state cannot become pointer state");
        Check(NativeAimFeature.CanUseSharedValidity(true, true, false, false, true, true), "Legacy provider with pointer poses and shared tracking flags is supported");
        Check(!NativeAimFeature.CanUseSharedValidity(false, true, false, false, true, true) && !NativeAimFeature.CanUseSharedValidity(true, false, false, false, true, true), "Both pointer components are required even with shared flags");
        Check(!NativeAimFeature.CanUseSharedValidity(true, true, true, true, true, true), "Independent pointer validity takes precedence");
        Check(!NativeAimFeature.CanUseSharedValidity(true, true, true, false, true, true) && !NativeAimFeature.CanUseSharedValidity(true, true, false, true, true, true), "Partial independent metadata cannot silently mix with shared flags");
        Check(!NativeAimFeature.CanUseSharedValidity(true, true, false, false, false, true) && !NativeAimFeature.CanUseSharedValidity(true, true, false, false, true, false), "Both shared validity fields are required");
        var requested = new Flag { Value = true };
        var alreadyOff = new Flag { Value = false };
        var writes = 0;
        var overrides = new TemporaryBooleanOverrides<Flag>(flag => flag.Value, (flag, value) => { flag.Value = value; writes++; });
        Check(overrides.Suppress(requested) && !requested.Value, "Enabled flag is suppressed for a render frame");
        Check(!overrides.Suppress(requested) && writes == 1, "Repeated frame acquisition preserves the original flag");
        Check(!overrides.Suppress(alreadyOff) && writes == 1, "Owner-disabled flag is not changed");
        overrides.Restore();
        Check(requested.Value && !alreadyOff.Value && writes == 2, "Frame end restores only flags this override changed");
        overrides.Restore();
        Check(writes == 2, "Repeated cleanup after disable has no extra writes");
        requested.Value = false;
        Check(!overrides.Suppress(requested), "An owner change between frames is respected");
        overrides.Restore();
        Check(!requested.Value, "Previously enabled state cannot leak into a later disabled frame");
        requested.Value = true;
        Check(overrides.Suppress(requested), "Override can be reacquired in a later VR session");
        overrides.Restore();
        Check(requested.Value, "Recovery cleanup restores an unfinished frame");
        var trigger = new TriggerLatch();
        trigger.Sample(true, 1f);
        Check(!trigger.Held && !trigger.Pressed, "Held trigger on entering gameplay cannot fire");
        trigger.Sample(true, 0f);
        trigger.Sample(true, .8f);
        Check(trigger.Held && trigger.Pressed, "Release then squeeze starts firing");
        trigger.Sample(true, 1f);
        Check(trigger.Held && !trigger.Pressed, "Holding does not repeat press edges");
        trigger.Sample(true, .5f);
        Check(trigger.Held && !trigger.Pressed, "Hysteresis avoids trigger chatter");
        trigger.Sample(true, .25f);
        Check(!trigger.Held && !trigger.Pressed, "Release threshold stops firing");
        trigger.Sample(true, .75f);
        Check(trigger.Held && trigger.Pressed, "Next deliberate press is recognized");
        trigger.Sample(false, 1f);
        Check(!trigger.Held && !trigger.Pressed, "Tracking/context loss stops firing immediately");
        trigger.Sample(true, 1f);
        Check(!trigger.Held && !trigger.Pressed, "Recovery while held cannot resume firing");
        trigger.Sample(true, .5f);
        Check(!trigger.Held, "Partial release cannot rearm after tracking loss");
        trigger.Sample(true, 0f);
        trigger.Sample(true, 1f);
        Check(trigger.Held && trigger.Pressed, "Full release rearms recovered input");
        trigger.Sample(true, float.NaN);
        Check(!trigger.Held && !trigger.Pressed, "Nonfinite trigger input cancels firing");
        trigger.Sample(true, 0f);
        trigger.Sample(true, float.PositiveInfinity);
        Check(!trigger.Held && !trigger.Pressed, "Infinite trigger input cannot fire");
        var movement = new MovementNeutralGate();
        Check(!movement.Sample(true, Vector2.up, Vector2.zero, false, .15f), "Held stick cannot move on entry");
        Check(!movement.Sample(true, Vector2.zero, Vector2.zero, true, .15f), "Held movement button blocks initial arming");
        Check(movement.Sample(true, Vector2.zero, Vector2.zero, false, .15f), "Neutral movement controls arm");
        Check(movement.Sample(true, Vector2.up, Vector2.right, true, .15f), "Armed controls support simultaneous translation, turning and buttons");
        Check(!movement.Sample(false, Vector2.up, Vector2.right, true, .15f), "Focus/tracking/context loss disarms movement");
        Check(!movement.Sample(true, Vector2.up, Vector2.zero, false, .15f), "Recovery with displaced stick requires centering");
        Check(!movement.Sample(true, Vector2.zero, Vector2.right, false, .15f), "Turning stick also must return to center");
        Check(movement.Sample(true, Vector2.zero, Vector2.zero, false, .15f), "Recovery with centered sticks rearms");
        Check(!movement.Sample(true, new Vector2(float.NaN, 0), Vector2.zero, false, .15f), "Nonfinite axis disarms movement");
        Check(MovementInput.Deadzone(new Vector2(.1f, 0), .15f) == Vector2.zero, "Stick drift stays neutral");
        Check(MovementInput.Deadzone(Vector2.right, .15f) == Vector2.right, "Full stick retains full thrust");
        Check(MovementInput.Deadzone(new Vector2(1, 1), .15f).magnitude <= 1.0001f, "Diagonal stick magnitude stays bounded");
        Check(MovementInput.Deadzone(new Vector2(float.PositiveInfinity, 0), .15f) == Vector2.zero, "Infinite axis becomes neutral");
        var leftInput = new ControllerInputs(true, Vector2.zero, false, false, true, false);
        var rightInput = new ControllerInputs(true, Vector2.zero, true, false, true, false);
        Check(MovementInput.Button(VrButton.LeftStickClick, leftInput, rightInput) && MovementInput.Button(VrButton.RightStickClick, leftInput, rightInput), "Both stick clicks are available for gamepad brake chord");
        Check(MovementInput.Button(VrButton.RightPrimary, leftInput, rightInput) && !MovementInput.Button(VrButton.RightSecondary, leftInput, rightInput), "Primary and secondary buttons remain distinct");
        Check(!MovementInput.Button(VrButton.None, leftInput, rightInput), "Unbound extra brake cannot activate");
        Check(!MovementInput.Button(VrButton.LeftPrimary, leftInput, rightInput), "Right primary cannot activate a left binding");
        var menuGate = new MenuNeutralGate();
        Check(!menuGate.Sample(1, true, Vector2.zero, Vector2.zero, true, .25f), "Held confirm cannot activate a menu on VR entry");
        Check(menuGate.Sample(1, true, Vector2.zero, Vector2.zero, false, .25f), "Released menu buttons arm input");
        Check(menuGate.Sample(1, true, Vector2.up, Vector2.zero, true, .25f), "Armed tool wheel accepts a held button and stick together");
        Check(!menuGate.Sample(2, true, Vector2.up, Vector2.zero, true, .25f), "Gameplay to menu transition cannot carry held input");
        Check(!menuGate.Sample(2, true, Vector2.zero, Vector2.zero, true, .25f), "Centering alone cannot rearm held confirmation");
        Check(menuGate.Sample(2, true, Vector2.zero, Vector2.zero, false, .25f), "Release rearms the new menu context");
        Check(!menuGate.Sample(2, false, Vector2.zero, Vector2.zero, false, .25f), "Tracking/focus loss clears menu input");
        Check(!menuGate.Sample(2, true, Vector2.zero, Vector2.right, false, .25f), "Held scroll stick cannot resume after tracking loss");
        Check(menuGate.Sample(2, true, Vector2.zero, Vector2.zero, false, .25f), "Neutral recovered controllers rearm menus");
        Check(!menuGate.Sample(2, true, new Vector2(float.NaN, 0), Vector2.zero, false, .25f), "Invalid navigation sample disarms menus");
        var menuInput = MenuInput.Route(false, false, Vector2.up, Vector2.right, true, true, true, true, true, false, true, true);
        Check(menuInput.Confirm && menuInput.Back && !menuInput.Start && menuInput.PreviousTab && menuInput.NextTab, "Menu context routes confirm/back/tabs without pausing");
        Check(!menuInput.Tools && !menuInput.Mode && !menuInput.Interact, "Menu buttons cannot also switch tools or interact");
        Check(menuInput.Navigate == Vector2.up && menuInput.Scroll == Vector2.right, "Navigation and scroll retain separate sticks");
        var playInput = MenuInput.Route(true, false, Vector2.up, Vector2.right, true, true, true, false, true, false, true, true);
        Check(!playInput.Confirm && !playInput.Back && !playInput.PreviousTab && !playInput.NextTab, "Gameplay A/B thrust cannot also confirm or back");
        Check(playInput.Interact && playInput.Mode && !playInput.Start, "Gameplay routes interaction and mode without pausing");
        Check(playInput.Navigate == Vector2.zero && playInput.Scroll == Vector2.zero, "Menu device cannot duplicate normal gameplay stick movement");
        var wheelInput = MenuInput.Route(true, true, Vector2.up, Vector2.right, false, false, true, true, true, false, false, false);
        Check(wheelInput.Tools && wheelInput.Navigate == Vector2.right && wheelInput.Scroll == Vector2.zero, "Tool wheel receives right stick even when left stick differs");
        Check(!wheelInput.Interact && !wheelInput.Mode, "Tool selection cannot also interact or toggle cutting mode");
        var releasedWheel = MenuInput.Route(true, true, Vector2.up, Vector2.right, false, false, false, false, false, false, false, false);
        Check(!releasedWheel.Tools && releasedWheel.Navigate == Vector2.right, "Wheel release preserves right-stick direction for native close/equip");
        var extras = MenuInput.Route(false, false, Vector2.zero, Vector2.zero, false, false, false, false, false, false, false, false, true, true);
        Check(extras.Misc1 && extras.Misc2 && !extras.Tools && !extras.Interact, "Menu contextual X/Y actions do not open tools or interact");
        var gameplayExtras = MenuInput.Route(true, false, Vector2.zero, Vector2.zero, false, false, false, false, false, false, false, false, true, true);
        Check(!gameplayExtras.Misc1 && !gameplayExtras.Misc2, "Gameplay grip and roll click cannot trigger menu shortcuts");
        var pauseInput = MenuInput.Route(true, true, Vector2.up, Vector2.right, true, true, true, true, true, true, true, true, true, true);
        Check(pauseInput.Start && !pauseInput.Tools && !pauseInput.Interact && !pauseInput.Mode && pauseInput.Navigate == Vector2.zero, "Pause chord suppresses tool and movement routing");
        pauseInput = MenuInput.Route(false, false, Vector2.up, Vector2.right, true, true, true, true, true, true, true, true, true, true);
        Check(pauseInput.Start && !pauseInput.Back && !pauseInput.Confirm && !pauseInput.NextTab && !pauseInput.PreviousTab && !pauseInput.Misc1 && !pauseInput.Misc2 && pauseInput.Scroll == Vector2.zero, "Pause chord cannot also activate menu actions");
        var chord = new ButtonChord();
        chord.Sample(true, true, true);
        Check(!chord.Held, "Buttons already held on entry cannot pause");
        chord.Sample(true, false, false);
        chord.Sample(true, true, false);
        Check(!chord.Held && !chord.ConsumesButtons, "One button alone cannot pause");
        chord.Sample(true, true, true);
        Check(chord.Held && chord.ConsumesButtons, "Both buttons together activate the armed chord");
        chord.Sample(true, false, true);
        Check(!chord.Held && chord.ConsumesButtons, "Releasing one button keeps its partner suppressed");
        chord.Sample(true, true, true);
        Check(!chord.Held && chord.ConsumesButtons, "Repressing one partner cannot repeat pause");
        chord.Sample(true, false, false);
        chord.Sample(true, false, true);
        chord.Sample(true, true, true);
        Check(chord.Held, "Both released rearms chord in either press order");
        chord.Sample(false, true, true);
        chord.Sample(true, true, true);
        Check(!chord.Held, "Focus/tracking loss requires release before chord recovery");
        Check(!ButtonChord.ValidBinding(VrButton.LeftSecondary, VrButton.LeftSecondary) && !ButtonChord.ValidBinding(VrButton.None, VrButton.RightSecondary), "Same or missing buttons cannot degrade pause to a single button");
        Check(ButtonChord.ValidBinding(VrButton.LeftSecondary, VrButton.RightSecondary), "Distinct Pico Y and B chord is accepted");
        Check(DpadInput.Direction(Vector2.up) == Vector2.up && DpadInput.Direction(Vector2.down) == Vector2.down, "D-pad up/down directions are distinct");
        Check(DpadInput.Direction(Vector2.left) == Vector2.left && DpadInput.Direction(Vector2.right) == Vector2.right, "D-pad left/right directions are distinct");
        Check(DpadInput.Direction(new Vector2(.2f, .1f)) == Vector2.zero, "D-pad ignores centered stick drift");
        Check(DpadInput.Direction(new Vector2(.8f, .6f)) == Vector2.right, "D-pad diagonal chooses only its dominant direction");
        Check(DpadInput.Direction(new Vector2(float.NaN, 1)) == Vector2.zero, "D-pad rejects invalid tracking samples");
        var shifted = new ShiftedStickGate();
        Check(shifted.ForMovement(true, Vector2.up, .15f) == Vector2.zero, "D-pad shift prevents left-stick thrust");
        Check(shifted.ForMovement(false, Vector2.up, .15f) == Vector2.zero, "Releasing grip while stick held cannot start thrust");
        Check(shifted.ForMovement(false, Vector2.zero, .15f) == Vector2.zero && shifted.ForMovement(false, Vector2.up, .15f) == Vector2.up, "Centered stick restores movement after D-pad use");
        var dpadMenu = MenuInput.Route(false, false, Vector2.up, Vector2.right, false, false, false, false, false, false, false, false, false, false, false, true);
        Check(dpadMenu.Dpad == Vector2.zero && dpadMenu.Navigate == Vector2.up && dpadMenu.Scroll == Vector2.right, "Menu grip is reserved for LB while the stick continues navigation");
        var dpadGame = MenuInput.Route(true, false, Vector2.down, Vector2.right, false, false, false, false, true, false, false, false, false, false, false, true);
        Check(dpadGame.Dpad == Vector2.down && !dpadGame.Mode && dpadGame.Navigate == Vector2.zero, "D-pad mode excludes the ordinary tool-mode shortcut");
        var selectInput = MenuInput.Route(true, true, Vector2.up, Vector2.right, true, true, true, true, true, false, true, true, true, true, true, true);
        Check(selectInput.Select && !selectInput.Start && !selectInput.Confirm && !selectInput.Interact && !selectInput.Tools && selectInput.Dpad == Vector2.zero, "Select/View chord excludes confirm, tool, interaction and D-pad actions");
        var priority = MenuInput.Route(true, false, Vector2.zero, Vector2.zero, false, false, false, false, false, true, false, false, false, false, true);
        Check(priority.Start && !priority.Select, "Pause takes priority over Select when both chords are present");
        var grappleGrab = new TriggerLatch(); var grappleRetract = new TriggerLatch();
        grappleGrab.Sample(true, 1f); grappleRetract.Sample(false, 1f);
        Check(!grappleGrab.Held && !grappleRetract.Held, "Equipping with held triggers cannot grab or retract");
        grappleGrab.Sample(true, 0f); grappleGrab.Sample(true, 1f);
        grappleRetract.Sample(true, 1f);
        Check(grappleGrab.Pressed && !grappleRetract.Held, "New grapple attachment still requires left-trigger release before retract");
        grappleRetract.Sample(true, 0f); grappleRetract.Sample(true, 1f);
        Check(grappleGrab.Held && grappleRetract.Held, "Independent triggers permit held grapple plus retract");
        grappleGrab.Sample(false, 1f); grappleRetract.Sample(false, 1f);
        Check(!grappleGrab.Held && !grappleRetract.Held, "Blocked grapple context clears both trigger actions");
        var placement = new TriggerLatch();
        placement.Sample(true, 0f); placement.Sample(true, 1f); placement.Sample(true, 0f);
        Check(placement.Released && !placement.Held, "A deliberate tether release commits once");
        placement.Sample(true, 0f);
        Check(!placement.Released, "Tether release edge cannot repeat");
        placement.Sample(true, 1f); placement.Sample(false, 0f);
        Check(!placement.Released && !placement.Held, "Tracking/context loss cancels without a placement release");
        placement.Sample(true, 1f);
        Check(!placement.Pressed && !placement.Released, "Held tether cannot rearm after a context loss");
        placement.Sample(true, 0f); placement.Sample(true, 1f); placement.Sample(true, float.NaN);
        Check(!placement.Released, "Invalid trigger data cannot place a tether");
        placement.Sample(true, 0f); placement.Sample(true, 1f); placement.Sample(false, 1f); placement.Sample(true, 1f);
        Check(!placement.Held && !placement.Pressed, "New demo-charge or cutter mode requires release before fire");
        placement.Sample(true, 0f); placement.Sample(true, 1f);
        Check(placement.Pressed, "A fresh squeeze fires after the mode transition");
        var bumperMenu = MenuInput.Route(false, false, Vector2.zero, Vector2.zero, false, false, false, false, false, false, true, true);
        Check(bumperMenu.PreviousTab && bumperMenu.NextTab && !bumperMenu.Misc1 && !bumperMenu.Misc2, "Menu bumpers do not invoke X/defaults or Y");
        var faceMenu = MenuInput.Route(false, false, Vector2.zero, Vector2.zero, false, false, false, false, false, false, false, false, true, true);
        Check(faceMenu.Misc1 && faceMenu.Misc2 && !faceMenu.PreviousTab && !faceMenu.NextTab, "Menu face actions remain separate from bumper tabs");
        var triggerMenu = MenuInput.Route(false, false, Vector2.zero, Vector2.zero, false, false, false, false, false, false, false, false, leftTrigger:true, rightTrigger:true);
        Check(triggerMenu.LeftTrigger && triggerMenu.RightTrigger, "Menu trigger cycle/page inputs are available");
        var triggerGame = MenuInput.Route(true, false, Vector2.zero, Vector2.zero, false, false, false, false, false, false, false, false, leftTrigger:true, rightTrigger:true);
        Check(!triggerGame.LeftTrigger && !triggerGame.RightTrigger, "Menu triggers cannot leak into gameplay actions");
        var triggerChord = MenuInput.Route(false, false, Vector2.zero, Vector2.zero, false, false, false, false, false, false, false, false, select:true, leftTrigger:true, rightTrigger:true);
        Check(triggerChord.Select && !triggerChord.LeftTrigger && !triggerChord.RightTrigger, "View chord suppresses menu trigger cycling");
        var delayedMember = new ChordMemberDelay();
        Check(!delayedMember.Sample(true, true, false, 1f), "First X/Y press is reserved for a chord");
        Check(!delayedMember.Sample(true, true, true, 1.05f), "A completed chord cannot first emit X/defaults");
        Check(!delayedMember.Sample(true, false, false, 1.1f), "Releasing a chord cannot replay a canceled X tap");
        Check(!delayedMember.Sample(true, true, false, 2f) && delayedMember.Sample(true, false, false, 2.05f), "A quick individual X/Y tap is preserved");
        Check(!delayedMember.Sample(true, false, false, 2.06f), "A deferred tap emits only one tick");
        delayedMember.Sample(true, true, false, 3f);
        Check(delayedMember.Sample(true, true, false, 3.13f) && delayedMember.Sample(true, true, false, 3.2f), "An individual held Y reaches and holds the tool wheel");
        Check(!delayedMember.Sample(true, false, false, 3.3f), "A forwarded hold releases without a duplicate tap");
        delayedMember.Sample(true, true, false, 4f); delayedMember.Sample(false, true, false, 4.05f);
        Check(!delayedMember.Sample(true, false, false, 4.1f), "Context loss discards a pending button tap");
        Console.WriteLine($"PASS: {checks} tracking, trigger, movement, menu and frame-override checks.");
    }

    private sealed class Flag { public bool Value; }

    private static void CheckNewAimingAndToggle()
    {
        Near(ToolProjection.Direction(new Vector2(1500, 1600), new Vector2(3000, 3200), 70, 1.6f), Vector3.forward, "Headset pixel center points straight ahead");
        Near(ToolProjection.Direction(new Vector2(960, 540), new Vector2(1920, 1080), 70, 1.6f), Vector3.forward, "Desktop pixel center points straight ahead");
        Near(ToolProjection.Direction(new Vector2(1200, 400), new Vector2(1600, 1600), 70, 1.6f),
            ToolProjection.Direction(new Vector2(1440, 270), new Vector2(1920, 1080), 70, 1.6f), "Equivalent normalized cut endpoints ignore resolution");
        var left = ToolProjection.Direction(new Vector2(250, 500), new Vector2(1000, 1000), 90, 1);
        var right = ToolProjection.Direction(new Vector2(750, 500), new Vector2(1000, 1000), 90, 1);
        Near(new Vector3(-left.x, left.y, left.z), right, "Cut line endpoints are symmetric around aim");
        Near(ToolProjection.Direction(Vector2.zero, Vector2.zero, 70, 1), Vector3.forward, "Missing dimensions cannot produce invalid aim");
        Near(ToolProjection.Direction(Vector2.zero, Vector2.one, float.NaN, 1), Vector3.forward, "Nonfinite projection is rejected");
        var neutral = new ControllerInputs(true, Vector2.zero, false, false, false, false);
        var full = new ControllerInputs(true, Vector2.zero, false, false, true, true);
        var click = new ControllerInputs(true, Vector2.zero, false, false, true, false);
        var displaced = new ControllerInputs(true, Vector2.right, false, false, true, true);
        var toggle = new VrToggleGesture();
        Check(!toggle.Sample(true, full, full, 0) && !toggle.Sample(true, full, full, 3), "A held startup chord cannot toggle VR");
        toggle.Sample(true, neutral, neutral, 4);
        Check(!toggle.Sample(true, click, click, 5) && !toggle.ConsumesButtons, "Brake alone cannot begin VR toggle");
        Check(!toggle.Sample(true, full, click, 6) && toggle.ConsumesButtons, "Chord assembly reserves gameplay buttons");
        Check(!toggle.Sample(true, full, full, 7) && !toggle.Sample(true, full, full, 8.9f), "VR toggle requires a complete two-second hold");
        Check(toggle.Sample(true, full, full, 9), "Deliberate complete hold toggles VR");
        Check(!toggle.Sample(true, full, full, 15), "Holding past completion cannot toggle twice");
        toggle.Sample(true, neutral, full, 16);
        Check(!toggle.Sample(true, full, full, 20), "Partial release cannot rearm toggle");
        toggle.Sample(true, neutral, neutral, 21); toggle.Sample(true, full, full, 22);
        toggle.Sample(true, displaced, full, 23);
        Check(!toggle.Sample(true, full, full, 25) && !toggle.Sample(true, full, full, 26), "Stick movement restarts the complete hold interval");
        Check(toggle.Sample(true, full, full, 27), "Centered chord can toggle after a fresh interval");
        toggle.Sample(false, full, full, 28);
        Check(!toggle.Sample(true, full, full, 35), "Tracking or focus recovery requires full release");
        toggle.Sample(true, neutral, neutral, 36); toggle.Sample(true, full, full, 37);
        toggle.Sample(true, full, full, float.NaN);
        Check(!toggle.Sample(true, full, full, 40), "Invalid time disarms the gesture");
        var shoulder = Vector3.zero;
        foreach (var target in new[] { new Vector3(.2f, -.3f, .2f), new Vector3(0, 0, 2), Vector3.zero, Vector3.up })
        {
            Check(ArmGeometry.Solve(shoulder, target, new Vector3(.2f, -.4f, 0), .3f, .25f, out var elbow, out var wrist), "Arm solver supports near, far and parallel targets");
            Check(Math.Abs(elbow.magnitude - .3f) < .0001f && Math.Abs((wrist - elbow).magnitude - .25f) < .0001f, "Arm solver preserves bone lengths without stretching");
        }
        Check(!ArmGeometry.Solve(shoulder, Vector3.forward, Vector3.up, 0, .2f, out _, out _), "Missing bone length leaves the native arm intact");
        Check(!ArmGeometry.Solve(shoulder, new Vector3(float.NaN, 0, 0), Vector3.up, .3f, .2f, out _, out _), "Invalid hand position cannot corrupt bones");
    }

    private static void CheckHaptics()
    {
        var pulse = new HapticEnvelope();
        Check(!pulse.Sample(1, 0, out _), "Idle haptics send no device commands");
        pulse.Queue(.4f, .1f, 1);
        Check(pulse.Sample(1, 0, out var level) && Math.Abs(level - .4f) < .0001f, "Tool event starts a haptic pulse immediately");
        Check(!pulse.Sample(1.01f, 0, out _), "Sustained haptics are rate limited between refreshes");
        pulse.Queue(.7f, .05f, 1.02f);
        Check(pulse.Sample(1.02f, .5f, out level) && Math.Abs(level - .7f) < .0001f, "Overlapping event and continuous vibration use maximum rather than additive amplitude");
        Check(pulse.Sample(1.2f, 0, out level) && level == 0, "Pulse expiry explicitly stops runtimes that ignore duration");
        Check(!pulse.Sample(1.21f, 0, out _), "Idle stop is not repeatedly sent");
        Check(pulse.Sample(2, .3f, out level) && level > 0, "Continuous tool state starts vibration");
        Check(pulse.Sample(2.06f, .3f, out level) && level > 0, "Continuous state refreshes short impulses");
        pulse.Queue(1, .3f, 2.1f); pulse.Clear();
        Check(!pulse.Sample(2.15f, 0, out _), "Pause/tracking loss clears pending pulses before recovery");
        pulse.Queue(float.NaN, .1f, 3);
        Check(!pulse.Sample(3, 0, out _), "Invalid event data cannot start haptics");
        pulse.Queue(10, 50, 4);
        Check(pulse.Sample(4, 0, out level) && level == 1, "Oversized impulse amplitude is clamped");
        Check(pulse.Sample(4.31f, 0, out level) && level == 0, "Impulse duration is bounded even for oversized requests");
        pulse.Sample(5, .5f, out _);
        Check(pulse.Sample(float.NaN, .5f, out level) && level == 0, "Invalid clock requests an immediate stop");
    }

    private static void Near(Vector3 actual, Vector3 expected, string name) => Check(
        Math.Abs(actual.x - expected.x) < .0001f && Math.Abs(actual.y - expected.y) < .0001f &&
        Math.Abs(actual.z - expected.z) < .0001f, name);

    private static void CheckSplitGamepad()
    {
        ControllerInputs Pad(bool a = false, bool b = false, bool x = false, bool y = false,
            bool lb = false, bool menu = false, bool view = false, bool grip = false, Vector2 stick = default, Vector2 dpad = default) =>
            new ControllerInputs(true, stick, a, b, false, grip, true, x, y, lb, menu, view, dpad);
        MenuInput Route(ControllerInputs l, ControllerInputs r, bool play = true, bool wheel = false) => MenuInput.Gamepad(play, wheel, l, r, .25f, true, true);
        var neutral = Pad();
        var x = Route(neutral, Pad(x: true));
        Check(x.Interact && !x.Tools && !x.Back && !x.Start && !x.Select, "Frame X independently interacts without old chord delay");
        var y = Route(Pad(stick: Vector2.up), Pad(y: true, stick: Vector2.right));
        Check(y.Tools && y.Navigate == Vector2.up && y.Scroll == Vector2.zero, "Frame Y selects tools with opposite/left stick");
        Check(!y.Interact && y.Dpad == Vector2.zero, "Tool wheel consumes unrelated gameplay actions");
        var b = Route(neutral, Pad(b: true));
        Check(!b.Tools && !b.Interact && !b.Start, "Frame B cannot alias X/Y/pause");
        Check(!Route(neutral, Pad(x: true, y: true)).Select, "Frame X+Y does not invoke Pico View chord");
        Check(!Route(neutral, Pad(b: true, y: true)).Start, "Frame B+Y does not invoke Pico pause chord");
        foreach (var dir in new[] { Vector2.up, Vector2.down, Vector2.left, Vector2.right })
        {
            Check(Route(Pad(dpad: dir), neutral).Dpad == dir, "Physical D-pad routes independently of grip/stick " + dir);
            Check(Route(Pad(dpad: dir), neutral, false).Dpad == dir, "Physical D-pad works in menus " + dir);
        }
        var grips = Route(Pad(grip: true, stick: Vector2.up), Pad(grip: true));
        Check(grips.Tools && !grips.PreviousTab && !grips.NextTab && !grips.Mode && grips.Dpad == Vector2.zero, "Right full grip opens wheel and suppresses simultaneous scanner shortcut");
        var gripWheel = Route(Pad(stick: Vector2.left), Pad(grip: true));
        Check(gripWheel.Tools && gripWheel.Navigate == Vector2.left, "Right full grip duplicates Y with left-stick selection");
        Check(Route(neutral, Pad(y: true, grip: true)).Tools && Route(neutral, Pad(y: true)).Tools &&
            Route(neutral, Pad(grip: true)).Tools && !Route(neutral, neutral).Tools, "Tool wheel stays held until both Y and full grip release");
        Check(Route(Pad(grip: true), neutral).Dpad == Vector2.up, "Left full grip duplicates physical D-pad up");
        Check(Route(Pad(grip: true, dpad: Vector2.up), neutral).Dpad == Vector2.up, "Scanner aliases do not double the D-pad value");
        Check(Route(Pad(grip: true, dpad: Vector2.left), neutral).Dpad == new Vector2(-1, 1), "Scanner grip preserves horizontal physical D-pad input");
        Check(Route(Pad(grip: true), neutral, false).Dpad == Vector2.up, "Left full grip retains D-pad-up navigation in menus");
        Check(Route(neutral, Pad(grip: true), false).Misc1, "Right full grip retains contextual Y in menus");
        Check(!Route(Pad(grip: true), Pad(menu: true, grip: true)).Tools &&
            Route(Pad(grip: true), Pad(menu: true, grip: true)).Dpad == Vector2.zero, "Pause suppresses both grip aliases");
        var bumpers = Route(Pad(lb: true), Pad(lb: true));
        Check(bumpers.PreviousTab && bumpers.NextTab, "Physical bumpers reach native gameplay hand actions");
        bumpers = Route(Pad(lb: true), Pad(lb: true), false);
        Check(bumpers.PreviousTab && bumpers.NextTab, "Physical bumpers reach menu tab actions");
        var pause = Route(Pad(dpad: Vector2.up), Pad(menu: true, y: true, x: true));
        Check(pause.Start && !pause.Tools && !pause.Interact && pause.Dpad == Vector2.zero && !pause.LeftTrigger, "Physical Menu consumes concurrent gameplay inputs");
        var view = Route(Pad(view: true), Pad(x: true, y: true));
        Check(view.Select && !view.Tools && !view.Interact, "Physical View consumes concurrent gameplay inputs");
        var confirm = Route(neutral, Pad(a: true), false);
        Check(confirm.Confirm && !confirm.Back, "Frame A confirms");
        Check(Route(neutral, Pad(b: true), false).Back, "Frame B backs out");
        var contextual = Route(neutral, Pad(x: true, y: true), false);
        Check(contextual.Misc1 && contextual.Misc2 && !contextual.Tools, "Frame menu contextual X/Y remain independent");
        Check(!Route(neutral, neutral).LeftTrigger && !Route(neutral, neutral).RightTrigger, "Gamepad bridge never duplicates tracked gameplay firing");
        Check(Route(neutral, neutral, false).LeftTrigger && Route(neutral, neutral, false).RightTrigger, "Menu triggers retain native page actions");
        var gate = new MenuNeutralGate();
        Check(!gate.Sample(1, true, Vector2.zero, Vector2.zero, Pad(menu: true).AnyButton, .25f), "Held Frame Menu cannot arm on context entry");
        Check(gate.Sample(1, true, Vector2.zero, Vector2.zero, neutral.AnyButton, .25f), "Frame menu arms on full neutral");
        Check(!gate.Sample(2, true, Vector2.zero, Vector2.zero, Pad(dpad: Vector2.up).AnyButton, .25f), "Held physical D-pad cannot bleed across contexts");
        Check(!new ControllerInputs(true, Vector2.zero, false, false, false, false).FullGamepad, "Existing controller constructors retain legacy layout");
    }

    private static void Check(bool value, string name)
    {
        if (!value) throw new Exception("FAIL: " + name);
        checks++;
    }
}
