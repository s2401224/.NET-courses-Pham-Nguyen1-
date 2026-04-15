using Raylib_cs;
using System.Drawing;
using System.Numerics;
using System.Text.Json;
using Color = Raylib_cs.Color;

namespace Asteroids
{
    // Game states
    enum GameState { Menu, Playing, GameOver }

    // Data saved to JSON file
    class HighScoreData
    {
        public int HighScore { get; set; } = 0;
        public string PlayerName { get; set; } = "AAA";
    }

    // Handles movement and screen wrapping
    public class Transform
    {
        public Vector2 position;
        public Vector2 velocity;
        public float maxSpeed;
        public Vector2 direction;
        public float rotationRadians;

        public Transform(Vector2 position, float maxSpeed)
        {
            this.position = position;
            this.maxSpeed = maxSpeed;
            this.velocity = Vector2.Zero;
            this.direction = new Vector2(0f, -1f); // default direction: up
        }

        public void Move()
        {
            float dt = Raylib.GetFrameTime();

            // Cap velocity to maxSpeed
            if (velocity.Length() > maxSpeed)
                velocity = Vector2.Normalize(velocity) * maxSpeed;

            position += velocity * dt;

            // Wrap around screen edges
            int sw = Raylib.GetScreenWidth();
            int sh = Raylib.GetScreenHeight();
            if (position.X < 0) position.X += sw;
            else if (position.X >= sw) position.X -= sw;
            if (position.Y < 0) position.Y += sh;
            else if (position.Y >= sh) position.Y -= sh;
        }

        public void Turn(float amountRadians)
        {
            rotationRadians += amountRadians;
            direction = Vector2.Transform(direction, Matrix3x2.CreateRotation(amountRadians));
        }

        public void AddForceToDirection(float force)
        {
            // Push ship forward in the direction it faces
            velocity += direction * force * Raylib.GetFrameTime();
        }
    }

    // Stores collision radius and checks circle vs circle hits
    public class Collision
    {
        public float radius;

        public Collision(float radius) { this.radius = radius; }

        public static bool CheckCollision(Transform tA, Collision cA, Transform tB, Collision cB)
        {
            return Raylib.CheckCollisionCircles(tA.position, cA.radius, tB.position, cB.radius);
        }
    }

    // Bullet: flies straight, disappears after 2 seconds
    public class Bullet
    {
        public Transform transform;
        public Collision collision;
        public bool active = true;
        private float lifetime = 2.0f;

        public Bullet(Vector2 position, Vector2 velocity)
        {
            transform = new Transform(position, 600f);
            transform.velocity = velocity;
            collision = new Collision(4f);
        }

        public void Update()
        {
            transform.Move();
            lifetime -= Raylib.GetFrameTime();
            if (lifetime <= 0f) active = false;
        }

        public void Draw(Color color)
        {
            Raylib.DrawCircleV(transform.position, 4f, color);
        }
    }

    public enum AsteroidSize { Large = 3, Medium = 2, Small = 1 }

    // Asteroid: random jagged polygon that slowly spins as it drifts
    public class Asteroid
    {
        public Transform transform;
        public Collision collision;
        public AsteroidSize size;
        private Vector2[] points; // polygon vertices

        public Asteroid(Vector2 position, AsteroidSize size, Vector2? overrideVelocity = null)
        {
            this.size = size;
            float radius = size == AsteroidSize.Large ? 40f : size == AsteroidSize.Medium ? 24f : 13f;
            float speed = size == AsteroidSize.Large ? 60f : size == AsteroidSize.Medium ? 110f : 180f;

            transform = new Transform(position, speed * 2f);
            collision = new Collision(radius);

            // Random offsets give each asteroid a rocky look
            Random rng = new Random();
            int n = 10;
            points = new Vector2[n];
            for (int i = 0; i < n; i++)
            {
                float angle = i / (float)n * MathF.Tau;
                float r = radius * (0.7f + rng.NextSingle() * 0.5f);
                points[i] = new Vector2(MathF.Cos(angle) * r, MathF.Sin(angle) * r);
            }

            if (overrideVelocity.HasValue)
                transform.velocity = overrideVelocity.Value;
            else
            {
                float angle2 = rng.NextSingle() * MathF.Tau;
                transform.velocity = new Vector2(MathF.Cos(angle2), MathF.Sin(angle2)) * speed;
            }
        }

