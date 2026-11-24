using System.Collections.Generic;
using UnityEngine;

namespace TumbleLane
{
    public enum Style { Wood, Asphalt, Chevron }

    public struct PathPoint
    {
        public Vector3 pos;     // centre of the top surface (or the line through a gap)
        public Vector3 fwd;     // horizontal heading of the course here
        public float width;     // 0 over a gap
        public float dist;      // metres from the start
    }

    public struct Checkpoint
    {
        public int index;
        public Vector3 pos;
        public Vector3 fwd;
    }

    // Builds the whole floating course from code: a cursor walks forward and lays pieces.
    public class Course : MonoBehaviour
    {
        public const float Thickness = 0.8f;
        public const float BallRadius = 0.5f;

        public readonly List<PathPoint> path = new List<PathPoint>();
        public readonly List<Checkpoint> checkpoints = new List<Checkpoint>();
        public readonly List<Blocker> blockers = new List<Blocker>();
        public float finishDist;
        public float length;

        Vector3 cur;
        float yaw;
        float dist;
        int segCount;
        PhysicsMaterial surface;

        Vector3 Fwd => Quaternion.Euler(0, yaw, 0) * Vector3.forward;
        Vector3 Right => Quaternion.Euler(0, yaw, 0) * Vector3.right;

        public void Build(float launchSpeed)
        {
            Look.Build();
            surface = new PhysicsMaterial("Track") { dynamicFriction = 0.7f, staticFriction = 0.8f, bounciness = 0.02f, bounceCombine = PhysicsMaterialCombine.Minimum };
            cur = Vector3.zero; yaw = 0; dist = 0;

            // 1. start pad and a wooden run-up with coins
            Straight(10, 6f, Style.Wood);
            AddCheckpoint(-6f);
            float d0 = dist;
            Straight(18, 3.0f, Style.Wood);
            CoinRow(d0 + 3, d0 + 17, 0f, 2.5f);

            // 2. jog left onto a thin plank
            d0 = dist;
            Shift(10, -2.0f, 1.8f, Style.Wood);
            Straight(8, 1.8f, Style.Wood);
            CoinRow(d0 + 2, d0 + 17, 0f, 2.5f);

            // 3. asphalt bend right, chevron boost, ramp, gap with a hoop
            Bend(70, 12, 3.2f, Style.Asphalt);
            d0 = dist;
            Straight(12, 3.2f, Style.Chevron, boost: launchSpeed);
            Ramp(6, 1.8f, 3.0f, launchSpeed);
            Gap(9, -2.5f, launchSpeed, 4.0f);

            // 4. landing, then the narrow beam
            Straight(14, 4f, Style.Wood);
            AddCheckpoint(-11f);
            d0 = dist;
            Straight(16, 1.3f, Style.Wood);
            CoinRow(d0 + 2, d0 + 15, 0f, 2.6f);
            Bend(-90, 10, 2.4f, Style.Wood);

            // 5. wide asphalt with sliding blockers
            d0 = dist;
            Straight(28, 5.5f, Style.Asphalt);
            AddBlocker(d0 + 9f, 1.7f, 1.6f, 0f);
            AddBlocker(d0 + 19f, 1.7f, 1.9f, 1.7f);
            CoinRow(d0 + 4, d0 + 26, 0f, 3f, zig: 1.4f);

            // 6. zigzag planks
            d0 = dist;
            Shift(8, 2.2f, 1.6f, Style.Wood);
            Shift(8, -2.2f, 1.6f, Style.Wood);
            CoinRow(d0 + 1, d0 + 15, 0f, 2.4f);

            // 7. railed downhill
            Straight(4, 3.2f, Style.Wood);
            AddCheckpoint(-2f);
            d0 = dist;
            Straight(20, 3.2f, Style.Wood, rise: -5f, rails: true);
            CoinRow(d0 + 3, d0 + 19, 0f, 2.5f);
            Bend(40, 16, 3.2f, Style.Asphalt);

            // 8. second boost and jump
            Straight(10, 3.0f, Style.Chevron, boost: launchSpeed);
            Ramp(5, 1.5f, 3.0f, launchSpeed);
            Gap(8, -1.5f, launchSpeed, 3.4f);

            // 9. finish straight with the crate tower
            d0 = dist;
            Straight(44, 5f, Style.Wood, rails: true);
            finishDist = d0 + 12f;
            FinishLine(d0 + 12f);
            CrateTower(d0 + 19f);
            length = dist;

            Scenery();
        }

