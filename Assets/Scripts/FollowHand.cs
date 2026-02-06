using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.Splines;
using UnityEngine.XR.OpenXR.Input;

public class FollowHand : MonoBehaviour // Crea y actualiza colliders en las puntas de los dedos para hand tracking
{
    [Header("Asignar elementos para interacciones")]
    public OVRSkeleton ovrSkeletonL;
    public OVRSkeleton ovrSkeletonR;
    public SplineContainer trajectorySpline; // Scrpt de la trayectoria para calcular 
    public ShipMovement shipMovementL; // Script para obtener flag de que ya tomo la nave (comenzó el juego)
    public ShipMovement shipMovementR;
    public TextMeshProUGUI debugText;

    // --- Para funcionamiento interno ---

    // Parametros importantes que definir
    public float colliderRadius = 0.007f;
    string fingerLayer = "Default";
    bool isLeftHand;
    int fingerLayerID;
    bool trackingReady = false;

    // Colliders para los dedos
    GameObject[] rightHandColliders = new GameObject[6];
    GameObject[] leftHandColliders = new GameObject[6];

    // Roots por mano (para jerarquía)
    GameObject rightRoot;
    GameObject leftRoot;

    // Definir los BoneIds de las puntas de cada dedo
    private OVRSkeleton.BoneId[] fingerBoneIds = new OVRSkeleton.BoneId[]
    {
        OVRSkeleton.BoneId.XRHand_ThumbDistal,        // Pulgar
        OVRSkeleton.BoneId.XRHand_IndexIntermediate,  // Índice
        OVRSkeleton.BoneId.XRHand_MiddleIntermediate, // Medio
        OVRSkeleton.BoneId.XRHand_RingIntermediate,   // Anular
        OVRSkeleton.BoneId.XRHand_LittleIntermediate,  // Meñique
        OVRSkeleton.BoneId.XRHand_Palm                // Palma
    };

    void Start()
    {
        // Obtener layer ID
        fingerLayerID = LayerMask.NameToLayer(fingerLayer);
        if (fingerLayerID == -1) fingerLayerID = 0;

        // Crear los objetos parent por mano que contendran los colliders -> modificar (que ya existan para poder interactuar con ellos)
        rightRoot = new GameObject("RightHandHapticColliders");
        leftRoot = new GameObject("LeftHandHapticColliders");

        // Crear colliders para mano izquierda
        isLeftHand = true;
        leftHandColliders = CreateFingerColliders();
        for (int i = 0; i < 6; i++)
            leftHandColliders[i].transform.SetParent(leftRoot.transform, worldPositionStays: false);

        // Crear colliders para mano derecha
        isLeftHand = false;
        rightHandColliders = CreateFingerColliders();
        for (int i = 0; i < 6; i++)
            rightHandColliders[i].transform.SetParent(rightRoot.transform, worldPositionStays: false);

        // Esperar a que se inicialicen los Skeletons
        StartCoroutine(WaitForSkeletons());
    }

    void Update()
    {
        // Acceder a bones hasta que el tracking esté listo
        if (!trackingReady) return;

        // Por si se pierde el tracking, evitamos nulls y dejamos de actualizar
        if (ovrSkeletonL == null || !ovrSkeletonL.IsDataValid ||
            ovrSkeletonR == null || !ovrSkeletonR.IsDataValid)
        {
            // Reintentar cuando vuelva el tracking
            trackingReady = false;
            if (debugText != null) debugText.text += "\nTracking perdido, esperando de nuevo...";
            StartCoroutine(WaitForSkeletons());
            return;
        }

        // Actualizar posición de cada collider
        for (int i = 0; i < 6; i++)
        {
            var boneL = FindBoneTransform(ovrSkeletonL, fingerBoneIds[i]);
            var boneR = FindBoneTransform(ovrSkeletonR, fingerBoneIds[i]);

            if (boneL != null && leftHandColliders[i] != null)
            {
                leftHandColliders[i].transform.position = boneL.position;
                leftHandColliders[i].transform.rotation = boneL.rotation;
            }

            if (boneR != null && rightHandColliders[i] != null)
            {
                rightHandColliders[i].transform.position = boneR.position;
                rightHandColliders[i].transform.rotation = boneR.rotation;
            }
        }
    }

