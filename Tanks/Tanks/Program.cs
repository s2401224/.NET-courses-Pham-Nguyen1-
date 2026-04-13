using Raylib_cs;
using System.Numerics;

// BULLET
// Represents a single projectile fired by a tank.
// Each tank owns one Bullet instance
public class Bullet
{
    public Vector2 position;    // current center of the bullet
    public Vector2 direction;   // normalized travel direction
    public float speed = 400f; // pixels per second
    public float radius = 6f;   // collision + draw radius
    public bool active = false; // only update/draw when true
    public Color color;          // matches the owner tank's color

    // Store the bullet's color on creation; position/direction set later at shoot time
    public Bullet(Color color)
    {
        this.color = color;
    }

    // Wake the bullet up at a given position and send it flying
    public void Shoot(Vector2 startPos, Vector2 dir)
    {
        position = startPos;
        direction = dir;
        active = true;
    }

    // Move the bullet forward; kill it if it leaves the screen
    public void Update()
    {
        if (!active) return;

        // Frame-rate independent movement
        position += direction * speed * Raylib.GetFrameTime();

        // Flew off any edge → deactivate (no wrap-around)
        if (position.X < 0 || position.X > Raylib.GetScreenWidth() ||
            position.Y < 0 || position.Y > Raylib.GetScreenHeight())
        {
            active = false;
        }
    }

    // Draw a filled circle at the bullet's position
    public void Draw()
    {
        if (!active) return;
        Raylib.DrawCircleV(position, radius, color);
    }

    // Bounding box used for wall collision checks (square around the circle)
    public Rectangle GetRect()
    {
        return new Rectangle(position.X - radius, position.Y - radius, radius * 2, radius * 2);
    }
}

// WALL 
// A static obstacle on the map. Tanks stop when they hit one; bullets vanish.
public class Wall
{
    public Rectangle rect;             // position + size in world space
    public Color color = Color.Gray;   // fill color

    // Build a wall from top-left corner + dimensions
    public Wall(float x, float y, float width, float height)
    {
        rect = new Rectangle(x, y, width, height);
    }

    // Solid fill + a darker border so walls stand out from the background
    public void Draw()
    {
        Raylib.DrawRectangleRec(rect, color);
        Raylib.DrawRectangleLinesEx(rect, 2, Color.DarkGray);
    }
}

// TANK 
// Handles movement, shooting, collision detection and drawing for one player.
public class Tank
{
    public Vector2 position;                         // center of the tank hull
    public Vector2 direction;                        // which way the turret points
    public Vector2 tankSize = new Vector2(36, 36); // hull width & height
    public Vector2 turretSize = new Vector2(10, 18); // small rectangle sticking out the front
    public float speed = 160f;                // movement speed 
    public Color color;                            // hull color, also used by the bullet
    public int score = 0;                   // how many times this tank has hit the enemy
    public bool alive = true;                // false = skip update & draw

    // Each tank owns exactly one bullet 
    public Bullet bullet;

    // Prevent the holding down fire and spamming shots
    private double lastShootTime = -10.0; 
    private double shootInterval = 0.8;   

    // Keyboard bindings stored per-tank so two players can share one keyboard
    private KeyboardKey keyUp, keyDown, keyLeft, keyRight, keyShoot;

    // Set spawn position, color and control keys; default facing right
    public Tank(float x, float y, Color color,
                KeyboardKey up, KeyboardKey down, KeyboardKey left, KeyboardKey right, KeyboardKey shoot)
    {
        position = new Vector2(x, y);
        direction = new Vector2(1, 0); 
        this.color = color;
        keyUp = up; keyDown = down; keyLeft = keyLeft = left; keyRight = right; keyShoot = shoot;
        bullet = new Bullet(color);    // bullet shares the tank's color
    }