        public void Update()
        {
            transform.Move();
            transform.rotationRadians += 0.01f; // slow spin each frame
        }

        public void Draw()
        {
            Vector2 pos = transform.position;
            float rot = transform.rotationRadians;
            for (int i = 0; i < points.Length; i++)
            {
                Vector2 a = Rotate(points[i], rot) + pos;
                Vector2 b = Rotate(points[(i + 1) % points.Length], rot) + pos;
                Raylib.DrawLineV(a, b, Color.LightGray);
            }
        }

        private Vector2 Rotate(Vector2 v, float angle)
        {
            float cos = MathF.Cos(angle);
            float sin = MathF.Sin(angle);
            return new Vector2(v.X * cos - v.Y * sin, v.X * sin + v.Y * cos);
        }
    }

    // Player ship: rotates, thrusts, shoots
    public class Ship
    {
        public Transform transform;
        public Collision collision;
        public List<Bullet> bullets = new();
        public float invincibleTimer = 0f; // invincibility timer after respawn

        private float shootInterval = 0.25f;
        private float lastShootTime;
        private float bulletSpeed = 500f;

        public Ship(Vector2 position)
        {
            transform = new Transform(position, 350f);
            collision = new Collision(16f);
            lastShootTime = -shootInterval; // allow shooting immediately at start
        }

        public void Reset(Vector2 position)
        {
            transform.position = position;
            transform.velocity = Vector2.Zero;
            transform.rotationRadians = 0f;
            transform.direction = new Vector2(0f, -1f);
            bullets.Clear();
            invincibleTimer = 2.5f; // 2.5 seconds of invincibility after respawn
        }

        public void Update()
        {
            if (invincibleTimer > 0f) invincibleTimer -= Raylib.GetFrameTime();

            // Rotate left or right
            if (Raylib.IsKeyDown(KeyboardKey.Left) || Raylib.IsKeyDown(KeyboardKey.A))
                transform.Turn(-2.5f * Raylib.GetFrameTime());
            if (Raylib.IsKeyDown(KeyboardKey.Right) || Raylib.IsKeyDown(KeyboardKey.D))
                transform.Turn(2.5f * Raylib.GetFrameTime());

            // Thrust forward
            if (Raylib.IsKeyDown(KeyboardKey.Up) || Raylib.IsKeyDown(KeyboardKey.W))
                transform.AddForceToDirection(220f);

            transform.Move();

            // Shoot, limited by shootInterval cooldown
            bool wantShoot = Raylib.IsKeyDown(KeyboardKey.Space) || Raylib.IsKeyDown(KeyboardKey.Z);
            if (wantShoot && (float)Raylib.GetTime() - lastShootTime > shootInterval)
            {
                lastShootTime = (float)Raylib.GetTime();
                Vector2 vel = transform.direction * bulletSpeed + transform.velocity * 0.3f;
                bullets.Add(new Bullet(transform.position, vel));
            }

            // Remove expired bullets
            for (int i = bullets.Count - 1; i >= 0; i--)
            {
                bullets[i].Update();
                if (!bullets[i].active) bullets.RemoveAt(i);
            }
        }

        public void Draw()
        {
            // Blink while invincible
            if (invincibleTimer > 0f && (int)(invincibleTimer * 8) % 2 == 0) return;

            Vector2 pos = transform.position;
            float rot = transform.rotationRadians;

            // Draw ship as a triangle
            Vector2 tip = pos + Rotate(new Vector2(0, -18), rot);
            Vector2 left = pos + Rotate(new Vector2(-11, 12), rot);
            Vector2 right = pos + Rotate(new Vector2(11, 12), rot);
            Vector2 mid = pos + Rotate(new Vector2(0, 6), rot);

            Raylib.DrawLineV(tip, left, Color.SkyBlue);
            Raylib.DrawLineV(tip, right, Color.SkyBlue);
            Raylib.DrawLineV(left, mid, Color.SkyBlue);
            Raylib.DrawLineV(right, mid, Color.SkyBlue);

            // Engine flame when thrusting
            if (Raylib.IsKeyDown(KeyboardKey.Up) || Raylib.IsKeyDown(KeyboardKey.W))
            {
                Vector2 fl = pos + Rotate(new Vector2(-6, 12), rot);
                Vector2 fr = pos + Rotate(new Vector2(6, 12), rot);
                Vector2 ft = pos + Rotate(new Vector2(0, 24), rot);
                Raylib.DrawLineV(fl, ft, Color.Orange);
                Raylib.DrawLineV(fr, ft, Color.Yellow);
            }

            foreach (var b in bullets) b.Draw(Color.Yellow);
        }

