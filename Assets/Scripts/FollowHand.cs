using Meta.XR;
using UnityEngine;

// Crea y actualiza colliders en las puntas de los dedos para hand tracking
public class FollowHand : MonoBehaviour
{
    // Asignar el objeto OVRSkeleton desde el Inspector
    public OVRSkeleton ovrSkeleton;
    // Asignar un TMPro para ver mensajes en MR
    public TMPro.TextMeshProUGUI debugText;

    // Parametros importantes que definir
    float colliderRadius = 0.015f; 
    string fingerLayer = "Default";
    bool isLeftHand;
    int fingerLayerID;

    // Colliders para los dedos
    private GameObject[] colliderObjects = new GameObject[5];
    private SphereCollider[] colliders = new SphereCollider[5];
    private Rigidbody[] rigidbodies = new Rigidbody[5];

    // BoneIds de las puntas de cada dedo
    private OVRSkeleton.BoneId[] fingerBoneIds = new OVRSkeleton.BoneId[]
    {
        OVRSkeleton.BoneId.Hand_ThumbTip, // Pulgar
        OVRSkeleton.BoneId.Hand_IndexTip, // Índice
        OVRSkeleton.BoneId.Hand_MiddleTip, // Medio
        OVRSkeleton.BoneId.Hand_RingTip, // Anular
        OVRSkeleton.BoneId.Hand_PinkyTip // Meñique
    };


    void Start()
    {
        // Determinar si es mano izquierda o derecha
        isLeftHand = ovrSkeleton.name.ToLower().Contains("left");

        // Obtener layer ID
        fingerLayerID = LayerMask.NameToLayer(fingerLayer);
        if (fingerLayerID == -1)
        {
            debugText.text = "Layer " + fingerLayer + " no existe, usando Default";
            fingerLayerID = 0;
        }

        CreateFingerColliders();
        debugText.text += "\nColliders creados para mano " + (isLeftHand ? "IZQUIERDA" : "DERECHA");
    }

    void CreateFingerColliders()
    {
        string handPrefix = isLeftHand ? "Left" : "Right";

        for (int i = 0; i < 5; i++)
        {
            // Crear GameObject para el collider
            colliderObjects[i] = new GameObject($"{handPrefix}FingerCollider_{i}");
            colliderObjects[i].layer = fingerLayerID;

            // Crear SphereCollider
            colliders[i] = colliderObjects[i].AddComponent<SphereCollider>();
            colliders[i].radius = colliderRadius;
            colliders[i].isTrigger = false; 

            // Crear Rigidbody
            rigidbodies[i] = colliderObjects[i].AddComponent<Rigidbody>();

            // Configuración  del Rigidbody
            rigidbodies[i].isKinematic = true; 
            rigidbodies[i].useGravity = false;
            rigidbodies[i].collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            rigidbodies[i].interpolation = RigidbodyInterpolation.Interpolate;

            // Añadir el handler de vibración
            var vibrationHandler = colliderObjects[i].AddComponent<FingerVibrationHandler>();
            vibrationHandler.fingerIndex = i;
            vibrationHandler.isLeftHand = isLeftHand;
            vibrationHandler.debugText = debugText;

            debugText.text += "\nCreado " + colliderObjects[i].name;
        }
    }

    void Update()
    {
        // Verificar que este asignado el OVR Skeleton
        if (ovrSkeleton == null || !ovrSkeleton.IsDataValid)
            return;

        // Actualizar posición de cada collider
        for (int i = 0; i < 5; i++)
        {
            var bone = ovrSkeleton.Bones[(int)fingerBoneIds[i]];

            if (bone.Transform != null && colliderObjects[i] != null)
            {
                colliderObjects[i].transform.position = bone.Transform.position;
                colliderObjects[i].transform.rotation = bone.Transform.rotation;
            }
        }
    }
}