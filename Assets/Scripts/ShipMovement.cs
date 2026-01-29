using UnityEngine;
using Meta.XR.MRUtilityKit;

public class ShipMovement : MonoBehaviour
{
    public GameObject handpoint; // punto de la palma de la mano donde se posicionara la nave
    public GameObject HandGrabInteraction; // building block para dejar de interactuar
    private GameObject Ship;

    private bool isHolding = false; // flag para sujetar
    public bool StartGame = true; // flag para que almacene el tiempo de inicio
    public float BeginningTime;
    public TMPro.TextMeshProUGUI debugText; // Para Display Debugs

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
                StartGame = false;
                BeginningTime = Time.time; // Marcar el tiempo donde inició del juego (cuando toca la nave por primera vez)
            }
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

        // Desactivar gravedad y hacerlo cinematico
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

        // Reactivar la interacción de la mano (Building Block)
        HandGrabInteraction.SetActive(true);

        debugText.text += "\nNave liberada";
    }
}