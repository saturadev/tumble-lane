using UnityEngine;

namespace TumbleLane
{
    // All art is made here from code: procedural textures and plain Standard materials.
    public static class Look
    {
        public static Material Wood, WoodSide, Asphalt, AsphaltSide, Chevron, Hazard, Floor, Ball, Leaf, Stem,
            Coin, HoopA, HoopB, Pillar, Trail, TrailCool, Checkpoint, Spark, BlobShadow;
        public static Material[] Crates;

        static bool built;

        public static void Build()
        {
            if (built) return;
            built = true;

            Wood = Mat(new Color(0.97f, 0.88f, 0.70f), WoodTex(), 0.15f);
            WoodSide = Mat(new Color(0.78f, 0.55f, 0.36f), null, 0.1f);
            Asphalt = Mat(Color.white, AsphaltTex(), 0.2f);
            AsphaltSide = Mat(new Color(0.98f, 0.86f, 0.10f), null, 0.25f);
            Chevron = Mat(Color.white, ChevronTex(), 0.3f);
            Hazard = Mat(Color.white, StripeTex(), 0.35f);
            Floor = new Material(Shader.Find("Unlit/Texture")) { mainTexture = QuiltTex() };
            // Pip: deep saturated orange with a warm rim light and a dark outline (PipBall shader),
            // so it stands out from the cream wood track at the size the final video shows the game.
            var pipShader = Resources.Load<Shader>("PipBall");
            if (pipShader == null) pipShader = Shader.Find("TumbleLane/PipBall");
            if (pipShader != null && pipShader.isSupported)
            {
                Ball = new Material(pipShader) { mainTexture = DimpleTex() };
                Ball.color = new Color(1.0f, 0.45f, 0.04f);
            }
            else Ball = Mat(new Color(1.0f, 0.45f, 0.04f), DimpleTex(), 0.55f);
            Leaf = Mat(new Color(0.25f, 0.68f, 0.22f), null, 0.4f);
            Stem = Mat(new Color(0.42f, 0.28f, 0.14f), null, 0.1f);
            Coin = Mat(new Color(1.0f, 0.80f, 0.15f), null, 0.8f);
            Coin.SetFloat("_Metallic", 0.6f);
            Coin.EnableKeyword("_EMISSION");
            Coin.SetColor("_EmissionColor", new Color(0.35f, 0.24f, 0.0f));
            HoopA = Mat(new Color(1.0f, 0.30f, 0.55f), null, 0.6f);
            HoopA.EnableKeyword("_EMISSION");
            HoopA.SetColor("_EmissionColor", new Color(0.35f, 0.05f, 0.15f));
            HoopB = Mat(new Color(1f, 1f, 1f), null, 0.6f);
            Pillar = Mat(Color.white, ChevronTex(), 0.2f);
            Checkpoint = Mat(new Color(0.35f, 0.85f, 1.0f), null, 0.5f);
            Checkpoint.EnableKeyword("_EMISSION");
            Checkpoint.SetColor("_EmissionColor", new Color(0.05f, 0.25f, 0.35f));
            Crates = new[]
            {
                Mat(Color.white, CrateTex(new Color(1f, 0.45f, 0.55f)), 0.3f),
                Mat(Color.white, CrateTex(new Color(0.40f, 0.75f, 1f)), 0.3f),
                Mat(Color.white, CrateTex(new Color(0.55f, 0.85f, 0.35f)), 0.3f),
                Mat(Color.white, CrateTex(new Color(1f, 0.75f, 0.20f)), 0.3f),
            };

            var sprite = Shader.Find("Sprites/Default");
            Trail = new Material(sprite);
            TrailCool = new Material(sprite);
            Spark = new Material(sprite);
            BlobShadow = new Material(sprite) { mainTexture = SoftBlob() };
            BlobShadow.renderQueue = 2990; // after the opaque track, before the trails
        }

        static Material Mat(Color c, Texture2D tex, float smooth)
        {
            var m = new Material(Shader.Find("Standard"));
            m.color = c;
            if (tex != null) m.mainTexture = tex;
            m.SetFloat("_Glossiness", smooth);
            return m;
        }

        static Texture2D NewTex(int w, int h, bool repeat = true)
        {
            var t = new Texture2D(w, h, TextureFormat.RGBA32, true);
            t.wrapMode = repeat ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
            t.filterMode = FilterMode.Trilinear;
            t.anisoLevel = 8;
            return t;
        }

