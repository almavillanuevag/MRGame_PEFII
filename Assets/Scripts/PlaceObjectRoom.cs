using System.Collections;
using UnityEngine;
using Meta.XR.MRUtilityKit;

public class PlaceObjectRoom : MonoBehaviour
{
    [Header("Anchor selection")]
    [Tooltip("Scene label del anchor donde quieres colocar el objeto (ej. TABLE).")]
    public MRUKAnchor.SceneLabels targetLabel = MRUKAnchor.SceneLabels.TABLE;

    [Tooltip("Si existen varios anchors con ese label, se elegirá el más cercano a este punto (si se asigna).")]
    public Transform referencePoint;

    [Header("Placement")]
    public AnchorPlacement placement = AnchorPlacement.OnTop;          // Sobre / dentro / debajo del anchor
    public ObjectAlign objectAlign = ObjectAlign.BottomToTarget;       // Qué punto del objeto se alinea al target

    [Tooltip("Separación extra (en metros) sobre/abajo del punto destino. Útil para evitar z-fighting o interpenetración.")]
    public float yOffset = 0.02f;

    [Tooltip("Offset adicional en el espacio del anchor (X,Z útiles para mover sobre la mesa, por ejemplo).")]
    public Vector3 anchorLocalOffset = Vector3.zero;

    [Header("Options")]
    [Tooltip("Si no hay Collider en el anchor, usa su transform.position como fallback.")]
    public bool fallbackToTransformPosition = true;

    [Tooltip("Reintenta si no se encuentra el anchor inmediatamente.")]
    public int maxRetries = 120; // ~2s a 60fps
    public float retryInterval = 0.05f;

    private MRUKRoom room;

    public enum AnchorPlacement
    {
        OnTop,        // Punto superior del bounds del anchor
        Center,       // Centro del bounds del anchor
        BelowBottom   // Punto inferior del bounds del anchor
    }

    public enum ObjectAlign
    {
        BottomToTarget, // El bottom del objeto cae en el target (para "sobre mesa")
        CenterToTarget, // El centro del objeto cae en el target (para "dentro/centro")
        TopToTarget     // El top del objeto cae en el target (útil si quieres colgarlo debajo, etc.)
    }

    private void Start()
    {
        StartCoroutine(WaitForRoomAndPlace());
    }

    private IEnumerator WaitForRoomAndPlace()
    {
        // Esperar MRUK
        while (MRUK.Instance == null) yield return null;

        // Esperar cuarto
        while (MRUK.Instance.GetCurrentRoom() == null) yield return null;
        room = MRUK.Instance.GetCurrentRoom();

        // Esperar anchors
        while (room.Anchors == null || room.Anchors.Count == 0) yield return null;

        // Intentar encontrar anchor (con reintentos por si el label aparece después)
        MRUKAnchor chosen = null;
        for (int i = 0; i < maxRetries; i++)
        {
            chosen = FindBestAnchor(room, targetLabel, referencePoint);
            if (chosen != null) break;
            yield return new WaitForSeconds(retryInterval);
        }

        if (chosen == null)
        {
            Debug.LogWarning($"[PlaceObjectRoom] No encontré un anchor con label {targetLabel}. Objeto: {name}");
            yield break;
        }

        PlaceRelativeToAnchor(chosen);
    }

    private MRUKAnchor FindBestAnchor(MRUKRoom r, MRUKAnchor.SceneLabels label, Transform refPoint)
    {
        MRUKAnchor best = null;
        float bestDist = float.PositiveInfinity;

        foreach (var a in r.Anchors)
        {
            if (a == null) continue;
            if (a.Label != label) continue;

            if (refPoint == null)
            {
                // Si no hay referencePoint, regresa el primero
                return a;
            }

            float d = Vector3.Distance(refPoint.position, a.transform.position);
            if (d < bestDist)
            {
                bestDist = d;
                best = a;
            }
        }
        return best;
    }

    private void PlaceRelativeToAnchor(MRUKAnchor anchor)
    {
        // Obtener bounds del anchor (Collider preferido)
        if (!TryGetAnchorBounds(anchor, out Bounds anchorBounds))
        {
            if (!fallbackToTransformPosition)
            {
                Debug.LogWarning($"[PlaceObjectRoom] No pude obtener bounds del anchor y fallback desactivado. Objeto: {name}");
                return;
            }

            // Fallback: usar posición del anchor como "centro"
            anchorBounds = new Bounds(anchor.transform.position, Vector3.zero);
        }

        // Calcular punto destino en el anchor
        Vector3 targetPointWorld = GetAnchorTargetPoint(anchor, anchorBounds, placement);

        // Aplicar offset local del anchor (p.ej. mover sobre la mesa en X/Z del anchor)
        if (anchorLocalOffset != Vector3.zero)
        {
            targetPointWorld += anchor.transform.TransformVector(anchorLocalOffset);
        }

        // Offset vertical final (signo depende de placement)
        float signedYOffset = yOffset;
        if (placement == AnchorPlacement.BelowBottom) signedYOffset = -Mathf.Abs(yOffset);
        else signedYOffset = Mathf.Abs(yOffset);

        targetPointWorld += Vector3.up * signedYOffset;

        // Obtener bounds del objeto a mover (Renderer preferido)
        if (!TryGetObjectBounds(out Bounds objBounds))
        {
            // Fallback: usa pivot como referencia
            transform.position = targetPointWorld;
            return;
        }

        // Elegir qué punto del objeto alinear al target
        Vector3 objectRefPoint = GetObjectReferencePoint(objBounds, objectAlign);

        // Calcular delta para mover
        Vector3 delta = targetPointWorld - objectRefPoint;
        transform.position += delta;
    }

    private bool TryGetAnchorBounds(MRUKAnchor anchor, out Bounds b)
    {
        // Collider del anchor (si el building block lo genera)
        Collider col = anchor.GetComponentInChildren<Collider>();
        if (col != null)
        {
            b = col.bounds;
            return true;
        }

        Renderer rend = anchor.GetComponentInChildren<Renderer>();
        if (rend != null)
        {
            b = rend.bounds;
            return true;
        }

        b = default;
        return false;
    }

    private bool TryGetObjectBounds(out Bounds b)
    {
        Renderer[] rends = GetComponentsInChildren<Renderer>();
        if (rends == null || rends.Length == 0)
        {
            b = default;
            return false;
        }

        b = rends[0].bounds;
        for (int i = 1; i < rends.Length; i++)
            b.Encapsulate(rends[i].bounds);

        return true;
    }

    private Vector3 GetAnchorTargetPoint(MRUKAnchor anchor, Bounds bounds, AnchorPlacement p)
    {
        Vector3 center = bounds.center;

        switch (p)
        {
            case AnchorPlacement.OnTop:
                return new Vector3(center.x, bounds.max.y, center.z);

            case AnchorPlacement.Center:
                return center;

            case AnchorPlacement.BelowBottom:
                return new Vector3(center.x, bounds.min.y, center.z);

            default:
                return center;
        }
    }

    private Vector3 GetObjectReferencePoint(Bounds objBounds, ObjectAlign align)
    {
        Vector3 c = objBounds.center;

        switch (align)
        {
            case ObjectAlign.BottomToTarget:
                return new Vector3(c.x, objBounds.min.y, c.z);

            case ObjectAlign.CenterToTarget:
                return c;

            case ObjectAlign.TopToTarget:
                return new Vector3(c.x, objBounds.max.y, c.z);

            default:
                return c;
        }
    }
}
