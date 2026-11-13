using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Mossmere
{
    [RequireComponent(typeof(Rigidbody2D), typeof(BoxCollider2D))]
    public class Explorer : MonoBehaviour
    {
        public Sprite[] down, up, left, right;
        public Vector2 spawn;
        public float speed = 3.6f;
        public FollowCamera follow;
        public bool testControl;
        public Vector2 testInput;
        public int returns;
        public Vector2 Movement { get; private set; }
        public Rigidbody2D Body { get; private set; }
        SpriteRenderer visual;
        Sprite[] facing;
        float animationTime;
        float inputLock;
        readonly Dictionary<GameObject, int> layers = new();
        readonly Dictionary<Renderer, (int sorting, uint rendering, bool enabled)> renderers = new();

        void Awake()
        {
            Body = GetComponent<Rigidbody2D>();
            visual = GetComponent<SpriteRenderer>();
            facing = down;
            foreach (Transform t in GetComponentsInChildren<Transform>(true)) layers[t.gameObject] = t.gameObject.layer;
            foreach (Renderer r in GetComponentsInChildren<Renderer>(true)) renderers[r] = (r.sortingLayerID, r.renderingLayerMask, r.enabled);
        }

        void Update()
        {
            var keyboard = Keyboard.current;
            Vector2 input = testControl ? testInput : Vector2.zero;
            if (!testControl && keyboard != null)
            {
                input.x = (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed ? 1 : 0) - (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed ? 1 : 0);
                input.y = (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed ? 1 : 0) - (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed ? 1 : 0);
                if (keyboard.rKey.wasPressedThisFrame) ReturnToSpawn();
                if (keyboard.escapeKey.wasPressedThisFrame) Application.Quit();
            }
            if (Time.unscaledTime < inputLock) input = Vector2.zero;
            Movement = Vector2.ClampMagnitude(input, 1);
            if (Movement.sqrMagnitude > .01f)
            {
                facing = Mathf.Abs(Movement.x) > Mathf.Abs(Movement.y) ? (Movement.x > 0 ? right : left) : (Movement.y > 0 ? up : down);
                animationTime += Time.deltaTime * 8;
            }
            else animationTime = 0;
            if (facing != null && facing.Length > 0) visual.sprite = facing[(int)animationTime % facing.Length];
            visual.sortingOrder = 1000 - Mathf.RoundToInt((transform.position.y - .3f) * 16);
        }

        void FixedUpdate() { Body.linearVelocity = Movement * speed; }

        public void ReturnToSpawn()
        {
            foreach (var pair in layers) pair.Key.layer = pair.Value;
            foreach (var pair in renderers)
            {
                pair.Key.sortingLayerID = pair.Value.sorting;
                pair.Key.renderingLayerMask = pair.Value.rendering;
                pair.Key.enabled = pair.Value.enabled;
            }
            Body.simulated = true;
            Body.bodyType = RigidbodyType2D.Dynamic;
            Body.gravityScale = 0;
            Body.constraints = RigidbodyConstraints2D.FreezeRotation;
            Body.linearVelocity = Vector2.zero;
            Body.angularVelocity = 0;
            Body.position = spawn;
            transform.position = new Vector3(spawn.x, spawn.y, 0);
            var collision = GetComponent<BoxCollider2D>();
            collision.enabled = true;
            collision.isTrigger = false;
            Movement = Vector2.zero;
            testInput = Vector2.zero;
            inputLock = Time.unscaledTime + .22f;
            animationTime = 0;
            facing = down;
            visual.sprite = down[0];
            visual.sortingOrder = 1000 - Mathf.RoundToInt((spawn.y - .3f) * 16);
            Physics2D.SyncTransforms();
            follow.Snap();
            returns++;
        }
    }
}
