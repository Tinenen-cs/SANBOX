using UnityEngine;

namespace Mossmere
{
    public class VillagerDialogue : MonoBehaviour
    {
        public string characterName;
        [TextArea(2,4)] public string[] lines;
        public float greetingRadius=1.65f;
    }
}
