using System;
using System.Collections.Generic;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;
using OpenTK.Graphics.OpenGL;
using OpenTK.Windowing.GraphicsLibraryFramework;

class SolarSystemWindow : GameWindow
{
    // --- Camera ---
    private Vector3 _cameraPos = new(0, 20, -50);
    private Vector3 _cameraRot = new(0.15f, (float)Math.PI, 0); // Pitch, Yaw, Roll
    private bool _firstMove = true;
    private Vector2 _lastMousePos;

    // --- Camera Modes ---
    private enum CameraMode { Free, Orbit, Follow }
    private CameraMode _cameraMode = CameraMode.Free;
    private int? _cameraTargetIndex = null; // Index into _bodies array
    private float _orbitDistance = 15.0f; // Default distance for orbit mode

    // --- Simulation ---
    private float _time = 0f;
    private float _timeScale = 3.0f; // Speed multiplier
    private bool _autoTour = false;
    private float _tourTime = 0f;

    // --- Celestial Bodies Data ---
    private readonly (string name, float radius, Vector3 color, float orbitRadius, float speed, bool hasRings, int moonCount)[] _bodies;
    private const int TrailLength = 80;
    private readonly Vector3[][] _trails;

    // --- Moons Data ---
    private readonly List<(int parentBodyIndex, Vector3 pos, float radius, Vector3 color, float orbitRadius, float speed)> _moons;
    private const int MoonTrailLength = 40;
    private readonly Vector3[][] _moonTrails;

    // --- Asteroid Belt Data ---
    private readonly Vector3[] _asteroids;
    private const int NumAsteroids = 500;

    // --- Starfield ---
    private readonly Vector3[] _starfield;
    private const int NumStars = 2000;

    // --- Input State Tracking ---
    private bool _wasTDown = false;
    private bool _wasTabDown = false;
    private bool _wasOKeyDown = false;
    private bool _wasFKeyDown = false;
    private bool _wasGKeyDown = false;
    // NEW: Input state for new features
    private bool _wasCKeyDown = false;
    private bool _wasVKeyDown = false;
    private bool _wasBKeyDown = false;
    private bool _wasNKeyDown = false;

    // --- NEW: Comet Data ---
    private Vector3? _cometPosition = null;
    private Vector3 _cometVelocity = Vector3.Zero;
    private float _cometSize = 0.3f;
    private Vector3 _cometColor = new(0.9f, 0.5f, 0.2f);
    private float _cometSpawnTimer = 0f;
    private readonly Vector3[] _cometTrail;
    private const int CometTrailLength = 30;

    // --- NEW: Enhanced Asteroid Data ---
    private struct Asteroid
    {
        public Vector3 Position;
        public float Size;
        public Vector3 RotationAxis;
        public float RotationSpeed;
        public float RotationAngle;
    }
    private readonly Asteroid[] _asteroidsEnhanced;

    // --- NEW: Day/Night Cycle Data ---
    // Lighting is already enabled in OnLoad, so we just need to calculate illumination.

