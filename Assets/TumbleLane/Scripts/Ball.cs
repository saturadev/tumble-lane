using UnityEngine;
using UnityEngine.InputSystem;

namespace TumbleLane
{
    // Pip the clementine: a physics ball pushed along the course and steered sideways.
    public class Ball : MonoBehaviour
    {
        public float cruise = 11.5f;
        public float latMax = 6.5f;
        public bool bot;
        public bool rolling;
        public bool braking;

        public Rigidbody rb;
        public int pathIndex;
        public bool grounded;
        public float steer;
        public Course course;

        float boostSpeed, boostUntil;
        float dragSteer;
        Transform trailRoot;
        Transform blob;
        int lastGroundIndex;

        public Vector3 Heading => course.path[pathIndex].fwd;
        public float Speed => rb.linearVelocity.magnitude;
        public float Progress => course.path[pathIndex].dist / course.length;

        public static Ball Create(Course course)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "Pip";
            go.transform.localScale = Vector3.one * (Course.BallRadius * 2f);
            go.GetComponent<Renderer>().sharedMaterial = Look.Ball;
            go.GetComponent<Collider>().sharedMaterial = new PhysicsMaterial("Ball") { dynamicFriction = 0.8f, staticFriction = 0.9f, bounciness = 0.05f, bounceCombine = PhysicsMaterialCombine.Minimum };

            // leaf and stem, so the roll reads
            var stem = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            Object.Destroy(stem.GetComponent<Collider>());
            stem.transform.SetParent(go.transform, false);
            stem.transform.localPosition = new Vector3(0, 0.5f, 0);
            stem.transform.localScale = new Vector3(0.07f, 0.06f, 0.07f);
            stem.GetComponent<Renderer>().sharedMaterial = Look.Stem;
            var leaf = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Object.Destroy(leaf.GetComponent<Collider>());
            leaf.transform.SetParent(go.transform, false);
            leaf.transform.localPosition = new Vector3(0.14f, 0.5f, 0.02f);
            leaf.transform.localRotation = Quaternion.Euler(0, 20, -25);
            leaf.transform.localScale = new Vector3(0.36f, 0.05f, 0.17f);
            leaf.GetComponent<Renderer>().sharedMaterial = Look.Leaf;
            // a pale navel spot on the other side
            var spot = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Object.Destroy(spot.GetComponent<Collider>());
            spot.transform.SetParent(go.transform, false);
            spot.transform.localPosition = new Vector3(0, -0.47f, 0);
            spot.transform.localScale = new Vector3(0.14f, 0.08f, 0.14f);
            spot.GetComponent<Renderer>().sharedMaterial = Look.Stem;

            var rb = go.AddComponent<Rigidbody>();
            rb.mass = 1f;
            rb.linearDamping = 0f;
            rb.angularDamping = 0.4f;
            rb.maxAngularVelocity = 60f;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

            var b = go.AddComponent<Ball>();
            b.rb = rb;
            b.course = course;
            b.MakeTrail();
            return b;
        }

        void MakeTrail()
        {
            trailRoot = new GameObject("PipTrail").transform;
            AddTrail(new Vector3(0, -0.15f, 0), new Color(1f, 0.55f, 0.1f), new Color(1f, 0.25f, 0.6f), 0.13f);
            AddTrail(new Vector3(0.18f, -0.25f, 0), new Color(1f, 0.9f, 0.3f), new Color(0.3f, 0.8f, 1f), 0.08f);
            AddTrail(new Vector3(-0.18f, -0.25f, 0), new Color(0.6f, 1f, 0.5f), new Color(0.75f, 0.4f, 1f), 0.08f);

            // soft contact shadow right under Pip, so it sits on the track and reads at a glance
            var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
            q.name = "PipShadow";
            Object.Destroy(q.GetComponent<Collider>());
            var r = q.GetComponent<Renderer>();
            r.sharedMaterial = Look.BlobShadow;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            blob = q.transform;
            blob.SetParent(trailRoot, false);
        }

        void UpdateBlob()
        {
            if (blob == null) return;
            if (Physics.Raycast(rb.position, Vector3.down, out var hit, 6f, ~0, QueryTriggerInteraction.Ignore))
            {
                float h = Mathf.Max(0f, hit.distance - Course.BallRadius);
                float k = Mathf.Clamp01(1f - h / 5f);
                blob.gameObject.SetActive(k > 0.02f);
                blob.position = hit.point + hit.normal * 0.025f;
                blob.rotation = Quaternion.FromToRotation(Vector3.forward, hit.normal);
                float size = Course.BallRadius * 2f * Mathf.Lerp(0.8f, 1.45f, k);
                blob.localScale = new Vector3(size, size, 1f);
                Look.BlobShadow.color = new Color(1f, 1f, 1f, 0.75f * k);
            }
            else blob.gameObject.SetActive(false);
        }

