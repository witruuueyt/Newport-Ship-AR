using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Portal : MonoBehaviour
{
    public Transform fooled;
    public GameObject endingPanel1;
    new AudioSource audio;
    // Start is called before the first frame update
    void Start()
    {
        audio = GetComponent<AudioSource> ();
    }

    // Update is called once per frame
    void Update()
    {
        //Debug.Log("hello");
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.CompareTag("portal"))
        {
            Debug.Log("portal from player");
            transform.position = fooled.position;
            endingPanel1.SetActive(true);
            //audio.Play();
        }

    
        

    }

    public void OnTriggerEnter(Collider other)
    {
        if (other.tag == "portal")
        {
            Debug.Log("trigger");
            transform.position = fooled.position;
            endingPanel1.SetActive(true);
            //fire.SetActive(true);
        }
    }
}
