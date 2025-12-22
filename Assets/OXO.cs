using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;
using System;
using TMPro;
using UnityEngine.SceneManagement;
using UnityEngine.EventSystems;

public class OXO : MonoBehaviour
{
    public GameObject[] spaces = new GameObject[9];
    public GameObject cam;
    public GameObject StartButton;
    public GameObject X;
    public GameObject O;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        cam.GetComponent<AudioSource>().enabled = false;
        if (StartButton != null)
        {
            StartButton.SetActive(true);
            // var btn because it needed to stored and then accessed again.
            var btn = StartButton.GetComponent<Button>();
            // not null because its checking if the button is active or not and if so start the game.
            if (btn != null)
            {
                btn.onClick.AddListener(StartGame);
            }
            else
            {
                Debug.LogError("StartButton does not have a Button component.");
            }
        }
    }
    
    public void StartGame()
    {
        cam.GetComponent<AudioSource>().enabled = true;
        if (StartButton != null)
        {
            // Deactivate the button's GameObject when clicked
            StartButton.SetActive(false);
        }
    }

    // Update is called once per frame
    void Update()
    {
    
    }
}