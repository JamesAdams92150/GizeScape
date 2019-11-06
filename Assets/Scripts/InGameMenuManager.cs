using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

public class InGameMenuManager : MonoBehaviour
{
    // Start is called before the first frame update
    public GameObject menuRoot;

    void Start()
    {
        menuRoot.SetActive(false);
    }

    // Update is called once per frame
    void Update()
    {
        //left click && menu active
        if (!menuRoot.activeSelf && Input.GetMouseButtonDown(0))
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        if (Input.GetButtonDown("Pause Menu")
           || (menuRoot.activeSelf && Input.GetButtonDown("Cancel")))
        {

            SetPauseMenuActivation(!menuRoot.activeSelf);

        }
        if (EventSystem.current.currentSelectedGameObject == null)
        {
            EventSystem.current.SetSelectedGameObject(null);
        }
    }

    public void titleReturn()
    {
        SceneManager.LoadScene("Main Menu");
    }

    public void ClosePauseMenu()
    {
        SetPauseMenuActivation(false);
    }

    void SetPauseMenuActivation(bool active)
    {
        menuRoot.SetActive(active);

        if (menuRoot.activeSelf)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            Time.timeScale = 0f;

            EventSystem.current.SetSelectedGameObject(null);
        }
        else
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
            Time.timeScale = 1f;
        }

    }
}
