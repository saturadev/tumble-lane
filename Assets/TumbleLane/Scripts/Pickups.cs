using UnityEngine;

namespace TumbleLane
{
    public class CoinPickup : MonoBehaviour
    {
        bool taken;

        void Update()
        {
            transform.rotation = Quaternion.Euler(0, Time.time * 160f + transform.position.z * 7f, 90f);
        }

        void OnTriggerEnter(Collider other)
        {
            if (taken || other.GetComponentInParent<Ball>() == null) return;
            taken = true;
            Game.I?.OnCoin(transform.position);
            gameObject.SetActive(false);
        }

        public void ResetCoin()
        {
            taken = false;
            gameObject.SetActive(true);
        }
    }

    public class HoopGate : MonoBehaviour
    {
        bool passed;

        void OnTriggerEnter(Collider other)
        {
            if (passed || other.GetComponentInParent<Ball>() == null) return;
            passed = true;
            Game.I?.OnHoop(transform.position);
        }

        Renderer[] beads;

        void Update()
        {
            transform.Rotate(0, 0, 40f * Time.deltaTime, Space.Self);
            // the chase camera flies through right after the ball: hide the beads so they don't fill the screen
            var cam = Camera.main;
            if (cam == null) return;
            if (beads == null) beads = GetComponentsInChildren<Renderer>();
            Vector3 toHoop = transform.position - cam.transform.position;
            bool show = toHoop.magnitude > 3.2f || Vector3.Dot(toHoop, cam.transform.forward) > 2.5f;
            foreach (var b in beads) b.enabled = show;
        }

        public void ResetHoop() => passed = false;
    }

    public class BoostZone : MonoBehaviour
    {
        public float speed = 17f;

        void OnTriggerStay(Collider other)
        {
            var b = other.GetComponentInParent<Ball>();
            if (b != null) b.Boost(speed);
        }
    }

    public class Blocker : MonoBehaviour
    {
        public Vector3 center, right;
        public float amp, speed, phase, pathDist, halfWidth;
        Rigidbody rb;

        void Awake() => rb = GetComponent<Rigidbody>();

        public float OffsetAt(float t) => Mathf.Sin(t * speed + phase) * amp;

        void FixedUpdate()
        {
            rb.MovePosition(center + right * OffsetAt(Time.time));
        }
    }
}
