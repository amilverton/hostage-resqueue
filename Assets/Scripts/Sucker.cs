using System;
using UnityEngine;
using TMPro;

public static class GameState
{
    public static int Score = 0;
}


public class Sucker : MonoBehaviour
{
    
    private void OnTriggerEnter(Collider collidedWith)
    {
        // Here you have access to the object that has collided with you.
        if (collidedWith.gameObject.CompareTag("Civilian"))
        {
            
            Destroy(collidedWith.gameObject);
            GameState.Score += 1;
        }
    }

    /// <summary>
    /// THIS FUNCTION IS CALLED WHEN I COLLIDE WITH ANOTHER CIVILIAN
    /// </summary>
    /// <param name="collidedLeft"></param>
    private void OnTriggerExit(Collider collidedLeft)
    {
        // Here you have access to the object that has exited the trigger.
        if (collidedLeft.gameObject.CompareTag("Civilian"))
        {
            
        }
    }
}