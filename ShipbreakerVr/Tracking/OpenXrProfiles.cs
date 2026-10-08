using UnityEngine;
using UnityEngine.XR.OpenXR;
using UnityEngine.XR.OpenXR.Features.Interactions;

namespace ShipbreakerVr.Tracking;

public static class OpenXrProfiles
{
    public static void EnableStandardControllers()
    {
        try { FrameControllerProfile.Install(); }
        catch (System.Exception error) { Debug.LogWarning("[ShipbreakerVr] Native Frame profile could not be added; existing profiles retained. " + error); }
        // The checked-in bundle already contains these features, but enables only Touch.
        // Change the loaded settings before Initialize(), when OpenXR binds its action sets.
        foreach (var feature in OpenXRSettings.Instance.GetFeatures())
        {
            if (feature is KHRSimpleControllerProfile || feature is OculusTouchControllerProfile ||
                feature is ValveIndexControllerProfile || feature is HTCViveControllerProfile ||
                feature is MicrosoftMotionControllerProfile)
            {
                feature.enabled = true;
                Debug.Log($"[ShipbreakerVr] Controller profile: {feature.GetType().Name}");
            }
        }
    }
}
