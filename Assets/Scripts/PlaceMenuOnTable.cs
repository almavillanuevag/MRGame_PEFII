using System.Collections;
using UnityEngine;
using Meta.XR.MRUtilityKit;

public class PlaceMenuOnTable : MonoBehaviour
{
    [Header("Referencias")]
    [Tooltip("Normalmente CenterEyeAnchor o la cámara del rig (terapeuta).")]
    public Transform therapist;

    MRUK mruk;

    [Header("Posicionamiento")]
    [Tooltip("Distancia fija en X con respecto al terapeuta. Positivo = a la derecha del terapeuta, negativo = izquierda.")]
    public float xOffsetFromTherapist = 0.35f;

    float yLiftAboveTable = 0.01f; //Altura adicional sobre la superficie de la mesa (m).
    float zOutFromTableEdge = 0f; // Separación hacia afuera desde el borde de la mesa (m
    bool keepFacingTherapist = true; // El menu siga al terapeuta
    float rotationLerpSpeed = 10f; // Velocidad de suavizado para rotación.
    bool chooseClosestTable = true; // Elegir la mesa más cercana al terapeuta

    private MRUKAnchor tableAnchor;

    private IEnumerator Start()
    {
        if (therapist == null)
        {
            Debug.LogError("[PlaceMenuOnTableEdgeFacingUser] Asigna 'therapist' (CenterEye/Cámara).");
            yield break;
        }

        if (mruk == null) mruk = MRUK.Instance;

        // Esperar a que MRUK tenga room y anchors listos
        yield return new WaitUntil(() => mruk != null && mruk.Rooms != null && mruk.Rooms.Count > 0);

        // Buscar mesa
        tableAnchor = FindTableAnchor(mruk, therapist.position, chooseClosestTable);
        if (tableAnchor == null)
        {
            Debug.LogWarning("[PlaceMenuOnTableEdgeFacingUser] No encontré mesa (anchor).");
            yield break;
        }

        PlaceOnce();
    }

    private void Update()
    {
        if (!keepFacingTherapist) return;
        if (therapist == null) return;

        FaceTherapistYawOnly();
    }

    private void PlaceOnce()
    {
        // 1) Tomar bounds de la mesa (aprox) en mundo
        // MRUKAnchor tiene un volumen/plane; usaremos su transform y su bounds (cuando existe).
        // Si no hay bounds confiables, igual podemos usar el forward del anchor como "frente".
        Vector3 tableUp = tableAnchor.transform.up;
        Vector3 tableForward = tableAnchor.transform.forward;
        Vector3 tableRight = tableAnchor.transform.right;

        // Centro aproximado del anchor
        Vector3 tableCenter = tableAnchor.transform.position;

        // 2) Determinar "borde frontal" de la mesa: nos movemos en forward hasta el borde usando el tamaño si está disponible
        // Intento: usar PlaneRect/Bounds si existen; fallback a un offset fijo.
        float halfDepth = 0.35f; // fallback razonable si no hay tamaño
        float halfWidth = 0.45f;

        // Si MRUK tiene rect/bounds: (depende de versión; algunas exponen PlaneRect)
        // Como no es estable entre versiones, lo dejamos robusto:
        var rend = tableAnchor.GetComponentInChildren<Renderer>();
        if (rend != null)
        {
            Bounds b = rend.bounds;
            // Proyectar a ejes de la mesa: aproximación usando magnitudes en ejes mundo
            // Para mesa típica alineada, funciona muy bien.
            halfDepth = b.extents.z;
            halfWidth = b.extents.x;
            tableCenter = b.center;
        }

        // 3) Calcula X basado en terapeuta: posición deseada = terapeuta + (right * offset)
        Vector3 desiredX = therapist.position + therapist.right * xOffsetFromTherapist;

        // 4) Construye posición final:
        // - X viene de desiredX proyectado sobre el eje right de la mesa (para que caiga “sobre la mesa”)
        // - Z al borde frontal de la mesa + zOutFromTableEdge (afuera del borde)
        // - Y a la altura de la superficie + yLift
        //
        // Nota: esto asume que la mesa es más o menos horizontal.
        Vector3 edgeFront = tableCenter + tableForward * (halfDepth + zOutFromTableEdge);

        // Proyectar desiredX sobre el eje right de la mesa: tomar el componente right respecto al centro
        Vector3 fromCenterToDesired = desiredX - tableCenter;
        float xOnTable = Vector3.Dot(fromCenterToDesired, tableRight);
        xOnTable = Mathf.Clamp(xOnTable, -halfWidth, halfWidth);

        Vector3 finalPos = edgeFront + tableRight * xOnTable;

        // Ajuste de altura
        finalPos += tableUp * yLiftAboveTable;

        transform.position = finalPos;

        // Rotación inicial mirando al terapeuta
        FaceTherapistYawOnly();
    }

    private void FaceTherapistYawOnly()
    {
        Vector3 toUser = therapist.position - transform.position;
        toUser.y = 0f; // solo yaw
        if (toUser.sqrMagnitude < 0.0001f) return;

        Quaternion targetRot = Quaternion.LookRotation(toUser.normalized, Vector3.up);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, Time.deltaTime * rotationLerpSpeed);
    }

    private MRUKAnchor FindTableAnchor(MRUK mrukInstance, Vector3 referencePos, bool closest)
    {
        MRUKAnchor best = null;
        float bestDist = float.MaxValue;

        foreach (var room in mrukInstance.Rooms)
        {
            if (room == null || room.Anchors == null) continue;

            foreach (var a in room.Anchors)
            {
                if (a == null) continue;

                // Heurística: mesas suelen venir como anchors "TABLE" o como "SURFACE" con clasificación.
                // Según versión, a.Label puede variar. Aquí lo hacemos flexible:
                string name = a.name.ToLowerInvariant();
                bool looksLikeTable = name.Contains("table");

                // Si tu MRUK usa etiquetas, puedes endurecer esto:
                // looksLikeTable |= a.AnchorLabels.Contains(MRUKAnchor.SceneLabel.TABLE); (depende de versión)

                if (!looksLikeTable) continue;

                if (!closest) return a;

                float d = (a.transform.position - referencePos).sqrMagnitude;
                if (d < bestDist)
                {
                    bestDist = d;
                    best = a;
                }
            }
        }

        return best;
    }
}