        public bool IsInvincible() => invincibleTimer > 0f;

        private Vector2 Rotate(Vector2 v, float angle)
        {
            float cos = MathF.Cos(angle);
            float sin = MathF.Sin(angle);
            return new Vector2(v.X * cos - v.Y * sin, v.X * sin + v.Y * cos);
        }
    }

    // Enemy: drifts randomly and shoots in random directions
    public class Enemy
    {
        public Transform transform;
        public Collision collision;
        public List<Bullet> bullets = new();

        private float shootInterval;
        private float lastShootTime;
        private Random rng = new Random();

        public Enemy(Vector2 position)
        {
            transform = new Transform(position, 150f);
            collision = new Collision(18f);

            // Random drift direction and speed
            float angle = rng.NextSingle() * MathF.Tau;
            transform.velocity = new Vector2(MathF.Cos(angle), MathF.Sin(angle))
                               * (60f + rng.NextSingle() * 80f);

            shootInterval = 1.5f + rng.NextSingle() * 2f;
            lastShootTime = (float)Raylib.GetTime();
        }

        public void Update()
        {
            transform.Move();

            // Shoot at random intervals in a random direction
            if ((float)Raylib.GetTime() - lastShootTime > shootInterval)
            {
                lastShootTime = (float)Raylib.GetTime();
                shootInterval = 1.5f + rng.NextSingle() * 2f;
                float angle = rng.NextSingle() * MathF.Tau;
                Vector2 vel = new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * 200f;
                bullets.Add(new Bullet(transform.position, vel));
            }

            for (int i = bullets.Count - 1; i >= 0; i--)
            {
                bullets[i].Update();
                if (!bullets[i].active) bullets.RemoveAt(i);
            }
        }

        public void Draw()
        {
            Vector2 pos = transform.position;
            // Draw enemy as a red diamond
            Raylib.DrawLineV(pos + new Vector2(0, -20), pos + new Vector2(20, 0), Color.Red);
            Raylib.DrawLineV(pos + new Vector2(20, 0), pos + new Vector2(0, 20), Color.Red);
            Raylib.DrawLineV(pos + new Vector2(0, 20), pos + new Vector2(-20, 0), Color.Red);
            Raylib.DrawLineV(pos + new Vector2(-20, 0), pos + new Vector2(0, -20), Color.Red);
            Raylib.DrawLineV(pos + new Vector2(-20, 0), pos + new Vector2(20, 0), Color.DarkGray);

            foreach (var b in bullets) b.Draw(Color.Orange);
        }
    }

    internal class Program
    {
        static int screenW = 900;
        static int screenH = 700;

        static Ship player = null!;
        static List<Asteroid> asteroids = new();
        static List<Enemy> enemies = new();

        static int score = 0;
        static int lives = 3;
        static int level = 1;
        static int highScore = 0;

        static GameState state = GameState.Menu;
        static Random rng = new Random();

        // File where high score is saved
        static string saveFile = "highscore.json";

        static void Main(string[] args)
        {
            Raylib.InitWindow(screenW, screenH, "Asteroids");
            Raylib.SetTargetFPS(60);

            LoadHighScore(); // read saved high score on startup

            while (!Raylib.WindowShouldClose())
            {
                Update();
                Draw();
            }

            Raylib.CloseWindow();
        }

        // Load high score from JSON on startup
        static void LoadHighScore()
        {
            if (!File.Exists(saveFile)) return;
            try
            {
                string json = File.ReadAllText(saveFile);
                var data = JsonSerializer.Deserialize<HighScoreData>(json);
                if (data != null) highScore = data.HighScore;
            }
            catch { } // corrupted file to just start fresh
        }

