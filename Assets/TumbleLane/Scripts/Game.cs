using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
#if UNITY_EDITOR && SATURA_SPACE
using SaturaSpace;
#endif

namespace TumbleLane
{
    // Tumble Lane: roll Pip the clementine down a floating course. One slip and you drop.
    public class Game : MonoBehaviour
    {
        public static Game I;

        public enum State { Ready, Running, Falling, Finished }
        public State state;
        public int coins, hoops, falls, score;
        public float runTime;
        public float launchSpeed = 17f;

        Course course;
        Ball ball;
        ChaseCamera cam;
        int checkpoint;
        float shownScore;
        float fallAt, finishAt;

        TMP_FontAsset font;
        TMP_Text scoreText, coinText, centerText, hintText;
        RectTransform barFill;
        Canvas canvas;
        readonly List<(TMP_Text t, float born, Vector3 start)> popups = new List<(TMP_Text, float, Vector3)>();

        void Awake()
        {
            I = this;
            Time.fixedDeltaTime = 1f / 60f;
            Application.targetFrameRate = 60;
            font = Resources.Load<TMP_FontAsset>("LilitaOne SDF");
            Look.Build();
            var sun = FindFirstObjectByType<Light>();
            if (sun == null) sun = new GameObject("Sun").AddComponent<Light>();
            Look.Environment(sun);

            var c = Camera.main;
            if (c == null) { c = new GameObject("Main Camera").AddComponent<Camera>(); c.tag = "MainCamera"; }
            c.fieldOfView = 62f;
            c.nearClipPlane = 0.1f;
            c.farClipPlane = 600f;
            c.clearFlags = CameraClearFlags.Skybox;
            if (!c.TryGetComponent(out cam)) cam = c.gameObject.AddComponent<ChaseCamera>();

            BuildHud();
            NewRun(false);
        }

        public void NewRun(bool bot)
        {
            if (course != null) Destroy(course.gameObject);
            if (ball != null) { Destroy(ball.gameObject); }
            foreach (var t in GameObject.FindObjectsByType<Transform>(FindObjectsSortMode.None))
                if (t != null && t.name == "PipTrail") Destroy(t.gameObject);

            course = new GameObject("Course").AddComponent<Course>();
            course.Build(launchSpeed);
            ball = Ball.Create(course);
            ball.bot = bot;
            var start = course.checkpoints[0];
            ball.Place(start.pos + Vector3.up * (Course.BallRadius + 0.02f), start.fwd, start.index, 0f);
            cam.ball = ball;
            cam.frozen = false;
            cam.Snap();

            coins = hoops = falls = score = 0;
            shownScore = 0;
            runTime = 0;
            checkpoint = 0;
            state = State.Ready;
            centerText.text = "";
            hintText.text = bot ? "" : "DRAG TO STEER";
            ball.rolling = false;
        }

        public void Go()
        {
            ball.rolling = true;
        }

        void Update()
        {
            if (ball == null) return;
            switch (state)
            {
                case State.Ready:
                    if (ball.rolling) { state = State.Running; hintText.text = ""; }
                    break;
                case State.Running:
                    runTime += Time.deltaTime;
                    for (int i = course.checkpoints.Count - 1; i > checkpoint; i--)
                        if (ball.pathIndex >= course.checkpoints[i].index) { checkpoint = i; break; }
                    if (ball.Fallen)
                    {
                        state = State.Falling;
                        falls++;
                        fallAt = Time.time;
                        cam.frozen = true;
                        centerText.text = "OOPS!";
                        Log($"fell at {course.path[ball.pathIndex].dist:F1} m");
                    }
                    else if (course.path[ball.pathIndex].dist >= course.finishDist)
                    {
                        state = State.Finished;
                        finishAt = Time.time;
                        score += 100;
                        centerText.text = "LANE CLEAR!";
                        Confetti(ball.transform.position + ball.Heading * 6f + Vector3.up * 2f);
                        Log($"finished in {runTime:F1} s, coins {coins}, hoops {hoops}, falls {falls}, score {score}");
                    }
                    break;
                case State.Falling:
                    if (Time.time - fallAt > 1.2f)
                    {
                        var cp = course.checkpoints[checkpoint];
                        ball.Place(cp.pos + Vector3.up * (Course.BallRadius + 0.05f), cp.fwd, cp.index, 5f);
                        cam.frozen = false;
                        cam.Snap();
                        centerText.text = "";
                        state = State.Running;
                    }
                    break;
                case State.Finished:
                    if (Time.time - finishAt > 1.6f) ball.braking = true;
                    break;
            }
            UpdateHud();
        }

