using UnityEngine;

namespace ShipbreakerVr;

public class AttachToTarget : MonoBehaviour
{
    public Transform Target;
    public float ForwardOffset;

    private void Update()
    {
        if (!Target) return;
        transform.position = Target.position + Target.forward * ForwardOffset;
        transform.rotation = Target.rotation;
    }
}