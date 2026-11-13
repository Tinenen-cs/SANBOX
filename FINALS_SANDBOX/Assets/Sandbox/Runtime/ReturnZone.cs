using UnityEngine;

namespace Mossmere
{
    public class ReturnZone : MonoBehaviour
    {
        void OnTriggerEnter2D(Collider2D other)
        {
            var player = other.GetComponentInParent<Explorer>();
            if (player != null) player.ReturnToSpawn();
        }
    }
}