        void AddTrail(Vector3 off, Color a, Color b, float width)
        {
            var go = new GameObject("Trail");
            go.transform.SetParent(trailRoot, false);
            go.transform.localPosition = off;
            var tr = go.AddComponent<TrailRenderer>();
            tr.time = 0.45f;
            tr.minVertexDistance = 0.1f;
            tr.widthCurve = new AnimationCurve(new Keyframe(0, width), new Keyframe(1, 0));
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(a, 0), new GradientColorKey(b, 1) },
                      new[] { new GradientAlphaKey(0.95f, 0), new GradientAlphaKey(0f, 1) });
            tr.colorGradient = g;
            tr.sharedMaterial = Look.Trail;
            tr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        public void ClearTrails()
        {
            foreach (var tr in trailRoot.GetComponentsInChildren<TrailRenderer>()) tr.Clear();
        }

        public void Place(Vector3 pos, Vector3 fwd, int index, float speed)
        {
            rb.position = pos;
            transform.position = pos;
            rb.linearVelocity = fwd * speed;
            rb.angularVelocity = Vector3.zero;
            pathIndex = index;
            lastGroundIndex = index;
            boostUntil = 0;
            braking = false;
            trailRoot.position = pos;
            ClearTrails();
        }

        public void Boost(float speed)
        {
            boostSpeed = speed;
            boostUntil = Time.time + 0.25f;
        }

        public bool Boosting => Time.time < boostUntil;

        void Update()
        {
            if (!bot) ReadInput();
        }

        void LateUpdate()
        {
            if (trailRoot == null) return;
            trailRoot.position = transform.position;
            Vector3 v = rb.linearVelocity; v.y = 0;
            if (v.sqrMagnitude > 0.5f) trailRoot.rotation = Quaternion.LookRotation(v.normalized, Vector3.up);
            UpdateBlob();
        }

        void ReadInput()
        {
            float key = 0;
            var kb = Keyboard.current;
            if (kb != null)
            {
                if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) key -= 1;
                if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) key += 1;
                if (kb.anyKey.wasPressedThisFrame) rolling = true;
            }
            var ptr = Pointer.current;
            float drag = 0;
            if (ptr != null && ptr.press.isPressed)
            {
                rolling = true;
                drag = ptr.delta.ReadValue().x / Mathf.Max(1f, Screen.width * 0.01f);
            }
            dragSteer = Mathf.Lerp(dragSteer, Mathf.Clamp(drag, -1f, 1f), 1f - Mathf.Exp(-Time.deltaTime * 18f));
            steer = Mathf.Clamp(key + dragSteer, -1f, 1f);
        }

        void FixedUpdate()
        {
            if (course == null) return;
            pathIndex = course.Nearest(rb.position, pathIndex);
            var pp = course.path[pathIndex];
            grounded = Physics.Raycast(rb.position, Vector3.down, Course.BallRadius + 0.2f, ~0, QueryTriggerInteraction.Ignore);
            if (grounded) lastGroundIndex = pathIndex;

            if (bot) steer = BotSteer();
            if (!rolling) return;

            Vector3 fwd = pp.fwd;
            Vector3 right = Vector3.Cross(Vector3.up, fwd);
            Vector3 v = rb.linearVelocity;
            float vf = Vector3.Dot(v, fwd);
            float vl = Vector3.Dot(v, right);

            float target = braking ? 0f : (Boosting ? boostSpeed : cruise);
            float desiredLat = steer * latMax;
            if (grounded)
            {
                float gain = Boosting ? 10f : 3.5f;
                float af = Mathf.Clamp((target - vf) * gain, braking ? -12f : -14f, Boosting ? 60f : 22f);
                rb.AddForce(fwd * af, ForceMode.Acceleration);
                float al = Mathf.Clamp((desiredLat - vl) * 9f, -32f, 32f);
                rb.AddForce(right * al, ForceMode.Acceleration);
            }
            else
            {
                float al = Mathf.Clamp((desiredLat - vl) * 2f, -6f, 6f);
                rb.AddForce(right * al, ForceMode.Acceleration);
            }
        }

        // ---------- bot ----------

        public float BotTargetOffset(int lookIdx, float arriveIn)
        {
            float best = 0;
            var pp = course.path[lookIdx];
            float half = Mathf.Max(0.2f, pp.width * 0.5f - 0.75f);
            foreach (var bl in course.blockers)
            {
                float ahead = bl.pathDist - course.path[pathIndex].dist;
                if (ahead < -1.5f || ahead > 14f) continue;
                float t = Time.time + Mathf.Max(0, ahead) / Mathf.Max(4f, Speed);
                float bestScore = -1;
                for (float off = -half; off <= half + 0.01f; off += half / 3f)
                {
                    float score = float.MaxValue;
                    for (float dt = -0.35f; dt <= 0.35f; dt += 0.07f)
                        score = Mathf.Min(score, Mathf.Abs(off - bl.OffsetAt(t + dt)));
                    score -= Mathf.Abs(off) * 0.05f;
                    if (score > bestScore) { bestScore = score; best = off; }
                }
                break;
            }
            return best;
        }

        float BotSteer()
        {
            var here = course.path[pathIndex];
            float sp = Mathf.Max(4f, Speed);
            int look = Mathf.Min(course.path.Count - 1, pathIndex + Mathf.RoundToInt(sp * 0.32f / 0.5f) + 2);
            var target = course.path[look];
            if (target.width <= 0) target = here; // over a gap: keep the line
            Vector3 right = Vector3.Cross(Vector3.up, here.fwd);
            Vector3 tRight = Vector3.Cross(Vector3.up, target.fwd);
            float off = BotTargetOffset(look, 0);
            Vector3 aim = target.pos + tRight * off;
            Vector3 d = aim - rb.position; d.y = 0;
            float along = Mathf.Max(1.5f, Vector3.Dot(d, here.fwd));
            float lookTime = Mathf.Max(0.22f, along / sp);
            float wantLat = Vector3.Dot(d, right) / lookTime;
            return Mathf.Clamp(wantLat / latMax, -1f, 1f);
        }

        public bool Fallen => rb.position.y < course.path[lastGroundIndex].pos.y - 5f;
    }
}