        // ---------- events ----------

        public void OnCoin(Vector3 at)
        {
            coins++;
            score += 10;
            Popup("+10", at, new Color(1f, 0.9f, 0.3f));
        }

        public void OnHoop(Vector3 at)
        {
            hoops++;
            score += 20;
            Popup("GOOD! +20", at + Vector3.up * 1.6f, new Color(1f, 0.55f, 0.8f), 1.35f);
            Log($"hoop {hoops} at {Time.time:F2}");
        }

        void Log(string s)
        {
#if UNITY_EDITOR && SATURA_SPACE
            SaturaLog.Log("lane", s);
#endif
            Debug.Log("[TumbleLane] " + s);
        }

        // ---------- HUD ----------

        void BuildHud()
        {
            var go = new GameObject("HUD");
            canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.matchWidthOrHeight = 0.5f;

            scoreText = MakeText(go.transform, "Score", 96, TextAlignmentOptions.Top, new Vector2(0.5f, 1), new Vector2(0, -60), new Vector2(600, 130));
            // progress bar
            var barBg = new GameObject("Bar").AddComponent<Image>();
            barBg.transform.SetParent(go.transform, false);
            barBg.color = new Color(0, 0, 0, 0.25f);
            var rb = barBg.rectTransform;
            rb.anchorMin = rb.anchorMax = new Vector2(0.5f, 1);
            rb.pivot = new Vector2(0.5f, 1);
            rb.anchoredPosition = new Vector2(0, -40);
            rb.sizeDelta = new Vector2(520, 16);
            var fill = new GameObject("Fill").AddComponent<Image>();
            fill.transform.SetParent(barBg.transform, false);
            fill.color = new Color(1f, 0.55f, 0.12f);
            barFill = fill.rectTransform;
            barFill.anchorMin = new Vector2(0, 0);
            barFill.anchorMax = new Vector2(0, 1);
            barFill.pivot = new Vector2(0, 0.5f);
            barFill.anchoredPosition = Vector2.zero;
            barFill.sizeDelta = new Vector2(0, 0);

            var coinIcon = new GameObject("CoinIcon").AddComponent<RawImage>();
            coinIcon.transform.SetParent(go.transform, false);
            coinIcon.texture = Look.Disc(new Color(1f, 0.78f, 0.12f));
            var ci = coinIcon.rectTransform;
            ci.anchorMin = ci.anchorMax = new Vector2(1, 1);
            ci.pivot = new Vector2(1, 1);
            ci.anchoredPosition = new Vector2(-40, -60);
            ci.sizeDelta = new Vector2(70, 70);
            coinText = MakeText(go.transform, "Coins", 64, TextAlignmentOptions.TopRight, new Vector2(1, 1), new Vector2(-125, -60), new Vector2(300, 90));

            centerText = MakeText(go.transform, "Center", 120, TextAlignmentOptions.Center, new Vector2(0.5f, 0.5f), new Vector2(0, 380), new Vector2(1000, 200));
            hintText = MakeText(go.transform, "Hint", 60, TextAlignmentOptions.Center, new Vector2(0.5f, 0), new Vector2(0, 260), new Vector2(1000, 120));
        }

        TMP_Text MakeText(Transform parent, string name, int size, TextAlignmentOptions anchor, Vector2 anchorPt, Vector2 pos, Vector2 box)
        {
            var t = new GameObject(name).AddComponent<TextMeshProUGUI>();
            t.transform.SetParent(parent, false);
            t.font = font;
            t.fontSharedMaterial = font.material; // outline and underlay live on the font material, inside its padding
            t.fontSize = size;
            t.alignment = anchor;
            t.color = Color.white;
            t.raycastTarget = false;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            t.overflowMode = TextOverflowModes.Overflow;
            var r = t.rectTransform;
            r.anchorMin = r.anchorMax = anchorPt;
            r.pivot = anchorPt;
            r.anchoredPosition = pos;
            r.sizeDelta = box;
            return t;
        }

        static void Stretch(RectTransform r)
        {
            r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one;
            r.offsetMin = r.offsetMax = Vector2.zero;
        }

