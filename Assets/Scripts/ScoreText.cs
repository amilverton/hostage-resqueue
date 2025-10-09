using TMPro;
using UnityEngine;
using UnityEngine.UI;
public class ScoreText : MonoBehaviour
{
    public TextMeshProUGUI  scoreText;

    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        scoreText.text = $"{GameState.Score}";
    }
}
