using UnityEngine;

public class FinishTrigger : MonoBehaviour
{
    private void OnTriggerEnter(Collider other)
    {
        // Реагируем на игрока — поставь нужный тег или измени проверку
        if (other.CompareTag("Player"))
        {
            GameManager.Instance?.TriggerWin();
        }
    }
}