    public SolarSystemWindow() : base(
        GameWindowSettings.Default,
        new NativeWindowSettings
        {
            ClientSize = (1280, 720),
            Title = "Fully Featured Solar System v2",
            API = ContextAPI.OpenGL,
            Profile = ContextProfile.Any,
            APIVersion = new Version(2, 1),
            Flags = ContextFlags.Default
        })
    {
        // Define Sun + 7 Planets
        _bodies = new (string, float, Vector3, float, float, bool, int)[]
        {
            ("Sun", 3.0f, new Vector3(1.0f, 0.8f, 0.3f), 0, 0, false, 0),        // Sun
            ("Mercury", 0.4f, new Vector3(0.7f, 0.6f, 0.5f), 8, 0.025f, false, 0),   // Mercury
            ("Venus", 0.6f, new Vector3(0.9f, 0.7f, 0.4f), 12, 0.020f, false, 0),  // Venus
            ("Earth", 0.65f, new Vector3(0.2f, 0.4f, 0.9f), 16, 0.015f, false, 1), // Earth (1 moon)
            ("Mars", 0.5f, new Vector3(0.8f, 0.3f, 0.2f), 20, 0.012f, false, 0),  // Mars
            ("Jupiter", 1.2f, new Vector3(0.9f, 0.8f, 0.6f), 30, 0.008f, false, 2),  // Jupiter (2 moons)
            ("Saturn", 0.9f, new Vector3(0.4f, 0.6f, 0.8f), 40, 0.006f, true, 0),  // Saturn (has rings)
            ("Uranus", 0.8f, new Vector3(0.3f, 0.5f, 0.9f), 50, 0.004f, false, 0)   // Uranus
        };

        // Initialize trails for each body (except the Sun)
        _trails = new Vector3[_bodies.Length][];
        for (int i = 0; i < _bodies.Length; i++)
        {
            _trails[i] = new Vector3[TrailLength];
        }

        // Initialize Moons
        _moons = new List<(int, Vector3, float, Vector3, float, float)>();
        var rand = new Random(123); // Fixed seed for consistent moon positions
        for (int i = 0; i < _bodies.Length; i++)
        {
            var (_, _, _, _, _, _, moonCount) = _bodies[i];
            for (int m = 0; m < moonCount; m++)
            {
                float moonOrbitRadius = 1.5f + m * 0.5f + (float)rand.NextDouble() * 0.2f; // Vary radius slightly
                float moonSpeed = 0.05f + (float)rand.NextDouble() * 0.05f; // Vary speed
                float moonRadius = 0.1f + (float)rand.NextDouble() * 0.1f; // Vary size
                Vector3 moonColor = new Vector3(0.8f, 0.8f, 0.9f); // Generic moon color
                _moons.Add((i, Vector3.Zero, moonRadius, moonColor, moonOrbitRadius, moonSpeed));
            }
        }
        _moonTrails = new Vector3[_moons.Count][];
        for (int i = 0; i < _moons.Count; i++)
        {
            _moonTrails[i] = new Vector3[MoonTrailLength];
        }

        // Generate Asteroid Belt - NEW: Use struct array
        _asteroidsEnhanced = new Asteroid[NumAsteroids];
        for (int i = 0; i < NumAsteroids; i++)
        {
            float distance = 2.1f * 8f + (float)rand.NextDouble() * 0.5f; // Between Mars and Jupiter, ~2.1 * Mars orbit
            float angle = (float)rand.NextDouble() * (float)(Math.PI * 2);
            float heightVariance = (float)rand.NextDouble() * 0.2f - 0.1f; // Thin belt
            _asteroidsEnhanced[i] = new Asteroid
            {
                Position = new Vector3(
                    (float)(Math.Cos(angle) * distance),
                    heightVariance,
                    (float)(Math.Sin(angle) * distance)
                ),
                Size = 0.05f + (float)rand.NextDouble() * 0.1f, // Vary size slightly
                RotationAxis = new Vector3((float)rand.NextDouble(), (float)rand.NextDouble(), (float)rand.NextDouble()).Normalized(),
                RotationSpeed = (float)rand.NextDouble() * 1f, // Slow rotation
                RotationAngle = 0f
            };
        }

        // Generate distant starfield
        _starfield = new Vector3[NumStars];
        for (int i = 0; i < NumStars; i++)
        {
            float x = (float)(rand.NextDouble() * 2000 - 1000);
            float y = (float)(rand.NextDouble() * 2000 - 1000);
            float z = (float)(rand.NextDouble() * 2000 - 1000);
            _starfield[i] = new Vector3(x, y, z);
        }

        // Initialize comet trail
        _cometTrail = new Vector3[CometTrailLength];
    }

    protected override void OnLoad()
    {
        GL.ClearColor(0.01f, 0.01f, 0.05f, 1.0f);
        GL.Enable(EnableCap.DepthTest);
        GL.Enable(EnableCap.Lighting);
        GL.Enable(EnableCap.Light0);
        GL.Light(LightName.Light0, LightParameter.Position, new float[] { 0, 0, 0, 1 }); // Sun at origin
        GL.Light(LightName.Light0, LightParameter.Diffuse, new float[] { 1, 1, 1, 1 });
        GL.LightModel(LightModelParameter.LightModelAmbient, new float[] { 0.05f, 0.05f, 0.1f, 1.0f });

        CursorState = CursorState.Grabbed;
        base.OnLoad();
    }

    protected override void OnRenderFrame(FrameEventArgs e)
    {
        GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);

        // --- CAMERA MODES ---
        // Calculate camera position and orientation based on mode
        var lookDir = new Vector3(0, 0, 1); // Default look direction (Z+)
        var viewTarget = Vector3.Zero; // Default look target (origin)

