using UnityEngine;

namespace TumbleLane
{
    // Chase camera. Technique adapted from VladimirPirozhenko/AwesomeRunner FollowingCamera.cs (MIT):
    // target + rotated offset, SmoothDamp to it, then look at the target.
    public class ChaseCamera : MonoBehaviour
    {
        public Ball ball;
        // Closer and with less look-ahead than before, so Pip sits larger and higher in the portrait frame
        // (about two thirds down) while the track ahead stays in view.
        public float back = 5.0f, up = 2.75f, lookAhead = 2.0f, lookUp = 0.2f, damp = 0.09f;
        Vector3 vel;
        Vector3 heading = Vector3.forward;
        float baseY;
        public bool frozen;

        public void Snap()
        {
            if (ball == null) return;
            heading = ball.Heading;
            baseY = ball.transform.position.y;
            transform.position = Desired();
            transform.rotation = Quaternion.LookRotation(LookPoint() - transform.position, Vector3.up);
            vel = Vector3.zero;
        }

        Vector3 Desired()
        {
            Vector3 p = ball.transform.position; p.y = baseY;
            return p - heading * back + Vector3.up * up;
        }

        Vector3 LookPoint()
        {
            Vector3 p = ball.transform.position; p.y = Mathf.Lerp(baseY, p.y, 0.6f);
            return p + heading * lookAhead + Vector3.up * lookUp;
        }

        void LateUpdate()
        {
            if (ball == null) return;
            if (frozen)
            {
                var toBall = ball.transform.position - transform.position;
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(toBall, Vector3.up), 1f - Mathf.Exp(-Time.deltaTime * 6f));
                return;
            }
            Vector3 v = ball.rb.linearVelocity; v.y = 0;
            Vector3 want = ball.Heading;
            if (v.magnitude > 3f) want = Vector3.Slerp(want, v.normalized, 0.35f);
            heading = Vector3.Slerp(heading, want, 1f - Mathf.Exp(-Time.deltaTime * 4.5f)).normalized;
            // follow height softly so jumps read as jumps
            baseY = Mathf.Lerp(baseY, ball.transform.position.y, 1f - Mathf.Exp(-Time.deltaTime * (ball.grounded ? 6f : 2.2f)));
            transform.position = Vector3.SmoothDamp(transform.position, Desired(), ref vel, damp);
            var rot = Quaternion.LookRotation(LookPoint() - transform.position, Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, rot, 1f - Mathf.Exp(-Time.deltaTime * 14f));
        }
    }
}