        // ---------- pieces ----------

        void Straight(float len, float w, Style s, float rise = 0, bool rails = false, float boost = 0)
        {
            Vector3 a = cur, b = cur + Fwd * len + Vector3.up * rise;
            Seg(a, b, w, s, rails, boost);
            AddPath(a, b, w);
            cur = b;
        }

        void Shift(float len, float dx, float w, Style s)
        {
            Vector3 a = cur, b = cur + Fwd * len + Right * dx;
            Seg(a, b, w, s, false, 0);
            AddPath(a, b, w);
            cur = b;
        }

        void Bend(float angle, float radius, float w, Style s)
        {
            float arc = Mathf.Abs(angle) * Mathf.Deg2Rad * radius;
            int n = Mathf.Max(4, Mathf.CeilToInt(arc / 1.4f));
            float step = angle / n;
            float chord = 2f * radius * Mathf.Sin(Mathf.Abs(step) * 0.5f * Mathf.Deg2Rad);
            for (int i = 0; i < n; i++)
            {
                yaw += step * 0.5f;
                Vector3 a = cur, b = cur + Fwd * chord;
                Seg(a, b, w, s, false, 0);
                AddPath(a, b, w);
                yaw += step * 0.5f;
                cur = b;
            }
        }

        void Ramp(float len, float rise, float w, float boost)
        {
            Vector3 a = cur, b = cur + Fwd * len + Vector3.up * rise;
            Seg(a, b, w, Style.Chevron, false, boost);
            AddPath(a, b, w);
            cur = b;
        }

        // A jump. Simulates the flight from the ramp lip to place the hoop on the arc
        // and lays coins along it.
        void Gap(float len, float drop, float speed, float hoopAt)
        {
            Vector3 start = cur;
            Vector3 lipDir = path[path.Count - 1].pos - path[path.Count - 8].pos;
            lipDir.Normalize();
            Vector3 p = start + Vector3.up * BallRadius;
            Vector3 v = lipDir * speed;
            Vector3 hoop = Vector3.zero;
            var arc = new List<Vector3>();
            float dt = 0.01f;
            for (int i = 0; i < 400; i++)
            {
                p += v * dt; v += Physics.gravity * dt;
                float along = Vector3.Dot(p - start, Fwd);
                if (hoop == Vector3.zero && along >= hoopAt) hoop = p;
                if (i % 9 == 0 && along > 1f && along < len - 0.5f) arc.Add(p);
                if (along > len + 4f) break;
            }
            Vector3 end = cur + Fwd * len + Vector3.up * drop;
            AddPath(start, end, 0f);
            cur = end;
            Hoop(hoop);
            for (int i = 0; i < arc.Count; i++)
                if ((arc[i] - hoop).magnitude > 1.6f) Coin(arc[i]);
        }

        void AddPath(Vector3 a, Vector3 b, float w)
        {
            float len = Vector3.Distance(a, b);
            int n = Mathf.Max(1, Mathf.CeilToInt(len / 0.5f));
            for (int i = (path.Count == 0 ? 0 : 1); i <= n; i++)
            {
                float t = i / (float)n;
                path.Add(new PathPoint { pos = Vector3.Lerp(a, b, t), fwd = Fwd, width = w, dist = dist + len * t });
            }
            dist += len;
        }