        void Popup(string text, Vector3 world, Color c, float scale = 1f)
        {
            var t = MakeText(canvas.transform, "Popup", Mathf.RoundToInt(70 * scale), TextAlignmentOptions.Center, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(700, 140));
            t.text = text;
            t.color = c;
            popups.Add((t, Time.time, world));
        }

        void UpdateHud()
        {
            shownScore = Mathf.MoveTowards(shownScore, score, Time.deltaTime * 140f);
            scoreText.text = Mathf.RoundToInt(shownScore).ToString();
            coinText.text = coins.ToString();
            barFill.sizeDelta = new Vector2(520f * Mathf.Clamp01(ball.Progress), 0);
            var camera = Camera.main;
            for (int i = popups.Count - 1; i >= 0; i--)
            {
                var (t, born, start) = popups[i];
                float age = Time.time - born;
                if (age > 0.65f) { Destroy(t.gameObject); popups.RemoveAt(i); continue; }
                Vector3 sp = camera.WorldToScreenPoint(start + Vector3.up * (0.6f + age * 2.2f));
                t.transform.position = sp;
                var col = t.color; col.a = 1f - Mathf.SmoothStep(0.35f, 0.65f, age); t.color = col;
                t.transform.localScale = Vector3.one * (1f + Mathf.Max(0, 0.25f - age) * 1.6f);
            }
            if (state == State.Finished || state == State.Falling)
            {
                float k = Mathf.Min(1f, (Time.time - (state == State.Finished ? finishAt : fallAt)) * 5f);
                centerText.transform.localScale = Vector3.one * Mathf.Lerp(1.6f, 1f, k);
            }
        }

        void Confetti(Vector3 at)
        {
            var go = new GameObject("Confetti");
            go.transform.SetParent(course.transform, false);
            go.transform.position = at;
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.loop = false;
            main.duration = 0.5f;
            main.startLifetime = 2.2f;
            main.startSpeed = new ParticleSystem.MinMaxCurve(6f, 13f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.12f, 0.28f);
            main.gravityModifier = 1.1f;
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.4f, 0.6f), new Color(0.3f, 0.8f, 1f));
            main.startRotation = new ParticleSystem.MinMaxCurve(0, Mathf.PI * 2);
            var em = ps.emission;
            em.rateOverTime = 0;
            em.SetBursts(new[] { new ParticleSystem.Burst(0, 160) });
            var sh = ps.shape;
            sh.shapeType = ParticleSystemShapeType.Hemisphere;
            sh.radius = 1.2f;
            var rend = go.GetComponent<ParticleSystemRenderer>();
            rend.sharedMaterial = Look.Spark;
            ps.Play();
        }

        public string Describe()
        {
            if (ball == null) return "no run";
            var pp = course.path[ball.pathIndex];
            return $"state={state} dist={pp.dist:F1}/{course.length:F0} finishAt={course.finishDist:F0} speed={ball.Speed:F1} grounded={ball.grounded} coins={coins} hoops={hoops} falls={falls} score={score} time={runTime:F1} pos={ball.transform.position}";
        }

#if UNITY_EDITOR && SATURA_SPACE
        [SaturaHandle("Builds a fresh course and waits for the player: drag or A/D to steer, the first press starts the roll")]
        public static string StartRun()
        {
            I.NewRun(false);
            return I.Describe();
        }

        [SaturaHandle("Builds a fresh course and lets the bot play it")]
        public static string DemoRun()
        {
            I.NewRun(true);
            I.Go();
            return I.Describe();
        }

        [SaturaHandle("Run state: distance, speed, coins, hoops, falls, score")]
        public static string GetState() => I.Describe();

        [SaturaHandle("Waits until the run finishes or the time runs out. Yields the state.")]
        public static IEnumerator WaitForFinish(float seconds)
        {
            float end = Time.time + seconds;
            while (Time.time < end && I.state != State.Finished) yield return null;
            yield return I.Describe();
        }

        [SaturaHandle("Puts the ball at a distance along the course, rolling, for framing checks")]
        public static string JumpTo(float metres, bool bot)
        {
            I.ball.bot = bot;
            int idx = I.course.IndexAtDist(metres);
            var pp = I.course.path[idx];
            I.ball.Place(pp.pos + Vector3.up * (Course.BallRadius + 0.05f), pp.fwd, idx, I.ball.cruise);
            I.ball.rolling = true;
            I.state = State.Running;
            I.cam.Snap();
            I.hintText.text = "";
            return I.Describe();
        }
#endif
    }
}