    // Called every frame: read input, try to move, handle shooting, update bullet
    public void Update(List<Wall> walls, Tank other)
    {
        if (!alive) return;

        // Read directional input; direction only updates when a key is held,
        // so the turret keeps pointing the last way the tank moved
        Vector2 velocity = Vector2.Zero;
        if (Raylib.IsKeyDown(keyUp)) { velocity = new Vector2(0, -1); direction = velocity; }
        if (Raylib.IsKeyDown(keyDown)) { velocity = new Vector2(0, 1); direction = velocity; }
        if (Raylib.IsKeyDown(keyLeft)) { velocity = new Vector2(-1, 0); direction = velocity; }
        if (Raylib.IsKeyDown(keyRight)) { velocity = new Vector2(1, 0); direction = velocity; }

        // Calculate where the tank would be next frame
        Vector2 newPos = position + velocity * speed * Raylib.GetFrameTime();

        // Clamp to screen edges using half-size as margin so the tank never clips out
        float hw = tankSize.X / 2f;
        float hh = tankSize.Y / 2f;
        newPos.X = Math.Clamp(newPos.X, hw, Raylib.GetScreenWidth() - hw);
        newPos.Y = Math.Clamp(newPos.Y, hh, Raylib.GetScreenHeight() - hh);

        // Build a rectangle at the prospective new position for overlap tests
        Rectangle newRect = new Rectangle(newPos.X - hw, newPos.Y - hh, tankSize.X, tankSize.Y);

        // Check every wall; one hit is enough to block the move entirely
        bool blocked = false;
        foreach (var wall in walls)
        {
            if (Raylib.CheckCollisionRecs(newRect, wall.rect))
            {
                blocked = true;
                break;
            }
        }

        // Also block if we'd overlap the other tank 
        Rectangle otherRect = other.GetRect();
        if (Raylib.CheckCollisionRecs(newRect, otherRect))
            blocked = true;

        // Only commit the new position if nothing is in the way
        if (!blocked)
            position = newPos;

        // Shooting
        if (Raylib.IsKeyPressed(keyShoot))
        {
            // Two conditions
            if (Raylib.GetTime() - lastShootTime > shootInterval)
            {
                if (!bullet.active)
                {
                    // Spawn bullet just past the turret tip so it doesn't hit the hull
                    bullet.Shoot(position + direction * (tankSize.X / 2f + bullet.radius + 2f), direction);
                    lastShootTime = Raylib.GetTime();
                }
            }
        }

        // Advance the bullet and check if it hit any wall this frame
        bullet.Update();

        if (bullet.active)
        {
            foreach (var wall in walls)
            {
                // Circle vs rectangle check 
                if (Raylib.CheckCollisionCircleRec(bullet.position, bullet.radius, wall.rect))
                {
                    bullet.active = false; // bullet disappears on wall contact
                    break;
                }
            }
        }
    }

    // Draw hull, turret and the owned bullet
    public void Draw()
    {
        if (!alive) return;

        // Hull: square centered on position
        Vector2 topLeft = position - tankSize / 2f;
        Raylib.DrawRectangleV(topLeft, tankSize, color);

        // Turret: small black rectangle pushed out in the current facing direction
        Vector2 turretPos = position + direction * (tankSize.X / 2f + turretSize.X / 2f);
        Vector2 turretTopLeft = turretPos - turretSize / 2f;
        Raylib.DrawRectangleV(turretTopLeft, turretSize, Color.Black);

        // Draw bullet on top of everything else
        bullet.Draw();
    }

    // Returns the hull's axis-aligned bounding box 
    public Rectangle GetRect()
    {
        return new Rectangle(position.X - tankSize.X / 2f, position.Y - tankSize.Y / 2f, tankSize.X, tankSize.Y);
    }

    // Teleport back to spawn, clear bullet, mark alive — scores are NOT touched
    public void Reset(Vector2 startPos)
    {
        position = startPos;
        bullet.active = false;
        alive = true;
    }
}

// GAME 
// Top-level class: owns the window, tanks, walls and the main loop.
public class Game
{
    Tank player1 = null!;
    Tank player2 = null!;
    List<Wall> walls = new();

    // Spawn positions read from the map; kept so ResetRound knows where to send tanks
    Vector2 p1Start;
    Vector2 p2Start;

    int screenW = 800;
    int screenH = 600;

    // Map layout 
    // 0 = empty, 1 = P1 spawn, 2 = P2 spawn, 3+ = wall
    // Each number represents one grid cell
    int mapW = 12;
    int mapH = 10;
    int[] map = new int[]
    {
        0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
        0, 1, 0, 0, 3, 0, 0, 3, 0, 0, 0, 0,  // P1 spawns at (1,1); two vertical walls
        0, 0, 0, 0, 3, 0, 0, 3, 0, 0, 0, 0,
        0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
        0, 0, 3, 0, 0, 0, 0, 0, 0, 3, 0, 0,  // side pillars
        0, 0, 3, 0, 0, 0, 0, 0, 0, 3, 0, 0,
        0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
        0, 0, 0, 3, 3, 0, 0, 3, 3, 0, 0, 0,  // bottom horizontal barriers
        0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 2, 0,  // P2 spawns at (10,8)
        0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
    };

