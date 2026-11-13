using UnityEngine;

namespace Mossmere
{
    public class WorldDetails : MonoBehaviour
    {
        public Sprite[] frames;
        public float rate = 2;
        SpriteRenderer visual;
        void Awake() { visual = GetComponent<SpriteRenderer>(); }
        void Update()
        {
            if (frames != null && frames.Length > 0) visual.sprite = frames[(int)(Time.time * rate) % frames.Length];
        }
    }
}
