using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class ImageSwitcher : MonoBehaviour
{
    public Image image; 
    public Sprite[] images; 
    private int currentIndex = 0; 
    void Start()
    {
       
        if (image != null && images.Length > 0)
        {
            image.sprite = images[currentIndex];
        }
    }

    public void NextImage()
    {
        
        if (images.Length > 0)
        {
            currentIndex = (currentIndex + 1) % images.Length;
            image.sprite = images[currentIndex];
        }
    }

    public void PreviousImage()
    {
      
        if (images.Length > 0)
        {
            currentIndex = (currentIndex - 1 + images.Length) % images.Length;
            image.sprite = images[currentIndex];
        }
    }
}