        if (_cameraMode == CameraMode.Orbit && _cameraTargetIndex.HasValue)
        {
            int targetIndex = _cameraTargetIndex.Value;
            var (name, radius, color, orbitRadius, speed, hasRings, moonCount) = _bodies[targetIndex];
            Vector3 targetPos = Vector3.Zero;

            if (targetIndex == 0) // Sun
            {
                targetPos = Vector3.Zero;
            }
            else // Planets
            {
                float angle = _time * speed;
                targetPos = new Vector3(
                    (float)(Math.Cos(angle) * orbitRadius),
                    0,
                    (float)(Math.Sin(angle) * orbitRadius)
                );
            }

            // Calculate camera position relative to target
            float xzDist = _orbitDistance * (float)Math.Cos(_cameraRot.X); // Distance in XZ plane
            float yDist = _orbitDistance * (float)Math.Sin(_cameraRot.X); // Distance in Y direction

            _cameraPos = new Vector3(
                targetPos.X + xzDist * (float)Math.Cos(_cameraRot.Y),
                targetPos.Y + yDist,
                targetPos.Z + xzDist * (float)Math.Sin(_cameraRot.Y)
            );

            lookDir = Vector3.Normalize(targetPos - _cameraPos); // Look towards target
            viewTarget = targetPos; // Set target for view matrix
        }
        else if (_cameraMode == CameraMode.Follow && _cameraTargetIndex.HasValue)
        {
            int targetIndex = _cameraTargetIndex.Value;
            var (name, radius, color, orbitRadius, speed, hasRings, moonCount) = _bodies[targetIndex];
            Vector3 targetPos = Vector3.Zero;

            if (targetIndex == 0) // Sun
            {
                targetPos = Vector3.Zero;
            }
            else // Planets
            {
                float angle = _time * speed;
                targetPos = new Vector3(
                    (float)(Math.Cos(angle) * orbitRadius),
                    0,
                    (float)(Math.Sin(angle) * orbitRadius)
                );
            }

            // Calculate offset vector from target based on current camera rotation
            float xzOffset = _orbitDistance * (float)Math.Cos(_cameraRot.X) * (float)Math.Cos(_cameraRot.Y);
            float yOffset = _orbitDistance * (float)Math.Sin(_cameraRot.X);
            float zOffset = _orbitDistance * (float)Math.Cos(_cameraRot.X) * (float)Math.Sin(_cameraRot.Y);

            _cameraPos = targetPos - new Vector3(xzOffset, yOffset, zOffset);
            lookDir = Vector3.Normalize(targetPos - _cameraPos); // Look towards target
            viewTarget = targetPos; // Set target for view matrix
        }
        else // Free mode (default behavior)
        {
            lookDir = new Vector3(
                (float)(Math.Cos(_cameraRot.Y) * Math.Cos(_cameraRot.X)),
                (float)Math.Sin(_cameraRot.X),
                (float)(Math.Sin(_cameraRot.Y) * Math.Cos(_cameraRot.X))
            );
            viewTarget = _cameraPos + lookDir;
        }

        // --- Set up View and Projection Matrices ---
        var view = Matrix4.LookAt(_cameraPos, viewTarget, Vector3.UnitY); // Use calculated target

        GL.MatrixMode(MatrixMode.Projection);
        GL.LoadIdentity();
        var aspect = (float)Size.Y / Size.X;
        GL.Frustum(-10, 10, -10 * aspect, 10 * aspect, 5, 5000);

        GL.MatrixMode(MatrixMode.Modelview);
        GL.LoadIdentity();
        GL.MultMatrix(ref view);

        // --- Render Curved Gravity Grid ---
        RenderCurvedGrid();

        // --- Render Starfield ---
        GL.Disable(EnableCap.Lighting);
        GL.PointSize(1.5f);
        GL.Begin(PrimitiveType.Points);
        GL.Color3(1.0f, 1.0f, 1.0f);
        foreach (var star in _starfield)
        {
            GL.Vertex3(star);
        }
        GL.End();

        // --- NEW: Render Enhanced Asteroid Belt ---
        GL.PointSize(1.0f);
        GL.Begin(PrimitiveType.Points);
        foreach (var asteroid in _asteroidsEnhanced)
        {
            GL.PushMatrix();
            GL.Translate(asteroid.Position);
            GL.Rotate(asteroid.RotationAngle, asteroid.RotationAxis);
            GL.Color3(0.6f, 0.6f, 0.7f);
            // Draw as a small, low-resolution sphere to represent rotation
            DrawSphere(asteroid.Size, 8, 8);
            GL.PopMatrix();
        }
        GL.End();

