using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.Splines;
using UnityEngine.XR.OpenXR.Input;

public class FollowHand : MonoBehaviour // Crea y actualiza colliders en las puntas de los dedos para hand tracking
{
    // Asignar desde el Inspector
    public OVRSkeleton ovrSkeletonL;
    public OVRSkeleton ovrSkeletonR;
    public SplineContainer trajectorySpline; // Scrpt de la trayectoria para calcular 
    public ShipMovement shipMovementL; // Script para obtener flag de que ya tomo la nave (comenzó el juego)
    public ShipMovement shipMovementR;

    // Asignar un TMPro para ver mensajes en MR
    public TextMeshProUGUI debugText;

    // Parametros importantes que definir
    public float colliderRadius = 0.007f;
    string fingerLayer = "Default";
    bool isLeftHand;
    int fingerLayerID;
    bool trackingReady = false;
    bool showids = true;

    // Colliders para los dedos
    GameObject[] rightHandColliders = new GameObject[5];
    GameObject[] leftHandColliders = new GameObject[5];

    // Roots por mano (para jerarquía)
    GameObject rightRoot;
    GameObject leftRoot;

    // Definir los BoneIds de las puntas de cada dedo
    private OVRSkeleton.BoneId[] fingerBoneIds = new OVRSkeleton.BoneId[]
    {
        OVRSkeleton.BoneId.XRHand_ThumbTip,  // Pulgar
        OVRSkeleton.BoneId.XRHand_IndexTip,  // Índice
        OVRSkeleton.BoneId.XRHand_MiddleTip, // Medio
        OVRSkeleton.BoneId.XRHand_RingTip,   // Anular
        OVRSkeleton.BoneId.Hand_PinkyTip   // Meñique
    };

    void Start()
    {
        // Obtener layer ID
        fingerLayerID = LayerMask.NameToLayer(fingerLayer);
        if (fingerLayerID == -1) fingerLayerID = 0;

        // Crear los objetos parent por mano que contendran los colliders
        rightRoot = new GameObject("RightHandHapticColliders");
        leftRoot = new GameObject("LeftHandHapticColliders");

        // Crear colliders para mano izquierda
        isLeftHand = true;
        leftHandColliders = CreateFingerColliders();
        for (int i = 0; i < 5; i++)
            leftHandColliders[i].transform.SetParent(leftRoot.transform, worldPositionStays: false);

        // Crear colliders para mano derecha
        isLeftHand = false;
        rightHandColliders = CreateFingerColliders();
        for (int i = 0; i < 5; i++)
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
            debugText.text += "\nTracking perdido, esperando de nuevo...";
            StartCoroutine(WaitForSkeletons());
            return;
        }

        // Visualizar UNA vez los skeletos que usa
        if (showids)
        {
            for (int i = 0; i < 5; i++)
            {
                var boneL = FindBoneTransform(ovrSkeletonL, fingerBoneIds[i], showids);
                var boneR = FindBoneTransform(ovrSkeletonR, fingerBoneIds[i], showids);
            }
            showids = false;
        }

        // Actualizar posición de cada collider
        for (int i = 0; i < 5; i++)
        {
            var boneL = FindBoneTransform(ovrSkeletonL, fingerBoneIds[i], showids);
            var boneR = FindBoneTransform(ovrSkeletonR, fingerBoneIds[i], showids);

            if (boneL != null && boneL != null && leftHandColliders[i] != null)
            {
                leftHandColliders[i].transform.position = boneL.position;
                leftHandColliders[i].transform.rotation = boneL.rotation;
            }

            if (boneR != null && boneR != null && rightHandColliders[i] != null)
            {
                rightHandColliders[i].transform.position = boneR.position;
                rightHandColliders[i].transform.rotation = boneR.rotation;
            }
        }
    }

    private GameObject[] CreateFingerColliders()
    {
        string handPrefix = isLeftHand ? "Left" : "Right";
        GameObject[] colliderObjects = new GameObject[5];

        for (int i = 0; i < 5; i++)
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

            // Añadir los scripts para retroalimentacion haptica
            var vibrationHandler = colliderObjects[i].AddComponent<FingerVibrationHandler>();
            vibrationHandler.fingerIndex = i;
            vibrationHandler.isLeftHand = isLeftHand;
            vibrationHandler.debugText = debugText;

            var fingerHapticFeedback = colliderObjects[i].AddComponent<FingerHapticFeedback>();
            fingerHapticFeedback.debugText = debugText;
            fingerHapticFeedback.isLeftHand = isLeftHand;
            fingerHapticFeedback.fingerIndex = i;
            fingerHapticFeedback.trajectorySpline = trajectorySpline;
            fingerHapticFeedback.shipMovementR = shipMovementR;
            fingerHapticFeedback.shipMovementL = shipMovementL;

            if (debugText != null) debugText.text += "\nCreado " + colliderObjects[i].name;
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

    private Transform FindBoneTransform(OVRSkeleton skel, OVRSkeleton.BoneId id, bool showids)
    {
        if (skel == null || skel.Bones == null) return null;

        for (int i = 0; i < skel.Bones.Count; i++)
        {
            var b = skel.Bones[i];
            if (b != null && b.Id == id)
            {
                if (showids) debugText.text += $"\nBuscando: {id} | Encontrado: {b.Id}";
                return b.Transform;

            }
        }
        return null;
    }

}