        // Write high score to JSON file
        static void SaveHighScore()
        {
            var data = new HighScoreData { HighScore = highScore };
            string json = JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(saveFile, json);
        }

        static void StartGame()
        {
            score = 0; lives = 3; level = 1;
            state = GameState.Playing;
            player = new Ship(new Vector2(screenW / 2f, screenH / 2f));
            SpawnLevel(level);
        }

        static void SpawnLevel(int lvl)
        {
            asteroids.Clear();
            enemies.Clear();

            // More asteroids each level
            for (int i = 0; i < 3 + lvl; i++)
                asteroids.Add(new Asteroid(RandomPosAwayFromPlayer(), AsteroidSize.Large));

            // Enemies appear from level 2 onward
            for (int i = 0; i < lvl / 2; i++)
            {
                Vector2 pos = rng.Next(2) == 0
                    ? new Vector2(rng.Next(screenW), rng.Next(2) == 0 ? 0 : screenH)
                    : new Vector2(rng.Next(2) == 0 ? 0 : screenW, rng.Next(screenH));
                enemies.Add(new Enemy(pos));
            }
        }

        // Pick a random spot at least 150px away from the player
        static Vector2 RandomPosAwayFromPlayer()
        {
            Vector2 center = player?.transform.position ?? new Vector2(screenW / 2f, screenH / 2f);
            Vector2 pos;
            do { pos = new Vector2(rng.Next(screenW), rng.Next(screenH)); }
            while (Vector2.Distance(pos, center) < 150f);
            return pos;
        }

        static void Update()
        {
            if (state == GameState.Menu)
            {
                // Enter starts the game from the menu
                if (Raylib.IsKeyPressed(KeyboardKey.Enter))
                    StartGame();
                return;
            }

            if (state == GameState.GameOver)
            {
                // Enter goes back to menu
                if (Raylib.IsKeyPressed(KeyboardKey.Enter))
                    state = GameState.Menu;
                return;
            }

            // Game running
            player.Update();
            foreach (var a in asteroids) a.Update();
            foreach (var e in enemies) e.Update();

            CheckCollisions();

            // All asteroids gone to next level
            if (asteroids.Count == 0)
            {
                level++;
                player.Reset(new Vector2(screenW / 2f, screenH / 2f));
                SpawnLevel(level);
            }
        }

        static void CheckCollisions()
        {
            // Player bullets vs asteroids
            for (int b = player.bullets.Count - 1; b >= 0; b--)
            {
                bool hit = false;
                for (int a = asteroids.Count - 1; a >= 0; a--)
                {
                    if (Collision.CheckCollision(player.bullets[b].transform, player.bullets[b].collision,
                                                 asteroids[a].transform, asteroids[a].collision))
                    {
                        score += asteroids[a].size == AsteroidSize.Large ? 20
                               : asteroids[a].size == AsteroidSize.Medium ? 50 : 100;
                        SplitAsteroid(asteroids[a]);
                        asteroids.RemoveAt(a);
                        player.bullets.RemoveAt(b);
                        hit = true;
                        break;
                    }
                }
                if (hit) continue;

                // Player bullets vs enemies
                for (int e = enemies.Count - 1; e >= 0; e--)
                {
                    if (b < player.bullets.Count &&
                        Collision.CheckCollision(player.bullets[b].transform, player.bullets[b].collision,
                                                 enemies[e].transform, enemies[e].collision))
                    {
                        enemies.RemoveAt(e);
                        player.bullets.RemoveAt(b);
                        score += 200;
                        break;
                    }
                }
            }

            if (player.IsInvincible()) return;

            // Player touches asteroid
            foreach (var a in asteroids)
                if (Collision.CheckCollision(player.transform, player.collision, a.transform, a.collision))
                { PlayerDie(); return; }

            // Enemy bullet hits player
            foreach (var e in enemies)
                for (int b = e.bullets.Count - 1; b >= 0; b--)
                    if (Collision.CheckCollision(e.bullets[b].transform, e.bullets[b].collision,
                                                 player.transform, player.collision))
                    { e.bullets.RemoveAt(b); PlayerDie(); return; }
        }

