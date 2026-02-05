using UnityEngine;
using Meta.XR.MRUtilityKit;

public class ShipMovement : MonoBehaviour
{
    [Header("Asignar elementos para interacciones")]
    public GameObject handpoint; // punto de la palma de la mano donde se posicionara la nave
    public GameObject HandGrabInteraction; // building block para dejar de interactuar
    public OVRSkeleton ovrSkeleton;
    public TMPro.TextMeshProUGUI debugText; // Para Display Debugs


    [Header("Objetos publicos para lectura")]
    public bool StartGame = true; // flag para que almacene el tiempo de inicio
    public float BeginningTime;
    public int HandGrab = 0; // Conocer estado de que mano lo tomo:
                                 // 0 -> no ha colisionado
                                 // 1 -> mano izquierda
                                 // 2 -> mano derecha

    // -- Otras variables a declarar para funcionamiento interno --
    bool isHolding = false; 
    GameObject Ship;

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
                if (ovrSkeleton.name.ToLower().Contains("left")) HandGrab = 1;
                else HandGrab = 2;

                StartGame = false;
                BeginningTime = Time.time; // Marcar el tiempo donde inició del juego (cuando toca la nave por primera vez)
            }

            if(!isHolding)
                GrabShip(other.gameObject);
        }
    }

    // Función para que sujetar la nave con la mano cuando colisionen 
    void GrabShip(GameObject shipObj) 
    {
        isHolding = true;
        debugText.text += "\nColision con la mano: se pegó";

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

    // Función que permite que el Planeta obligue a la mano a soltar la nave
    public void ForceRelease()
    {
        isHolding = false;
        Ship = null;

        // Reactivar las interacciones de la mano
        HandGrabInteraction.SetActive(true);

        debugText.text += "\nNave liberada";
    }
}