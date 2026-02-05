using Meta.XR.MRUtilityKit;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.Splines;
public class TableInitialPlacement : MonoBehaviour
{
    [Header("Elementos para interacciones")]
    public GameObject shipSpawn;          
    public GameObject planetEnd;
    public TextMeshProUGUI debugText; // para debug

    // Variables para funcionamiento interno
    float yLift = 0.04f;
    SplineContainer splineContainer;

    MRUKRoom room;
    MRUKAnchor tableAnchor = null;
    Vector3 bottomCenter;
    Vector3 tableTopCenter;

    private void Start()
    {
        if (splineContainer == null)
            splineContainer = GetComponent<SplineContainer>();

        StartCoroutine(WaitForRoom());
    }

    IEnumerator WaitForRoom()
    {
        // Validar que exista una instancia del cuarto y se carge
        while (MRUK.Instance == null) yield return null;
        while (MRUK.Instance.GetCurrentRoom() == null) yield return null;

        // Almacenar el cuarto actual y obtener sus Anchors
        room = MRUK.Instance.GetCurrentRoom();
        Log("Room detected");

        while (room.Anchors == null || room.Anchors.Count == 0) yield return null;
        Log($"Anchors encontrados: {room.Anchors.Count}");

        // Colocar la trayectoria sobre la mesa
        PlaceTrajectoryOnTable();

        // Colocar la nave en el inicio y el planeta en el final
        PlaceShipAndPlanetFromKnots(tableTopCenter);
        
    }

    void PlaceTrajectoryOnTable()
    {
        // Buscar si existe el Anchor TABLE
        foreach (var anchor in room.Anchors)
        {
            Log($"Anchor: {anchor.Label}");
            if (anchor.Label == MRUKAnchor.SceneLabels.TABLE)
                tableAnchor = anchor;
        }

        if (tableAnchor == null)
        {
            Log("No se encontró un anchor TABLE.");
            return;
        }

        // Obtener el punto superior de la mesa
        if (!GetTableTopCenter(tableAnchor, out tableTopCenter))
        {
            Log("No pude obtener bounds de TABLE.");
            return;
        }

        Bounds rend = gameObject.GetComponentInChildren<Renderer>().bounds;

        // Centro inferior del volumen
        bottomCenter = new Vector3(
            rend.center.x,
            rend.min.y,
            rend.center.z
        );

        // BottomCenter quede en la mesa (tableTopCenter)
        Vector3 delta = (tableTopCenter + Vector3.up * yLift) - bottomCenter;

        // Mover la trayectoria ese delta
        gameObject.transform.position += delta;
        return;
    }

    bool GetTableTopCenter(MRUKAnchor tableAnchor, out Vector3 topCenter)
    {
        Collider col = tableAnchor.GetComponentInChildren<Collider>();
        if (col != null)
        {
            Bounds b = col.bounds;
            topCenter = b.center + Vector3.up * b.extents.y;
            return true;
        }
        topCenter = tableAnchor.transform.position;
        return true;
    }

    void PlaceShipAndPlanetFromKnots(Vector3 tableTopCenter)
    {
        if (splineContainer == null)
        {
            Log("SplineContainer null");
            return;
        }

        var spline = splineContainer.Spline;
        int knotCount = spline.Count;

        if (knotCount < 2)
        {
            Log("El spline necesita al menos 2 knots");
            return;
        }

        Vector3 firstKnotWorld = splineContainer.transform.TransformPoint(spline[0].Position);
        Vector3 lastKnotWorld = splineContainer.transform.TransformPoint(spline[knotCount - 1].Position);

        // Proyectar ambos puntos al plano superior de la mesa
        firstKnotWorld.y = tableTopCenter.y + yLift;
        lastKnotWorld.y = tableTopCenter.y + yLift;

        // Ajustes de offsets finos
        lastKnotWorld += new Vector3(0f, -0.01f, 0.005f);

        if (shipSpawn != null)
        {
            shipSpawn.transform.position = firstKnotWorld;
        }
        else Log("shipSpawn no asignado.");

        if (planetEnd != null)
        {
            planetEnd.transform.position = lastKnotWorld;
        }
        else Log("planetEnd no asignado.");
    }


    void Log(string msg)
    {
        Debug.Log(msg);
        if (debugText != null) debugText.text += "\n" + msg;
    }
}