        void Seg(Vector3 a, Vector3 b, float w, Style s, bool rails, float boost)
        {
            const float overlap = 0.35f;
            Vector3 dir = b - a;
            float len = dir.magnitude + overlap;
            var rot = Quaternion.LookRotation(dir.normalized, Vector3.up);
            var root = new GameObject($"Seg{segCount:000}_{s}");
            root.transform.SetParent(transform, false);
            root.transform.SetPositionAndRotation((a + b) * 0.5f, rot);

            var body = GameObject.CreatePrimitive(PrimitiveType.Cube);
            body.name = "Body";
            body.transform.SetParent(root.transform, false);
            body.transform.localPosition = new Vector3(0, -Thickness * 0.5f, 0);
            body.transform.localScale = new Vector3(w, Thickness, len);
            body.GetComponent<Renderer>().sharedMaterial = s == Style.Wood ? Look.WoodSide : Look.AsphaltSide;
            body.GetComponent<Collider>().sharedMaterial = surface;

            var top = GameObject.CreatePrimitive(PrimitiveType.Quad);
            top.name = "Top";
            Object.Destroy(top.GetComponent<Collider>());
            top.transform.SetParent(root.transform, false);
            top.transform.localPosition = new Vector3(0, (segCount % 2 == 0) ? 0.004f : 0.012f, 0);
            top.transform.localRotation = Quaternion.Euler(90, 0, 0);
            top.transform.localScale = new Vector3(w, len, 1);
            var r = top.GetComponent<Renderer>();
            r.sharedMaterial = s == Style.Wood ? Look.Wood : s == Style.Asphalt ? Look.Asphalt : Look.Chevron;
            float tileLen = s == Style.Wood ? w * 2f : s == Style.Asphalt ? Mathf.Max(3f, w) : Mathf.Min(w, 3f);
            Look.Tile(r, new Vector2(1, len / tileLen));

            if (boost > 0)
            {
                var trig = new GameObject("Boost");
                trig.transform.SetParent(root.transform, false);
                trig.transform.localPosition = new Vector3(0, 0.6f, 0);
                var bc = trig.AddComponent<BoxCollider>();
                bc.isTrigger = true;
                bc.size = new Vector3(w, 1.2f, len);
                trig.AddComponent<BoostZone>().speed = boost;
            }

            if (rails)
            {
                for (int side = -1; side <= 1; side += 2)
                {
                    var rail = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                    rail.name = "Rail";
                    rail.transform.SetParent(root.transform, false);
                    rail.transform.localPosition = new Vector3(side * (w * 0.5f - 0.1f), 0.45f, 0);
                    rail.transform.localRotation = Quaternion.Euler(90, 0, 0);
                    rail.transform.localScale = new Vector3(0.22f, len * 0.5f, 0.22f);
                    var rr = rail.GetComponent<Renderer>();
                    rr.sharedMaterial = Look.Hazard;
                    Look.Tile(rr, new Vector2(1, len * 1.2f));
                    Object.Destroy(rail.GetComponent<Collider>());
                    var col = rail.AddComponent<BoxCollider>();
                    col.size = new Vector3(1, 2, 1);
                    for (float z = -len * 0.5f + 0.5f; z < len * 0.5f; z += 3f)
                    {
                        var post = GameObject.CreatePrimitive(PrimitiveType.Cube);
                        post.name = "Post";
                        Object.Destroy(post.GetComponent<Collider>());
                        post.transform.SetParent(root.transform, false);
                        post.transform.localPosition = new Vector3(side * (w * 0.5f - 0.1f), 0.22f, z);
                        post.transform.localScale = new Vector3(0.1f, 0.45f, 0.1f);
                        post.GetComponent<Renderer>().sharedMaterial = Look.WoodSide;
                    }
                }
            }
            segCount++;
        }

        void AddCheckpoint(float back)
        {
            float d = dist + back;
            int idx = IndexAtDist(d);
            var pp = path[idx];
            checkpoints.Add(new Checkpoint { index = idx, pos = pp.pos, fwd = pp.fwd });
            var pad = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            pad.name = "Checkpoint";
            Object.Destroy(pad.GetComponent<Collider>());
            pad.transform.SetParent(transform, false);
            pad.transform.position = pp.pos + Vector3.up * 0.02f;
            float s = Mathf.Min(pp.width * 0.8f, 2.6f);
            pad.transform.localScale = new Vector3(s, 0.01f, s);
            pad.GetComponent<Renderer>().sharedMaterial = Look.Checkpoint;
        }

        public int IndexAtDist(float d)
        {
            int lo = 0, hi = path.Count - 1;
            while (lo < hi)
            {
                int mid = (lo + hi) / 2;
                if (path[mid].dist < d) lo = mid + 1; else hi = mid;
            }
            return lo;
        }

        void CoinRow(float from, float to, float lateral, float step, float zig = 0)
        {
            int k = 0;
            for (float d = from; d <= to; d += step, k++)
            {
                var pp = path[IndexAtDist(d)];
                if (pp.width <= 0) continue;
                float off = lateral + (zig > 0 ? Mathf.Sin(k * 0.9f) * zig : 0);
                Vector3 right = Vector3.Cross(Vector3.up, pp.fwd);
                Coin(pp.pos + right * off + Vector3.up * 0.6f);
            }
        }

