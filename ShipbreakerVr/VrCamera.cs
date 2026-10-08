using BBI.Unity.Game;
using BepInEx.Configuration;
using UnityEngine;
using UnityEngine.SpatialTracking;

namespace ShipbreakerVr;

// Run after Cinemachine. Keep the rig outside game-owned camera hierarchies.
[DefaultExecutionOrder(9000)]
public class VrCamera : MonoBehaviour
{
    private static VrCamera instance;
    private static Camera gameplayCamera;
    private static Hab3DController habitat;
    private Camera vrCamera;
    private Camera suppressedCamera;
    private bool previousEnabled;
    private float nextCanvasScan;
    private int sourceMask = ~0;
    private string lastViewStatus;
    public static Camera MainCamera { get; private set; }
    public static Camera ViewCamera { get; private set; }
    public static Transform BodyTransform => instance ? instance.transform : null;
    private static ConfigEntry<float> eyeForwardOffset;
    public static Vector3 EyeOffset => Vector3.forward * (eyeForwardOffset?.Value ?? 0f);
    internal static void Configure(ConfigFile config) => eyeForwardOffset = config.Bind("VR", "EyeForwardOffsetMetres", 0f,
        new ConfigDescription("Eye position ahead of native camera; original mod used 0.2. Does not scale the world or controller tracking.", new AcceptableValueRange<float>(-.1f, .3f)));

    public static void Create(Camera camera)
    {
        gameplayCamera = camera;
        EnsureRig();
    }

    public static void RegisterHabitat(Hab3DController controller)
    {
        habitat = controller;
        EnsureRig();
    }

    private static void EnsureRig()
    {
        if (instance) return;
        var rig = new GameObject("ShipbreakerVr camera rig");
        DontDestroyOnLoad(rig);
        instance = rig.AddComponent<VrCamera>();
        var view = new GameObject("VrCamera");
        view.transform.SetParent(rig.transform, false);
        view.transform.localPosition = EyeOffset;
        instance.vrCamera = view.AddComponent<Camera>();
        instance.vrCamera.nearClipPlane = .03f;
        ViewCamera = instance.vrCamera;
        ViewCamera.enabled = false;
        ViewCamera.tag = "MainCamera";
        var driver = view.AddComponent<TrackedPoseDriver>();
        driver.trackingType = TrackedPoseDriver.TrackingType.RotationOnly;
        driver.SetPoseSource(TrackedPoseDriver.DeviceType.GenericXRDevice, TrackedPoseDriver.TrackedPose.Center);
    }

    internal static void EnsureEarlyRig()
    {
        if (instance) return;
        // Main-menu/initial loading must not wait for the habitat or player to spawn.
        var source = Camera.main;
        EnsureRig();
        if (source && source != ViewCamera)
        {
            gameplayCamera = source;
            instance.transform.SetPositionAndRotation(source.transform.position, source.transform.rotation);
        }
        Debug.Log("[ShipbreakerVr] Early XR view ready before gameplay/habitat camera creation.");
    }

    // Restore before the game's state handler writes its next camera state.
    public static void BeforeGameCameraStateChange()
    {
        if (instance) instance.ReleaseSource();
    }

    private void LateUpdate()
    {
        vrCamera.transform.localPosition = EyeOffset;
        var inHabitat = GameSession.CurrentGameState == GameSession.GameState.Hab ||
            (GameSession.CurrentGameState == GameSession.GameState.Paused && GameSession.PrevGameState == GameSession.GameState.Hab);
        var brain = habitat ? habitat.CinemachineBrain : null;
        var source = inHabitat && brain ? brain.OutputCamera : gameplayCamera;
        if (MainCamera != source)
        {
            ReleaseSource();
            MainCamera = source;
        }
        var status = $"source={(source ? source.name : "holding last pose during camera gap")}; state={GameSession.CurrentGameState}; habitat={inHabitat}";
        if (status != lastViewStatus)
        {
            lastViewStatus = status;
            Debug.Log($"[ShipbreakerVr] View: {status}");
        }
        if (!ModXrManager.IsVrEnabled)
        {
            ReleaseSource();
            vrCamera.enabled = false;
            return;
        }
        if (source && suppressedCamera != source)
        {
            ReleaseSource();
            suppressedCamera = source;
            previousEnabled = source.enabled;
        }
        if (source)
        {
            source.enabled = false;
            transform.SetPositionAndRotation(source.transform.position, source.transform.rotation);
            vrCamera.nearClipPlane = Mathf.Min(.03f, source.nearClipPlane);
            vrCamera.farClipPlane = source.farClipPlane;
            sourceMask = source.cullingMask;
            vrCamera.clearFlags = source.clearFlags;
            vrCamera.backgroundColor = source.backgroundColor;
        }
        // Loading can destroy the previous game camera before the next one exists.
        // Keep head tracking and the UI view alive at the last body pose across that gap.
        vrCamera.cullingMask = sourceMask | VrUi.VisibleLayerMask | VrHelmetDamage.VisibleLayerMask;
        vrCamera.enabled = true;
        if (Time.unscaledTime >= nextCanvasScan)
        {
            nextCanvasScan = Time.unscaledTime + 1f;
            foreach (var canvas in FindObjectsOfType<Canvas>()) VrUi.Attach(canvas);
        }
    }

    private void ReleaseSource()
    {
        if (suppressedCamera) suppressedCamera.enabled = previousEnabled;
        suppressedCamera = null;
    }

    private void OnDisable()
    {
        ReleaseSource();
        if (vrCamera) vrCamera.enabled = false;
    }

    private void OnDestroy()
    {
        ReleaseSource();
        if (instance != this) return;
        instance = null;
        MainCamera = null;
        ViewCamera = null;
    }
}
