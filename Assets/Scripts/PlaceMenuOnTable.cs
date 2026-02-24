using System.Collections;
using UnityEngine;
using Meta.XR.MRUtilityKit;



public class PlaceMenuOnTable : MonoBehaviour
{   // Coloca un Canvas World Space en el borde de la mesa más cercana al usuario.
    [Header("Placement")]
    public Transform _cameraTransform;
    [Tooltip("Offset en X relativo al usuario. 0 = mismo X que el usuario, positivo = derecha, negativo = izquierda.")]
    public float xOffset = -0.2f;

    [Tooltip("Offset vertical sobre el borde de la mesa (metros). Útil para que el canvas no quede enterrado.")]
    public float yOffset = 0.1f;

    [Tooltip("Offset en Z desde el borde de la mesa hacia el usuario (metros). Positivo = ligeramente hacia el jugador.")]
    public float zEdgeOffset = 0f;

    bool continuousLookAt = true; // el canvas rota continuamente para mirar al usuario cada frame.
    bool lockPitch = true; //Solo rota en el eje Y

    // Retry
    int maxRetries = 120;
    float retryInterval = 0.05f;

    // Referencia a la cámara principal (usuario)
    bool _placed = false;

    private void Start()
    {
        if (_cameraTransform == null)
        {
            Debug.LogError("[PlaceMenuOnTableEdgeFacingUser] Asigna CenterEye Cámara");
        }

        StartCoroutine(WaitForRoomAndPlace());
    }

    private void LateUpdate()
    {
        // Rotación continua para que el canvas siempre mire al usuario
        if (_placed && continuousLookAt && _cameraTransform != null)
        {
            FaceUser();
        }
    }

    private IEnumerator WaitForRoomAndPlace()
    {
        while (MRUK.Instance == null) yield return null;
        while (MRUK.Instance.GetCurrentRoom() == null) yield return null;

        MRUKRoom room = MRUK.Instance.GetCurrentRoom();

        while (room.Anchors == null || room.Anchors.Count == 0) yield return null;

        MRUKAnchor tableAnchor = null;
        for (int i = 0; i < maxRetries; i++)
        {
            tableAnchor = FindNearestTableAnchor(room);
            if (tableAnchor != null) break;
            yield return new WaitForSeconds(retryInterval);
        }

        if (tableAnchor == null)
        {
            Debug.LogWarning("No se encontró ningún anchor TABLE en la habitación.");
            yield break;
        }

        PlaceOnTableEdge(tableAnchor);
    }

    private MRUKAnchor FindNearestTableAnchor(MRUKRoom room)
    {
        if (_cameraTransform == null) return null;

        MRUKAnchor best = null;
        float bestScore = float.PositiveInfinity;

        foreach (var anchor in room.Anchors)
        {
            if (anchor == null) continue;
            if (anchor.Label != MRUKAnchor.SceneLabels.TABLE) continue;

            // Score = distancia en Z + distancia en Y (ignora X)
            Vector3 diff = anchor.transform.position - _cameraTransform.position;
            float score = Mathf.Abs(diff.z) + Mathf.Abs(diff.y);

            if (score < bestScore)
            {
                bestScore = score;
                best = anchor;
            }
        }

        return best;
    }

    private void PlaceOnTableEdge(MRUKAnchor anchor)
    {
        // Obtener bounds de la mesa
        if (!TryGetAnchorBounds(anchor, out Bounds tableBounds))
        {
            tableBounds = new Bounds(anchor.transform.position, Vector3.zero);
            Debug.LogWarning("No se obtuvieron bounds de la mesa. Usando posición del anchor.");
        }

        // Y: superficie superior de la mesa 
        float targetY = tableBounds.max.y + yOffset;

        // Z: borde de la mesa más cercano al usuario
        float userZ = _cameraTransform.position.z;
        float edgeZMin = tableBounds.min.z;
        float edgeZMax = tableBounds.max.z;

        float nearestEdgeZ = (Mathf.Abs(userZ - edgeZMin) < Mathf.Abs(userZ - edgeZMax))
            ? edgeZMin
            : edgeZMax;

        // Desplazar ligeramente hacia el usuario para evitar z fighting con la mesa
        float directionToUser = Mathf.Sign(userZ - nearestEdgeZ);
        float targetZ = nearestEdgeZ + directionToUser * zEdgeOffset;

        // X: posición X del usuario + offset configurable 
        float targetX = _cameraTransform.position.x + xOffset;

        transform.position = new Vector3(targetX, targetY, targetZ);

        // Rotación inicial hacia el usuario
        FaceUser();

        _placed = true;

        Debug.Log($"Canvas colocado en {transform.position} (mesa: {anchor.name}).");
    }
    private void FaceUser()
    {
        if (_cameraTransform == null) return;

        Vector3 direction = _cameraTransform.position - transform.position;

        if (lockPitch)
        {
            // Eliminar componente vertical canvas permanece perfectamente vertical
            direction.y = 0f;
        }

        if (direction.sqrMagnitude < 0.0001f) return;

        // Negamos la dirección: queremos que la cara frontal del canvas apunte AL usuario
        Quaternion look = Quaternion.LookRotation(-direction, Vector3.up);
        Quaternion pitch = Quaternion.Euler(30f, 0f, 0f);

        transform.rotation = look * pitch;
    }
    private bool TryGetAnchorBounds(MRUKAnchor anchor, out Bounds b)
    {
        Collider col = anchor.GetComponentInChildren<Collider>();
        if (col != null) { b = col.bounds; return true; }

        Renderer rend = anchor.GetComponentInChildren<Renderer>();
        if (rend != null) { b = rend.bounds; return true; }

        b = default;
        return false;
    }
}