        void Coin(Vector3 at)
        {
            var c = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            c.name = "Coin";
            Object.Destroy(c.GetComponent<Collider>());
            c.transform.SetParent(transform, false);
            c.transform.position = at;
            c.transform.localScale = new Vector3(0.55f, 0.05f, 0.55f);
            c.GetComponent<Renderer>().sharedMaterial = Look.Coin;
            var sc = c.AddComponent<SphereCollider>();
            sc.isTrigger = true;
            sc.radius = 1.3f;
            c.AddComponent<CoinPickup>();
        }

        void Hoop(Vector3 at)
        {
            var root = new GameObject("Hoop");
            root.transform.SetParent(transform, false);
            root.transform.position = at;
            root.transform.rotation = Quaternion.LookRotation(Fwd, Vector3.up);
            const int n = 28;
            const float radius = 1.55f;
            for (int i = 0; i < n; i++)
            {
                float a = i / (float)n * Mathf.PI * 2f;
                var bead = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                Object.Destroy(bead.GetComponent<Collider>());
                bead.transform.SetParent(root.transform, false);
                bead.transform.localPosition = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0) * radius;
                bead.transform.localScale = Vector3.one * 0.42f;
                bead.GetComponent<Renderer>().sharedMaterial = (i % 2 == 0) ? Look.HoopA : Look.HoopB;
            }
            var trig = root.AddComponent<SphereCollider>();
            trig.isTrigger = true;
            trig.radius = radius - 0.2f;
            root.AddComponent<HoopGate>();
        }

        void AddBlocker(float d, float amp, float speed, float phase)
        {
            var pp = path[IndexAtDist(d)];
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "Blocker";
            go.transform.SetParent(transform, false);
            go.transform.rotation = Quaternion.LookRotation(pp.fwd, Vector3.up);
            go.transform.localScale = new Vector3(1.5f, 1.1f, 0.9f);
            go.transform.position = pp.pos + Vector3.up * 0.55f;
            var r = go.GetComponent<Renderer>();
            r.sharedMaterial = Look.Hazard;
            Look.Tile(r, new Vector2(1.5f, 1.1f));
            var rb = go.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            var b = go.AddComponent<Blocker>();
            b.center = pp.pos + Vector3.up * 0.55f;
            b.right = Vector3.Cross(Vector3.up, pp.fwd);
            b.amp = amp; b.speed = speed; b.phase = phase;
            b.pathDist = d;
            b.halfWidth = 0.75f;
            blockers.Add(b);
        }

        void FinishLine(float d)
        {
            var pp = path[IndexAtDist(d)];
            var root = new GameObject("FinishLine");
            root.transform.SetParent(transform, false);
            root.transform.SetPositionAndRotation(pp.pos, Quaternion.LookRotation(pp.fwd, Vector3.up));
            // checker strip
            for (int i = 0; i < 10; i++)
            for (int j = 0; j < 2; j++)
            {
                var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
                Object.Destroy(q.GetComponent<Collider>());
                q.transform.SetParent(root.transform, false);
                q.transform.localPosition = new Vector3(-2.25f + i * 0.5f, 0.02f, j * 0.5f);
                q.transform.localRotation = Quaternion.Euler(90, 0, 0);
                q.transform.localScale = new Vector3(0.5f, 0.5f, 1);
                q.GetComponent<Renderer>().sharedMaterial = ((i + j) % 2 == 0) ? Look.HoopB : Look.HoopA;
            }
            // arch
            for (int side = -1; side <= 1; side += 2)
            {
                var post = GameObject.CreatePrimitive(PrimitiveType.Cube);
                Object.Destroy(post.GetComponent<Collider>());
                post.transform.SetParent(root.transform, false);
                post.transform.localPosition = new Vector3(side * 2.7f, 3.5f, 0.25f);
                post.transform.localScale = new Vector3(0.3f, 7f, 0.3f);
                post.GetComponent<Renderer>().sharedMaterial = Look.HoopA;
            }
            var beam = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Object.Destroy(beam.GetComponent<Collider>());
            beam.transform.SetParent(root.transform, false);
            beam.transform.localPosition = new Vector3(0, 7.0f, 0.25f);
            beam.transform.localScale = new Vector3(5.7f, 0.5f, 0.3f);
            beam.GetComponent<Renderer>().sharedMaterial = Look.HoopA;
        }

