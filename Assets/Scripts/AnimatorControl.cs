using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AnimatorControl : MonoBehaviour
{
    public Animator animator;

    public void PlayAnimation()
    {
        animator.SetTrigger("Play");
    }
}
