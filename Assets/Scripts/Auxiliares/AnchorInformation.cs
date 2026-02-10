using UnityEngine;
using Meta.XR.MRUtilityKit;
using TMPro;
using System.Collections;

public class AnchorInformation : MonoBehaviour
{
    public TextMeshProUGUI debugText;
    public GameObject centerGameObject;

    MRUKRoom room;

    private void Start()
    {
        StartCoroutine(WaitForRoom());
    }

    IEnumerator WaitForRoom()
    {
        while (MRUK.Instance == null)
            yield return null;

        while (MRUK.Instance.GetCurrentRoom() == null)
            yield return null;

        room = MRUK.Instance.GetCurrentRoom();
        Log("Room detected");

        while (room.Anchors == null || room.Anchors.Count == 0)
            yield return null;

        Log($"Anchors encontrados: {room.Anchors.Count}");

        MRUKAnchor tableAnchor = null;

        foreach (var anchor in room.Anchors)
        {
            Log($"Anchor: {anchor.Label}");

            // Ojo: en tu versión el enum es MRUKAnchor.SceneLabels
            if (anchor.Label == MRUKAnchor.SceneLabels.TABLE)
            {
                tableAnchor = anchor;
                break;
            }
        }

        if (tableAnchor == null)
        {
            Log("No se encontró un anchor TABLE.");
            yield break;
        }

        PlaceOnTable(tableAnchor);
    }

    void PlaceOnTable(MRUKAnchor tableAnchor)
    {
        if (centerGameObject == null)
        {
            Log("centerGameObject no asignado.");
            return;
        }

        // Intentar obtener bounds desde un Collider (lo más estable entre versiones)
        Collider col = tableAnchor.GetComponentInChildren<Collider>();

        if (col != null)
        {
            Bounds b = col.bounds;

            // Colocar sobre la cara superior (mundo Y)
            Vector3 topCenter = b.center + Vector3.up * b.extents.y;

            centerGameObject.transform.position = topCenter;
            centerGameObject.transform.rotation = tableAnchor.transform.rotation;

            Log("centerGameObject colocado sobre TABLE (usando Collider.bounds).");
        }
        else
        {
            // Fallback: si no hay collider, al menos colócalo en el transform del anchor
            centerGameObject.transform.position = tableAnchor.transform.position;
            centerGameObject.transform.rotation = tableAnchor.transform.rotation;

            Log("TABLE encontrado pero sin Collider. Colocado en transform.position (fallback).");
        }
    }

    void Log(string msg)
    {
        Debug.Log(msg);
        if (debugText != null)
            debugText.text += "\n" + msg;
    }
}