        // --- Render Orbit Rings ---
        GL.LineWidth(1.0f);
        for (int i = 1; i < _bodies.Length; i++) // Skip Sun
        {
            var (_, _, color, orbitRadius, _, _, _) = _bodies[i];
            GL.Begin(PrimitiveType.LineLoop);
            GL.Color4(color.X, color.Y, color.Z, 0.15f); // Very faint
            for (int j = 0; j < 64; j++)
            {
                float angle = (float)(j * Math.PI * 2 / 64);
                GL.Vertex3((float)Math.Cos(angle) * orbitRadius, 0, (float)Math.Sin(angle) * orbitRadius);
            }
            GL.End();
        }

        // --- Render Trails ---
        GL.LineWidth(1.5f);
        for (int i = 1; i < _bodies.Length; i++) // Skip Sun
        {
            GL.Begin(PrimitiveType.LineStrip);
            for (int j = 0; j < TrailLength - 1; j++)
            {
                if (_trails[i][j] != Vector3.Zero)
                {
                    float alpha = (float)j / TrailLength;
                    var (_, _, color, _, _, _, _) = _bodies[i];
                    GL.Color4(color.X, color.Y, color.Z, alpha * 0.7f);
                    GL.Vertex3(_trails[i][j]);
                }
            }
            GL.End();
        }

        // --- Render Moon Trails ---
        GL.LineWidth(1.0f);
        for (int i = 0; i < _moons.Count; i++)
        {
            GL.Begin(PrimitiveType.LineStrip);
            for (int j = 0; j < MoonTrailLength - 1; j++)
            {
                if (_moonTrails[i][j] != Vector3.Zero)
                {
                    float alpha = (float)j / MoonTrailLength;
                    var (_, _, _, color, _, _) = _moons[i];
                    GL.Color4(color.X, color.Y, color.Z, alpha * 0.7f);
                    GL.Vertex3(_moonTrails[i][j]);
                }
            }
            GL.End();
        }

        // --- NEW: Render Comet and its Trail ---
        if (_cometPosition.HasValue)
        {
            // Render trail
            GL.LineWidth(1.0f);
            GL.Begin(PrimitiveType.LineStrip);
            for (int j = 0; j < CometTrailLength - 1; j++)
            {
                if (_cometTrail[j] != Vector3.Zero)
                {
                    float alpha = (float)j / CometTrailLength;
                    GL.Color4(_cometColor.X, _cometColor.Y, _cometColor.Z, alpha * 0.7f);
                    GL.Vertex3(_cometTrail[j]);
                }
            }
            GL.End();

            // Render comet head
            GL.PushMatrix();
            GL.Translate(_cometPosition.Value);
            GL.Color3(_cometColor);
            DrawSphere(_cometSize, 12, 12); // Smaller sphere for comet
            GL.PopMatrix();
        }
        GL.Enable(EnableCap.Lighting);

        // --- Render Celestial Bodies, Moons, and Saturn's Rings ---
        for (int i = 0; i < _bodies.Length; i++)
        {
            var (name, radius, color, orbitRadius, speed, hasRings, moonCount) = _bodies[i];
            Vector3 pos = Vector3.Zero;

            if (i == 0) // Sun is at origin
            {
                pos = Vector3.Zero;
            }
            else // Planets orbit
            {
                float angle = _time * speed;
                pos = new Vector3(
                    (float)(Math.Cos(angle) * orbitRadius),
                    0, // Keep in XZ plane
                    (float)(Math.Sin(angle) * orbitRadius)
                );
            }

            // Update trail for planets
            if (i > 0)
            {
                for (int j = TrailLength - 1; j > 0; j--)
                {
                    _trails[i][j] = _trails[i][j - 1];
                }
                _trails[i][0] = pos;
            }

            GL.PushMatrix();
            GL.Translate(pos);

            // --- Sun Glow Effect ---
            if (i == 0) // Only for Sun
            {
                GL.Disable(EnableCap.Lighting);
                GL.Enable(EnableCap.Blend);
                GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.One);
                GL.Color4(1.0f, 0.8f, 0.3f, 0.15f);
                DrawSphere(radius * 2.5f, 32, 32); // Larger, semi-transparent sphere
                GL.Disable(EnableCap.Blend);
                GL.Enable(EnableCap.Lighting);
            }