        void CrateTower(float d)
        {
            var pp = path[IndexAtDist(d)];
            var rot = Quaternion.LookRotation(pp.fwd, Vector3.up);
            Vector3 right = Vector3.Cross(Vector3.up, pp.fwd);
            const float s = 0.85f;
            int k = 0;
            for (int row = 0; row < 3; row++)
            for (int y = 0; y < 5 - row; y++)
            for (int x = -1; x <= 1; x++)
            {
                var c = GameObject.CreatePrimitive(PrimitiveType.Cube);
                c.name = "Crate";
                c.transform.SetParent(transform, false);
                c.transform.SetPositionAndRotation(pp.pos + pp.fwd * (row * s * 1.02f) + right * (x * s * 1.02f) + Vector3.up * (s * 0.5f + y * s * 1.001f), rot);
                c.transform.localScale = Vector3.one * s;
                c.GetComponent<Renderer>().sharedMaterial = Look.Crates[k++ % Look.Crates.Length];
                var rb = c.AddComponent<Rigidbody>();
                rb.mass = 0.25f;
                rb.Sleep();
            }
        }

        void Scenery()
        {
            var floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.name = "Cushion";
            Object.Destroy(floor.GetComponent<Collider>());
            floor.transform.SetParent(transform, false);
            Vector3 mid = path[path.Count / 2].pos;
            floor.transform.position = new Vector3(mid.x, -24f, mid.z);
            floor.transform.localScale = new Vector3(120, 1, 120);
            var fr = floor.GetComponent<Renderer>();
            fr.sharedMaterial = Look.Floor;
            Look.Tile(fr, new Vector2(260, 260));
            fr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            // tall chevron towers and loose floating pieces in the distance
            var rnd = new System.Random(7);
            for (int i = 0; i < 14; i++)
            {
                var pp = path[rnd.Next(path.Count)];
                Vector3 side = Vector3.Cross(Vector3.up, pp.fwd) * ((rnd.Next(2) == 0 ? -1 : 1) * (28f + (float)rnd.NextDouble() * 40f));
                Vector3 pos = pp.pos + side + pp.fwd * ((float)rnd.NextDouble() * 30f);
                if (MinDistToPath(pos) < 18f) continue;
                if (i % 3 == 0)
                {
                    var tower = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    tower.name = "Tower";
                    Object.Destroy(tower.GetComponent<Collider>());
                    tower.transform.SetParent(transform, false);
                    float h = 14f + (float)rnd.NextDouble() * 14f;
                    tower.transform.position = pos + Vector3.up * (h * 0.5f - 6f);
                    tower.transform.rotation = Quaternion.LookRotation(-side, Vector3.up);
                    tower.transform.localScale = new Vector3(2.4f, h, 0.6f);
                    var tr = tower.GetComponent<Renderer>();
                    tr.sharedMaterial = Look.Pillar;
                    Look.Tile(tr, new Vector2(1, h / 2.4f));
                }
                else
                {
                    var slab = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    slab.name = "Island";
                    Object.Destroy(slab.GetComponent<Collider>());
                    slab.transform.SetParent(transform, false);
                    slab.transform.position = pos + Vector3.up * ((float)rnd.NextDouble() * 10f - 6f);
                    slab.transform.rotation = Quaternion.Euler(0, (float)rnd.NextDouble() * 180f, 0);
                    slab.transform.localScale = new Vector3(3f + (float)rnd.NextDouble() * 4f, 0.8f, 10f + (float)rnd.NextDouble() * 16f);
                    slab.GetComponent<Renderer>().sharedMaterial = (i % 2 == 0) ? Look.WoodSide : Look.AsphaltSide;
                }
            }
        }

        float MinDistToPath(Vector3 p)
        {
            float best = float.MaxValue;
            for (int i = 0; i < path.Count; i += 8)
            {
                var d = path[i].pos - p; d.y = 0;
                best = Mathf.Min(best, d.magnitude);
            }
            return best;
        }

        // Nearest path index, searched around the last known index.
        public int Nearest(Vector3 p, int hint)
        {
            int best = hint;
            float bd = float.MaxValue;
            int from = Mathf.Max(0, hint - 30), to = Mathf.Min(path.Count - 1, hint + 60);
            for (int i = from; i <= to; i++)
            {
                float d = (path[i].pos - p).sqrMagnitude;
                if (d < bd) { bd = d; best = i; }
            }
            return best;
        }
    }
}