        // Split asteroid into 2 smaller pieces
        static void SplitAsteroid(Asteroid a)
        {
            if (a.size == AsteroidSize.Small) return; // smallest size — just disappears
            AsteroidSize newSize = a.size == AsteroidSize.Large ? AsteroidSize.Medium : AsteroidSize.Small;
            float speed = newSize == AsteroidSize.Medium ? 110f : 180f;
            for (int i = 0; i < 2; i++)
            {
                float angle = rng.NextSingle() * MathF.Tau;
                Vector2 vel = new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * speed;
                asteroids.Add(new Asteroid(a.transform.position, newSize, vel));
            }
        }

        static void PlayerDie()
        {
            lives--;

            // Update and save high score if beaten
            if (score > highScore)
            {
                highScore = score;
                SaveHighScore();
            }

            if (lives <= 0)
                state = GameState.GameOver;
            else
                player.Reset(new Vector2(screenW / 2f, screenH / 2f));
        }

        static void Draw()
        {
            Raylib.BeginDrawing();
            Raylib.ClearBackground(Color.Black);

            if (state == GameState.Menu)
                DrawMenu();
            else if (state == GameState.GameOver)
                DrawGameOver();
            else
                DrawGame();

            Raylib.EndDrawing();
        }

        static void DrawMenu()
        {
            // Title
            string title = "ASTEROIDS";
            int tw = Raylib.MeasureText(title, 70);
            Raylib.DrawText(title, screenW / 2 - tw / 2, screenH / 2 - 120, 70, Color.White);

            // Start prompt
            string start = "Press ENTER to play";
            int sw = Raylib.MeasureText(start, 26);
            Raylib.DrawText(start, screenW / 2 - sw / 2, screenH / 2, 26, Color.Yellow);

            // Controls hint
            string ctrl = "Arrows / WASD = move     Space / Z = shoot";
            int cw = Raylib.MeasureText(ctrl, 18);
            Raylib.DrawText(ctrl, screenW / 2 - cw / 2, screenH / 2 + 50, 18, Color.DarkGray);

            // High score display
            string hs = $"High Score: {highScore}";
            int hw = Raylib.MeasureText(hs, 22);
            Raylib.DrawText(hs, screenW / 2 - hw / 2, screenH / 2 + 110, 22, Color.Gold);
        }

        static void DrawGameOver()
        {
            string msg = "GAME OVER";
            int tw = Raylib.MeasureText(msg, 60);
            Raylib.DrawText(msg, screenW / 2 - tw / 2, screenH / 2 - 80, 60, Color.Red);

            string scoreText = $"Score: {score}";
            int sw = Raylib.MeasureText(scoreText, 28);
            Raylib.DrawText(scoreText, screenW / 2 - sw / 2, screenH / 2, 28, Color.White);

            // Show message if player beat the record
            if (score >= highScore && score > 0)
            {
                string newRecord = "New High Score!";
                int rw = Raylib.MeasureText(newRecord, 26);
                Raylib.DrawText(newRecord, screenW / 2 - rw / 2, screenH / 2 + 40, 26, Color.Gold);
            }

            string hsText = $"High Score: {highScore}";
            int hw = Raylib.MeasureText(hsText, 22);
            Raylib.DrawText(hsText, screenW / 2 - hw / 2, screenH / 2 + 80, 22, Color.Gold);

            string back = "Press ENTER to return to menu";
            int bw = Raylib.MeasureText(back, 20);
            Raylib.DrawText(back, screenW / 2 - bw / 2, screenH / 2 + 130, 20, Color.LightGray);
        }

        static void DrawGame()
        {
            foreach (var a in asteroids) a.Draw();
            foreach (var e in enemies) e.Draw();
            player.Draw();

            // HUD top-left
            Raylib.DrawText($"Score: {score}", 10, 10, 22, Color.White);
            Raylib.DrawText($"Level: {level}", 10, 36, 22, Color.Yellow);
            Raylib.DrawText($"Lives: {lives}", 10, 62, 22, Color.SkyBlue);
            Raylib.DrawText($"Best:  {highScore}", 10, 88, 22, Color.Gold);
        }
    }
}