        // Light wood planks with darker brown edges along u = 0 and u = 1.
        static Texture2D WoodTex()
        {
            int w = 128, h = 256;
            var t = NewTex(w, h);
            var baseC = new Color(1.0f, 0.93f, 0.78f);
            var grain = new Color(0.93f, 0.82f, 0.62f);
            var edge = new Color(0.80f, 0.58f, 0.38f);
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float u = x / (float)w;
                float g = Mathf.PerlinNoise(u * 40f, y / (float)h * 3f);
                var c = Color.Lerp(baseC, grain, Mathf.SmoothStep(0.45f, 0.75f, g) * 0.6f);
                float e = Mathf.Min(u, 1f - u);
                if (e < 0.06f) c = Color.Lerp(edge, c, e / 0.06f * 0.4f);
                t.SetPixel(x, y, c);
            }
            t.Apply();
            return t;
        }

        // Dark asphalt, yellow borders, yellow dashes down the middle.
        static Texture2D AsphaltTex()
        {
            int w = 128, h = 128;
            var t = NewTex(w, h);
            var yel = new Color(1f, 0.87f, 0.10f);
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float u = x / (float)w, v = y / (float)h;
                float n = Mathf.PerlinNoise(x * 0.3f, y * 0.3f) * 0.05f;
                var c = new Color(0.27f + n, 0.28f + n, 0.30f + n);
                float e = Mathf.Min(u, 1f - u);
                if (e < 0.07f) c = yel;
                bool dash = (Mathf.Abs(u - 0.33f) < 0.012f || Mathf.Abs(u - 0.67f) < 0.012f) && v > 0.1f && v < 0.45f;
                if (dash) c = yel;
                t.SetPixel(x, y, c);
            }
            t.Apply();
            return t;
        }

        // Black strip with yellow chevrons pointing toward +v.
        static Texture2D ChevronTex()
        {
            int w = 128, h = 128;
            var t = NewTex(w, h);
            var yel = new Color(1f, 0.85f, 0.08f);
            var blk = new Color(0.10f, 0.10f, 0.12f);
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float u = x / (float)w, v = y / (float)h;
                float e = Mathf.Min(u, 1f - u);
                float k = v - Mathf.Abs(u - 0.5f) * 0.9f; // chevron
                k = k - Mathf.Floor(k);
                var c = k < 0.42f ? yel : blk;
                if (e < 0.06f) c = yel;
                t.SetPixel(x, y, c);
            }
            t.Apply();
            return t;
        }

        static Texture2D StripeTex()
        {
            int w = 64, h = 64;
            var t = NewTex(w, h);
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float k = (x + y) / (float)w;
                k -= Mathf.Floor(k);
                t.SetPixel(x, y, k < 0.5f ? new Color(1f, 0.85f, 0.08f) : new Color(0.12f, 0.12f, 0.14f));
            }
            t.Apply();
            return t;
        }

        // Soft quilted cushion far below the course.
        static Texture2D QuiltTex()
        {
            int s = 64;
            var t = NewTex(s, s);
            for (int y = 0; y < s; y++)
            for (int x = 0; x < s; x++)
            {
                float u = x / (float)s - 0.5f, v = y / (float)s - 0.5f;
                float d = Mathf.Sqrt(u * u + v * v) * 2f;
                float shade = Mathf.Lerp(1.0f, 0.78f, Mathf.SmoothStep(0.35f, 1.0f, d));
                float lit = Mathf.Clamp01(0.5f - (u + v) * 0.6f) * 0.08f;
                var c = new Color(0.98f, 0.94f, 0.84f) * (shade + lit);
                c.a = 1f;
                t.SetPixel(x, y, c);
            }
            t.Apply();
            return t;
        }

        static Texture2D DimpleTex()
        {
            int s = 128;
            var t = NewTex(s, s);
            for (int y = 0; y < s; y++)
            for (int x = 0; x < s; x++)
            {
                float n = Mathf.PerlinNoise(x * 0.35f, y * 0.35f);
                float b = 1f - Mathf.SmoothStep(0.55f, 0.8f, n) * 0.12f;
                t.SetPixel(x, y, new Color(b, b * 0.97f, b * 0.95f));
            }
            t.Apply();
            return t;
        }

        static Texture2D CrateTex(Color c)
        {
            int s = 64;
            var t = NewTex(s, s, false);
            for (int y = 0; y < s; y++)
            for (int x = 0; x < s; x++)
            {
                float u = x / (float)s, v = y / (float)s;
                float e = Mathf.Min(Mathf.Min(u, 1f - u), Mathf.Min(v, 1f - v));
                var col = e < 0.09f ? Color.white : c;
                float dx = u - 0.5f, dy = v - 0.5f;
                if (dx * dx + dy * dy < 0.018f) col = Color.white;
                t.SetPixel(x, y, col);
            }
            t.Apply();
            return t;
        }

        public static Texture2D Vignette()
        {
            int s = 256;
            var t = NewTex(s, s, false);
            for (int y = 0; y < s; y++)
            for (int x = 0; x < s; x++)
            {
                float u = x / (float)s - 0.5f, v = y / (float)s - 0.5f;
                float d = Mathf.Sqrt(u * u * 1.6f + v * v) * 2f;
                float a = Mathf.SmoothStep(0.8f, 1.4f, d) * 0.38f;
                t.SetPixel(x, y, new Color(0.10f, 0.08f, 0.04f, a));
            }
            t.Apply();
            return t;
        }

        // Soft dark contact shadow under the ball.
        static Texture2D SoftBlob()
        {
            int s = 64;
            var t = NewTex(s, s, false);
            for (int y = 0; y < s; y++)
            for (int x = 0; x < s; x++)
            {
                float u = (x + 0.5f) / s - 0.5f, v = (y + 0.5f) / s - 0.5f;
                float d = Mathf.Sqrt(u * u + v * v) * 2f;
                float a = 1f - Mathf.SmoothStep(0.15f, 1f, d);
                t.SetPixel(x, y, new Color(0.16f, 0.08f, 0.03f, a * a));
            }
            t.Apply();
            return t;
        }

        public static Texture2D Disc(Color c)
        {
            int s = 64;
            var t = NewTex(s, s, false);
            for (int y = 0; y < s; y++)
            for (int x = 0; x < s; x++)
            {
                float u = x / (float)s - 0.5f, v = y / (float)s - 0.5f;
                float d = Mathf.Sqrt(u * u + v * v) * 2f;
                var col = c;
                col.a = 1f - Mathf.SmoothStep(0.85f, 1f, d);
                if (d < 0.6f) col = Color.Lerp(c, Color.white, 0.25f);
                t.SetPixel(x, y, col);
            }
            t.Apply();
            return t;
        }

        static Texture2D SkyTex(Color haze, Color blue)
        {
            int h = 256;
            var t = NewTex(4, h, false);
            t.wrapMode = TextureWrapMode.Clamp;
            for (int y = 0; y < h; y++)
            {
                float v = y / (float)(h - 1);
                float k = Mathf.Pow(Mathf.Clamp01((v - 0.5f) / 0.2f), 0.7f);
                var c = Color.Lerp(haze, blue, k);
                for (int x = 0; x < 4; x++) t.SetPixel(x, y, c);
            }
            t.Apply();
            return t;
        }

        static Texture2D Flat(Color c)
        {
            var t = NewTex(4, 4, false);
            for (int y = 0; y < 4; y++) for (int x = 0; x < 4; x++) t.SetPixel(x, y, c);
            t.Apply();
            return t;
        }

        public static void Tile(Renderer r, Vector2 scale)
        {
            var b = new MaterialPropertyBlock();
            r.GetPropertyBlock(b);
            b.SetVector("_MainTex_ST", new Vector4(scale.x, scale.y, 0, 0));
            r.SetPropertyBlock(b);
        }

        public static void Environment(Light sun)
        {
            // pale blue overhead fading to a warm cream haze at the horizon
            var haze = new Color(0.95f, 0.91f, 0.80f);
            var blue = new Color(0.36f, 0.64f, 0.96f);
            var side = SkyTex(haze, blue);
            var sky = new Material(Shader.Find("Skybox/6 Sided"));
            sky.SetTexture("_FrontTex", side); sky.SetTexture("_BackTex", side);
            sky.SetTexture("_LeftTex", side); sky.SetTexture("_RightTex", side);
            sky.SetTexture("_UpTex", Flat(blue));
            sky.SetTexture("_DownTex", Flat(haze));
            sky.SetColor("_Tint", new Color(0.5f, 0.5f, 0.5f));
            sky.SetFloat("_Exposure", 1f);
            RenderSettings.skybox = sky;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = haze;
            RenderSettings.fogStartDistance = 60f;
            RenderSettings.fogEndDistance = 330f;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.56f, 0.60f, 0.68f);
            RenderSettings.ambientEquatorColor = new Color(0.58f, 0.55f, 0.50f);
            RenderSettings.ambientGroundColor = new Color(0.38f, 0.34f, 0.29f);
            if (sun != null)
            {
                sun.type = LightType.Directional;
                sun.color = new Color(1f, 0.96f, 0.88f);
                sun.intensity = 0.8f;
                sun.shadows = LightShadows.Soft;
                sun.shadowStrength = 0.55f;
                sun.transform.rotation = Quaternion.Euler(55f, -35f, 0f);
                RenderSettings.sun = sun;
            }
            QualitySettings.shadowDistance = 70f;
        }
    }
}