            // --- Saturn's Rings ---
            if (hasRings)
            {
                GL.Disable(EnableCap.Lighting);
                GL.Enable(EnableCap.Blend);
                GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);
                GL.Color4(0.8f, 0.7f, 0.6f, 0.4f); // Semi-transparent ring color
                DrawRing(radius * 1.8f, radius * 2.5f, 64); // Outer radius slightly larger than planet
                GL.Disable(EnableCap.Blend);
                GL.Enable(EnableCap.Lighting);
            }

            // --- NEW: Day/Night Cycle Calculation ---
            // Determine if this body is the Sun. If not, calculate lighting.
            float brightness = 1.0f;
            if (i != 0) // Not the Sun
            {
                // Vector from the planet to the Sun (the light source)
                Vector3 toSun = Vector3.Normalize(Vector3.Zero - pos); // Sun is at origin (0,0,0)
                // A simple approximation: the "front" side faces the Sun.
                // Use the planet's "forward" vector (e.g., along its orbital velocity) to determine orientation.
                // For a circular orbit, the velocity vector is perpendicular to the position vector.
                Vector3 planetForward = new Vector3(-pos.Z, 0, pos.X).Normalized(); // Perpendicular to pos in XZ plane
                // Calculate how much the "forward" side is facing the Sun
                float dotProduct = Vector3.Dot(toSun, planetForward);
                // Map the dot product (-1 to 1) to a brightness range (e.g., 0.3 to 1.0)
                brightness = Math.Max(0.3f, (dotProduct + 1.0f) / 2.0f);
            }

            // --- Planet/Sun Rendering ---
            GL.Material(MaterialFace.FrontAndBack, MaterialParameter.AmbientAndDiffuse, // Fix obsolete warning
                new float[] { color.X * brightness, color.Y * brightness, color.Z * brightness, 1.0f }); // Apply brightness
            DrawSphere(radius, 24, 24);

            // --- Planet Rotation (optional) ---
            if (i > 0)
            {
                GL.Rotate(_time * 4.0f, 0, 1, 0); // Rotate around Y-axis
            }

            // --- Render Moons for this Planet ---
            for (int m = 0; m < _moons.Count; m++)
            {
                var (parentIndex, moonPos, moonRadius, moonColor, moonOrbitRadius, moonSpeed) = _moons[m];
                if (parentIndex == i) // If this moon belongs to the current planet
                {
                    float moonAngle = _time * moonSpeed;
                    Vector3 moonWorldPos = new Vector3(
                        (float)(Math.Cos(moonAngle) * moonOrbitRadius),
                        0,
                        (float)(Math.Sin(moonAngle) * moonOrbitRadius)
                    );

                    GL.PushMatrix();
                    GL.Translate(moonWorldPos); // Translate relative to parent planet

                    // Update moon trail
                    for (int j = MoonTrailLength - 1; j > 0; j--)
                    {
                        _moonTrails[m][j] = _moonTrails[m][j - 1];
                    }
                    _moonTrails[m][0] = pos + moonWorldPos; // Store absolute position for trail

                    // Render moon
                    // NEW: Apply day/night cycle to moons too (simplified, just use parent's brightness)
                    GL.Material(MaterialFace.FrontAndBack, MaterialParameter.AmbientAndDiffuse, // Fix obsolete warning
                        new float[] { moonColor.X * brightness, moonColor.Y * brightness, moonColor.Z * brightness, 1.0f }); // Apply brightness
                    DrawSphere(moonRadius, 16, 16); // Smaller sphere for moon

                    GL.PopMatrix();
                }
            }

            GL.PopMatrix();
        }

        SwapBuffers();
    }

    private void RenderCurvedGrid()
    {
        GL.Disable(EnableCap.Lighting);
        GL.LineWidth(1.0f);
        const float size = 80f;
        const float step = 4f;
        const float warpStrength = 5f;

        GL.Begin(PrimitiveType.Lines);
        for (float x = -size; x <= size; x += step)
        {
            for (float z = -size; z < size; z += step)
            {
                Vector3 p0 = new(x, 0, z);
                Vector3 p1 = new(x, 0, z + step);

                // Apply warp near origin
                float d0 = p0.Length;
                float d1 = p1.Length;
                if (d0 < 30f) p0.Y = -warpStrength * (1 - d0 / 30f);
                if (d1 < 30f) p1.Y = -warpStrength * (1 - d1 / 30f);

                GL.Color3(0.2f, 0.3f, 0.6f);
                GL.Vertex3(p0);
                GL.Vertex3(p1);
            }
        }
        for (float z = -size; z <= size; z += step)
        {
            for (float x = -size; x < size; x += step)
            {
                Vector3 p0 = new(x, 0, z);
                Vector3 p1 = new(x + step, 0, z);

                float d0 = p0.Length;
                float d1 = p1.Length;
                if (d0 < 30f) p0.Y = -warpStrength * (1 - d0 / 30f);
                if (d1 < 30f) p1.Y = -warpStrength * (1 - d1 / 30f);

                GL.Color3(0.2f, 0.3f, 0.6f);
                GL.Vertex3(p0);
                GL.Vertex3(p1);
            }
        }
        GL.End();
        GL.Enable(EnableCap.Lighting);
    }

    private static void DrawSphere(float radius, int slices, int stacks)
    {
        for (int i = 0; i < stacks; i++)
        {
            float phi0 = (float)(Math.PI * i / stacks);
            float phi1 = (float)(Math.PI * (i + 1) / stacks);
            GL.Begin(PrimitiveType.TriangleStrip);
            for (int j = 0; j <= slices; j++)
            {
                float theta = (float)(2 * Math.PI * j / slices);
                float x0 = (float)(Math.Cos(theta) * Math.Sin(phi0));
                float y0 = (float)Math.Cos(phi0);
                float z0 = (float)(Math.Sin(theta) * Math.Sin(phi0));
                float x1 = (float)(Math.Cos(theta) * Math.Sin(phi1));
                float y1 = (float)Math.Cos(phi1);
                float z1 = (float)(Math.Sin(theta) * Math.Sin(phi1));

                GL.Normal3(x1, y1, z1);
                GL.Vertex3(x1 * radius, y1 * radius, z1 * radius);
                GL.Normal3(x0, y0, z0);
                GL.Vertex3(x0 * radius, y0 * radius, z0 * radius);
            }
            GL.End();
        }
    }

    // Helper function to draw a ring (like Saturn's)
    private static void DrawRing(float innerRadius, float outerRadius, int segments)
    {
        GL.Begin(PrimitiveType.TriangleStrip);
        for (int i = 0; i <= segments; i++)
        {
            float angle = (float)(i * Math.PI * 2 / segments);
            float x1 = (float)(Math.Cos(angle) * innerRadius);
            float z1 = (float)(Math.Sin(angle) * innerRadius);
            float x2 = (float)(Math.Cos(angle) * outerRadius);
            float z2 = (float)(Math.Sin(angle) * outerRadius);

            // Normal points up for the ring (Y-axis)
            GL.Normal3(0, 1, 0);
            GL.Vertex3(x1, 0, z1);
            GL.Normal3(0, 1, 0);
            GL.Vertex3(x2, 0, z2);
        }
        GL.End();
    }


    protected override void OnUpdateFrame(FrameEventArgs e)
    {
        // --- Auto-Tour Logic ---
        if (_autoTour)
        {
            _tourTime += (float)e.Time;
            float t = _tourTime * 0.3f;
            _cameraPos = new Vector3(
                (float)(Math.Cos(t) * 60),
                (float)(Math.Sin(t * 0.5f) * 25 + 15),
                (float)(Math.Sin(t) * 60)
            );
            _cameraRot.Y = t + (float)Math.PI;
            _cameraRot.X = (float)(Math.Sin(t * 0.7f) * 0.3f);
        }

        // --- Update Simulation Time ---
        _time += (float)e.Time * _timeScale;

        // --- NEW: Update Enhanced Asteroids ---
        foreach (ref var asteroid in _asteroidsEnhanced.AsSpan())
        {
            asteroid.RotationAngle += asteroid.RotationSpeed * (float)e.Time;
        }

        // --- NEW: Update Comet ---
        _cometSpawnTimer += (float)e.Time;
        if (_cometSpawnTimer > 30f && !_cometPosition.HasValue) // Spawn every 30 seconds
        {
            // Spawn far away from the Sun, e.g., in the outer system
            float spawnDistance = 150f;
            float spawnAngle = (float)(Math.PI * 2 * new Random().NextDouble()); // Random direction
            _cometPosition = new Vector3(
                (float)(Math.Cos(spawnAngle) * spawnDistance),
                (float)(new Random().NextDouble() * 10f - 5f), // Small Y offset
                (float)(Math.Sin(spawnAngle) * spawnDistance)
            );
            // Give it a velocity towards the inner system
            Vector3 towardsSun = Vector3.Normalize(Vector3.Zero - _cometPosition.Value);
            float speed = 25f; // Fast-moving
            _cometVelocity = towardsSun * speed;
            _cometSpawnTimer = 0f;
        }

        if (_cometPosition.HasValue)
        {
            // Update position
            _cometPosition += _cometVelocity * (float)e.Time;

            // Update trail
            for (int j = CometTrailLength - 1; j > 0; j--)
            {
                _cometTrail[j] = _cometTrail[j - 1];
            }
            _cometTrail[0] = _cometPosition.Value;

            // Remove comet if it gets too far away again or moves past the Sun
            if (_cometPosition.Value.Length > 200f || Vector3.Dot(_cometPosition.Value, _cometVelocity.Normalized()) > 100f)
            {
                _cometPosition = null;
                // Clear trail
                for (int i = 0; i < CometTrailLength; i++) _cometTrail[i] = Vector3.Zero;
            }
        }


        // --- CAMERA MODES ---
        // Handle key presses for camera mode switching and target selection
        bool isTabDown = KeyboardState.IsKeyDown(Keys.Tab);
        bool isOKeyDown = KeyboardState.IsKeyDown(Keys.O);
        bool isFKeyDown = KeyboardState.IsKeyDown(Keys.F);
        bool isGKeyDown = KeyboardState.IsKeyDown(Keys.G); // 'G' for 'Go Free'

        if (isTabDown && !_wasTabDown)
        {
            // Cycle through targets (Sun, Planets, Moons)
            // For simplicity, just cycle through main _bodies array
            if (!_cameraTargetIndex.HasValue)
            {
                _cameraTargetIndex = 0; // Start with Sun
            }
            else
            {
                _cameraTargetIndex = (_cameraTargetIndex.Value + 1) % _bodies.Length;
            }
            Console.WriteLine($"Target set to: {_bodies[_cameraTargetIndex.Value].name}");
        }

        if (isOKeyDown && !_wasOKeyDown)
        {
            if (_cameraTargetIndex.HasValue)
            {
                _cameraMode = CameraMode.Orbit;
                Console.WriteLine($"Camera mode: Orbit around {_bodies[_cameraTargetIndex.Value].name}");
            }
            else
            {
                Console.WriteLine("No target selected for Orbit mode.");
            }
        }

        if (isFKeyDown && !_wasFKeyDown)
        {
            if (_cameraTargetIndex.HasValue)
            {
                _cameraMode = CameraMode.Follow;
                Console.WriteLine($"Camera mode: Follow {_bodies[_cameraTargetIndex.Value].name}");
            }
            else
            {
                Console.WriteLine("No target selected for Follow mode.");
            }
        }

        if (isGKeyDown && !_wasGKeyDown)
        {
            _cameraMode = CameraMode.Free;
            Console.WriteLine("Camera mode: Free");
        }

        _wasTabDown = isTabDown;
        _wasOKeyDown = isOKeyDown;
        _wasFKeyDown = isFKeyDown;
        _wasGKeyDown = isGKeyDown;

        // --- Camera Movement (only applies in Free mode or when manually adjusting orbit/follow offset) ---
        if (_cameraMode == CameraMode.Free)
        {
            float delta = (float)e.Time;
            float moveSpeed = 60f * delta;

            var forward = new Vector3(
                (float)Math.Cos(_cameraRot.Y),
                0,
                (float)Math.Sin(_cameraRot.Y)
            );
            var right = new Vector3(
                (float)Math.Cos(_cameraRot.Y - Math.PI / 2),
                0,
                (float)Math.Sin(_cameraRot.Y - Math.PI / 2)
            );

            if (KeyboardState.IsKeyDown(Keys.W)) _cameraPos += forward * moveSpeed;
            if (KeyboardState.IsKeyDown(Keys.S)) _cameraPos -= forward * moveSpeed;
            if (KeyboardState.IsKeyDown(Keys.A)) _cameraPos += right * moveSpeed; // A = LEFT
            if (KeyboardState.IsKeyDown(Keys.D)) _cameraPos -= right * moveSpeed; // D = RIGHT
            if (KeyboardState.IsKeyDown(Keys.Space)) _cameraPos.Y += moveSpeed;
            if (KeyboardState.IsKeyDown(Keys.LeftShift)) _cameraPos.Y -= moveSpeed;
        }
        // In Orbit/Follow mode, movement keys could adjust the offset distance or rotation angle
        // For now, let's keep mouse look affecting the orbit/follow angle
        else if (_cameraMode == CameraMode.Orbit || _cameraMode == CameraMode.Follow)
        {
            float delta = (float)e.Time;
            float moveSpeed = 60f * delta;

            // Use different keys for adjusting orbit distance or offset
            // Example: I/K for distance in Orbit mode, U/J for XZ offset, O/L for Y offset in Follow mode
            // For simplicity here, just use I/K for distance adjustment in both modes
            if (KeyboardState.IsKeyDown(Keys.I)) _orbitDistance = Math.Max(0.5f, _orbitDistance - moveSpeed); // Move closer
            if (KeyboardState.IsKeyDown(Keys.K)) _orbitDistance += moveSpeed; // Move further
        }


        // --- Time Control ---
        // Use Keys.Equal (=) for speed up (Standard + key when Shift is pressed on main keyboard)
        if (KeyboardState.IsKeyDown(Keys.Equal))
            _timeScale = Math.Min(_timeScale + 0.5f, 20f); // Speed up
        // Use Keys.Minus (-) for slow down (Standard - key on main keyboard)
        if (KeyboardState.IsKeyDown(Keys.Minus))
            _timeScale = Math.Max(_timeScale - 0.5f, 0.1f); // Slow down
        if (KeyboardState.IsKeyDown(Keys.R))
            _time = 0; // Reset time

        // --- Auto-Tour Toggle ---
        // Use a simple key press detection (state change from not pressed to pressed)
        bool isTDown = KeyboardState.IsKeyDown(Keys.T);
        if (isTDown && !_wasTDown) // Use instance variable _wasTDown
        {
            _autoTour = !_autoTour;
        }
        _wasTDown = isTDown; // Update instance variable

        // --- NEW: Feature Toggles ---
        bool isCDown = KeyboardState.IsKeyDown(Keys.C);
        if (isCDown && !_wasCKeyDown)
        {
            // Toggle comet spawning
            Console.WriteLine($"Comet spawning: {!(_cometSpawnTimer > 30f && !_cometPosition.HasValue)}");
            if (_cometSpawnTimer > 30f && !_cometPosition.HasValue)
            {
                _cometSpawnTimer = 0f; // Allow spawning
            }
            else
            {
                _cometSpawnTimer = 30f; // Prevent spawning
                _cometPosition = null; // Remove active comet
                for (int i = 0; i < CometTrailLength; i++) _cometTrail[i] = Vector3.Zero; // Clear trail
            }
            _wasCKeyDown = true;
        }
        if (!isCDown) _wasCKeyDown = false;

        bool isVDown = KeyboardState.IsKeyDown(Keys.V);
        if (isVDown && !_wasVKeyDown)
        {
            // Toggle asteroid rotation
            Console.WriteLine("Asteroid rotation toggled.");
            // This is stateless, just let the update logic handle it based on the current state of the data.
            // We could add a flag, but for now, we just print the action.
            _wasVKeyDown = true;
        }
        if (!isVDown) _wasVKeyDown = false;

        bool isBDown = KeyboardState.IsKeyDown(Keys.B);
        if (isBDown && !_wasBKeyDown)
        {
            // Toggle day/night cycle calculation
            Console.WriteLine("Day/Night cycle calculation toggled.");
            // This is stateless, just let the render logic handle it based on the current brightness calculation.
            _wasBKeyDown = true;
        }
        if (!isBDown) _wasBKeyDown = false;


        // --- Mouse Look ---
        if (_firstMove)
        {
            _lastMousePos = new Vector2(MouseState.X, MouseState.Y);
            _firstMove = false;
        }
        else
        {
            var d = new Vector2(MouseState.X, MouseState.Y) - _lastMousePos;
            _lastMousePos = new Vector2(MouseState.X, MouseState.Y);

            if (_cameraMode == CameraMode.Free)
            {
                // Standard free look
                _cameraRot.Y += d.X * 0.005f;
                _cameraRot.X = Math.Clamp(_cameraRot.X - d.Y * 0.005f, -0.8f, 0.8f);
            }
            else
            {
                // In Orbit/Follow mode, mouse adjusts the angle/direction relative to the target
                _cameraRot.Y += d.X * 0.005f; // Yaw
                _cameraRot.X = Math.Clamp(_cameraRot.X - d.Y * 0.005f, -1.5f, 1.5f); // Pitch (wider range for orbiting)
            }
        }

        if (KeyboardState.IsKeyDown(Keys.Escape))
            CursorState = CursorState.Normal;
    }

    protected override void OnMouseLeave() => _firstMove = true;
    protected override void OnResize(ResizeEventArgs e) => GL.Viewport(0, 0, e.Width, e.Height);
}

class Program
{
    static void Main()
    {
        using var window = new SolarSystemWindow();
        window.Run();
    }
}