    // Entry point is creating one Game instance and start it
    public static void Main()
    {
        new Game().Run();
    }

    // Run = Init once, then loop forever until the window closes
    public void Run()
    {
        Init();
        GameLoop();
    }

    // Open the window, lock to 60 fps, build walls and spawn tanks
    private void Init()
    {
        Raylib.InitWindow(screenW, screenH, "Tanks");
        Raylib.SetTargetFPS(60);
        BuildMap();
    }

    // Parse the map array: place walls and figure out spawn positions
    private void BuildMap()
    {
        walls.Clear();

        // Each cell is exactly this many pixels wide/tall
        float cellW = (float)screenW / mapW;
        float cellH = (float)screenH / mapH;

        for (int y = 0; y < mapH; y++)
        {
            for (int x = 0; x < mapW; x++)
            {
                // 1D index from 2D coordinates: row * rowWidth + column
                int cell = map[y * mapW + x];

                // Center of this cell in world space 
                float cx = x * cellW + cellW / 2f;
                float cy = y * cellH + cellH / 2f;

                if (cell >= 3)
                {
                    // Any value >= 3 means a solid wall tile
                    walls.Add(new Wall(x * cellW, y * cellH, cellW, cellH));
                }
                else if (cell == 1)
                {
                    p1Start = new Vector2(cx, cy); // remember for resets
                }
                else if (cell == 2)
                {
                    p2Start = new Vector2(cx, cy);
                }
            }
        }

        // Create both tanks with their colors and key bindings
        // Scores stay at 0 on first call; BuildMap isn't called again mid-game
        player1 = new Tank(p1Start.X, p1Start.Y, Color.Red,
            KeyboardKey.W, KeyboardKey.S, KeyboardKey.A, KeyboardKey.D, KeyboardKey.Space);

        player2 = new Tank(p2Start.X, p2Start.Y, Color.SkyBlue,
            KeyboardKey.Up, KeyboardKey.Down, KeyboardKey.Left, KeyboardKey.Right, KeyboardKey.Enter);
    }

    // Send both tanks back to their starting tiles and kill any flying bullets
    private void ResetRound()
    {
        player1.Reset(p1Start);
        player2.Reset(p2Start);
    }

    // Core loop: keep going until the user closes the window
    private void GameLoop()
    {
        while (!Raylib.WindowShouldClose())
        {
            UpdateGame(); // logic first
            DrawGame();   // then render
        }
        Raylib.CloseWindow();
    }

    // Update both tanks
    private void UpdateGame()
    {
        // Each tank gets the walls list and a reference to the opponent for tank-tank collision
        player1.Update(walls, player2);
        player2.Update(walls, player1);

        // Did P1's bullet reach P2's hull?
        if (player1.bullet.active && player2.alive)
        {
            if (Raylib.CheckCollisionCircleRec(player1.bullet.position, player1.bullet.radius, player2.GetRect()))
            {
                player1.score++;        // shooter gets the point
                player1.bullet.active = false;
                ResetRound();           // both tanks go back to spawn
                return;                 // skip P2 check this frame
            }
        }

        // Did P2's bullet reach P1's hull?
        if (player2.bullet.active && player1.alive)
        {
            if (Raylib.CheckCollisionCircleRec(player2.bullet.position, player2.bullet.radius, player1.GetRect()))
            {
                player2.score++;
                player2.bullet.active = false;
                ResetRound();
                return;
            }
        }
    }

    private void DrawGame()
    {
        Raylib.BeginDrawing();
        Raylib.ClearBackground(new Color(34, 100, 34, 255)); // dark green battlefield

        // Draw all walls first 
        foreach (var wall in walls)
            wall.Draw();

        // Tanks (and their bullets) on top
        player1.Draw();
        player2.Draw();

        // ── HUD 
        // Scores in matching colors at the top corners
        Raylib.DrawText($"P1 Score: {player1.score}", 10, 10, 28, Color.Red);
        Raylib.DrawText($"P2 Score: {player2.score}", screenW - 170, 10, 28, Color.SkyBlue);

        // Key reminders at the bottom so new players know the controls
        Raylib.DrawText("P1: WASD + Space", 10, screenH - 24, 16, Color.LightGray);
        Raylib.DrawText("P2: Arrows + Enter", screenW - 180, screenH - 24, 16, Color.LightGray);

        Raylib.EndDrawing();
    }
}