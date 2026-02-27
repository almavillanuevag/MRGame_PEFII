using Meta.XR.MRUtilityKit;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ShipMovement : MonoBehaviour
{
    [Header("Asignar elementos para interacciones")]
    public GameObject handpoint; // punto de la palma de la mano donde se posicionara la nave
    public GameObject HandGrabInteraction; // building block para dejar de interactuar
    public OVRSkeleton ovrSkeleton;
    public Transform trackingPoint;

    [Header("Opcional: para debugs")]
    public TMPro.TextMeshProUGUI debugText; // Para Display Debugs


    [Header("Objetos publicos para lectura")]
    public bool StartGame = true; // flag para que almacene el tiempo de inicio
    public float BeginningTime;
    public List<Vector3> patientTrajectory = new List<Vector3>();
    public int HandGrab = 0; // Conocer estado de que mano lo tomo:
                                 // 0 -> no ha colisionado
                                 // 1 -> mano izquierda
                                 // 2 -> mano derecha

    // -- Otras variables a declarar para funcionamiento interno --
    bool isHolding = false; 
    GameObject Ship;
    float sampleInterval = 1f / 60f;
    bool isRecording = false;
    float minDistance = 0.005f; // 0.5cm
    Vector3 lastSamplePos;
    Coroutine recordingCoroutine;

    private void Update()
    {
        if (isHolding && Ship != null)
        {
            Ship.transform.position = handpoint.transform.position;
            Ship.transform.rotation = handpoint.transform.rotation;
        }

    }

    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Ship"))
        {
            if (StartGame)
            {
                if (ovrSkeleton.name.ToLower().Contains("left")) 
                    HandGrab = 1;
                else 
                    HandGrab = 2;

                StartGame = false;
                BeginningTime = Time.time; // Marcar el tiempo donde inició del juego (cuando toca la nave por primera vez)
            }

            if (!isHolding)
            {
                GrabShip(other.gameObject);
                StartRecordingTrajectory(other.gameObject.transform);
            }
               
        }
    }

    // Función para que sujetar la nave con la mano cuando colisionen 
    void GrabShip(GameObject shipObj) 
    {
        isHolding = true;
        if(debugText !=null) debugText.text += "\nColision con la mano: se pegó";

        // posicionar y alinear con handpoint
        shipObj.transform.position = handpoint.transform.position;
        shipObj.transform.rotation = handpoint.transform.rotation;
        shipObj.transform.SetParent(handpoint.transform); // hacerlo hijo para que siga el movimiento

        // Desactivar gravedad, interacciones y hacerlo cinematico
        Ship = shipObj;
        Rigidbody rb = Ship.GetComponent<Rigidbody>();
        rb.useGravity = false;
        rb.isKinematic = true;

        // Desactivar interccion con la otra mano
        HandGrabInteraction.SetActive(false);
    }

    void StartRecordingTrajectory(Transform shipObj)
    {
        if (isRecording)
        {
            Debug.LogWarning("Ya se está grabando una trayectoria");
            return;
        }

        trackingPoint = shipObj;
        // Limpiar trayectoria previa
        patientTrajectory.Clear();

        // Guardar posición inicial
        lastSamplePos = trackingPoint.position;
        patientTrajectory.Add(lastSamplePos);

        isRecording = true;

        // Iniciar coroutine de muestreo
        if (recordingCoroutine != null)
        {
            StopCoroutine(recordingCoroutine);
        }
        recordingCoroutine = StartCoroutine(SampleTrajectoryCoroutine());
    }

    private IEnumerator SampleTrajectoryCoroutine()
    {
        while (isRecording)
        {
            Vector3 currentPos = trackingPoint.position;
            float distance = Vector3.Distance(currentPos, lastSamplePos);

            // Solo guardar si se movió lo suficiente
            if (distance >= minDistance)
            {
                patientTrajectory.Add(currentPos);
                lastSamplePos = currentPos;

            }

            yield return new WaitForSeconds(sampleInterval);
        }
    }

    // Función que permite que el Planeta obligue a la mano a soltar la nave
    public void ForceRelease()
    {
        isHolding = false;
        Ship = null;

        // Reactivar las interacciones de la mano
        HandGrabInteraction.SetActive(true);

        if(debugText !=null) debugText.text += "\nNave liberada";
    }

    public void StopRecordingTrajectory()
    {
        if (!isRecording)
        {
            return;
        }

        isRecording = false;

        if (recordingCoroutine != null)
        {
            StopCoroutine(recordingCoroutine);
            recordingCoroutine = null;
        }

        // Guardar último punto
        if (patientTrajectory.Count > 0)
        {
            Vector3 finalPos = trackingPoint.position;
            if (Vector3.Distance(finalPos, patientTrajectory[patientTrajectory.Count - 1]) > 0.001f)
            {
                patientTrajectory.Add(finalPos);
            }
        }
    }
}