    private GameObject[] CreateFingerColliders()
    {
        string handPrefix = isLeftHand ? "Left" : "Right";
        GameObject[] colliderObjects = new GameObject[6];

        for (int i = 0; i < 6; i++)
        {
            // Crear GameObject para collider (visual + collider integrado por ser primitive)
            colliderObjects[i] = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            colliderObjects[i].name = $"{handPrefix}Sphere_{i}";
            colliderObjects[i].layer = fingerLayerID;

            // Visualizarlas (opcional, quitar despues)
            var renderer = colliderObjects[i].GetComponent<Renderer>();
            if (renderer != null)
            {
                Shader sh = Shader.Find("Universal Render Pipeline/Lit");
                if (sh == null) sh = Shader.Find("Standard");
                var mat = new Material(sh);
                mat.color = Color.gray;
                renderer.material = mat;

                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
            }

            // Escalar el diámetro 
            float diameter = colliderRadius * 2f;
            colliderObjects[i].transform.localScale = new Vector3(diameter, diameter, diameter);

            // Crear Rigidbody 
            Rigidbody rb = colliderObjects[i].GetComponent<Rigidbody>();
            if (rb == null) rb = colliderObjects[i].AddComponent<Rigidbody>();

            rb.isKinematic = true;
            rb.useGravity = false;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            rb.interpolation = RigidbodyInterpolation.Interpolate;

            var fingerHapticFeedback = colliderObjects[i].AddComponent<FingerHapticFeedback>();
            fingerHapticFeedback.debugText = debugText;
            fingerHapticFeedback.isLeftHand = isLeftHand;
            fingerHapticFeedback.fingerIndex = i;
            fingerHapticFeedback.trajectorySpline = trajectorySpline;
            fingerHapticFeedback.shipMovementR = shipMovementR;
            fingerHapticFeedback.shipMovementL = shipMovementL;
        }

        return colliderObjects;
    }

    private IEnumerator WaitForSkeletons()
    {
        if (debugText != null) debugText.text += "\nEsperando hand tracking...";

        // Esperar hasta que AMBOS skeletons estén listos
        while (ovrSkeletonL == null || !ovrSkeletonL.IsDataValid ||
               ovrSkeletonR == null || !ovrSkeletonR.IsDataValid)
        {
            yield return null;
        }

        // Asegurar que Bones ya exista
        while (ovrSkeletonL.Bones == null || ovrSkeletonR.Bones == null ||
               ovrSkeletonL.Bones.Count == 0 || ovrSkeletonR.Bones.Count == 0)
        {
            yield return null;
        }

        trackingReady = true;
        if (debugText != null) debugText.text += "\nHand tracking listo";
    }

    private Transform FindBoneTransform(OVRSkeleton skel, OVRSkeleton.BoneId id)
    {
        if (skel == null || skel.Bones == null) return null;

        for (int i = 0; i < skel.Bones.Count; i++)
        {
            var b = skel.Bones[i];
            if (b != null && b.Id == id)
                return b.Transform;
        }
        return null;
    }

    public void StopHapticFeedbackFunctions()
    {
        for (int i = 0; i < 6; i++)
        {
            FingerHapticFeedback HapticL = leftHandColliders[i].GetComponent<FingerHapticFeedback>();
            HapticL.StopVibration();
            Destroy(HapticL);

            FingerHapticFeedback HapticR = rightHandColliders[i].GetComponent<FingerHapticFeedback>();
            HapticR.StopVibration();
            Destroy(HapticR);
        }
    }
}
