using System.Collections;
using UnityEngine;

public class UIHand : MonoBehaviour
{
    // variables internas
    OVRSkeleton ovrSkeleton;
    bool trackingReady = false;
    bool isHolding = false;
    readonly OVRSkeleton.BoneId boneId = OVRSkeleton.BoneId.XRHand_Palm;
    Transform palmTransform;

    Vector3 offset = new Vector3(0, 0.025f, 0);

    void Start()
    {
        // Esperar a que se inicialicen los Skeletons
        StartCoroutine(WaitForSkeletons());

    }

    void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("HandCollider")) return;
        if (isHolding) return;

        // Encontrar skeleton de esa mano
        OVRSkeleton skeleton = other.GetComponentInChildren<OVRSkeleton>();
        if (skeleton == null)
        {
            Debug.LogWarning("HandCollider detectado, pero no se encontró OVRSkeleton en hijos.");
            return;
        }

        ovrSkeleton = skeleton;

        // Encontrar palm una vez 
        palmTransform = FindPalmTransform();
        if (palmTransform == null)
        {
            // Si aún no está listo, lo resolvemos en Update/Coroutine
            trackingReady = false;
            StartCoroutine(WaitForSkeletons());
        }
        else trackingReady = true;
        isHolding = true;
    }


    void Update()
    {
        if (!isHolding) return;
        if (!trackingReady) return;

        if (ovrSkeleton == null || !ovrSkeleton.IsDataValid)
        {
            trackingReady = false;
            palmTransform = null;
            StartCoroutine(WaitForSkeletons());
            return;
        }

        if (palmTransform == null)
        {
            palmTransform = FindPalmTransform();
            if (palmTransform == null) return;
        }

        // Actualizar posicion del canvas
        transform.position = palmTransform.position + offset;
        transform.rotation = palmTransform.rotation;
    }


    IEnumerator WaitForSkeletons()
    {
        // Esperar skeleton + datos válidos
        while (ovrSkeleton == null || !ovrSkeleton.IsDataValid)
            yield return null;

        // Esperar bones
        while (ovrSkeleton.Bones == null || ovrSkeleton.Bones.Count == 0)
        {
            yield return null;
        }

        // Encontrar palm bone
        palmTransform = FindPalmTransform();
        if (palmTransform == null)
        {
            // reintentar obtener palm transform 
            yield return new WaitForSeconds(0.1f);
            StartCoroutine(WaitForSkeletons());
            yield break;
        }

        trackingReady = true;
    }

    Transform FindPalmTransform()
    {
        if (ovrSkeleton == null || ovrSkeleton.Bones == null) return null;

        foreach (var b in ovrSkeleton.Bones)
        {
            if (b != null && b.Id == boneId)
                return b.Transform;
        }
        return null;
    }
}
