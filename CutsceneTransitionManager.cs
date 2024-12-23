using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using WSoft.Core;

/// <summary>
/// Transitions Scene to gameplay scene
/// </summary>

public class CutsceneTransitionManager : MonoBehaviour
{
    public int cutsceneLength;
    public GameObject Prompt;

    private bool isHoldingSpace = false;
    private float holdTime;
    public float requiredHoldTime;

    void Start()
    {
        Prompt.SetActive(false);
        StartCoroutine(LetCutscenePlay(cutsceneLength));
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Space))
        {
            Prompt.SetActive(true);
        }

        if (Input.GetKey(KeyCode.Space))
        {
            isHoldingSpace = true;
            holdTime += Time.deltaTime;

            if (holdTime >= requiredHoldTime)
            {
                SkipCutscene();
            }
        }
        else
        {
            isHoldingSpace = false;
            holdTime = 0f;
        }
    }

    private void SkipCutscene()
    {
        SceneManager.LoadScene("SmallMap");
    }

    public IEnumerator LetCutscenePlay(float seconds)
    {
        yield return new WaitForSeconds(seconds);

        if (!isHoldingSpace)
        {
            SceneManager.LoadScene("SmallMap");
        }
    }
}
