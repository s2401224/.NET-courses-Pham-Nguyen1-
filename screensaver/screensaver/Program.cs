using Raylib_cs;
using System.Numerics;

class Program
{
    static void Main()
    {
        // Create window (800x800) with title
        Raylib.InitWindow(800, 800, "Screensaver");

        // Limit FPS (important for smooth movement)
        Raylib.SetTargetFPS(30);

        // Triangle points (start positions)
        Vector2 A = new Vector2(Raylib.GetScreenWidth() / 2, 40);                  // top middle
        Vector2 B = new Vector2(40, Raylib.GetScreenHeight() / 2);                // left middle
        Vector2 C = new Vector2(Raylib.GetScreenWidth() - 40, Raylib.GetScreenHeight() * 3 / 4); // bottom right

        // Movement speed (pixels per second)
        float speed = 200f;

        // Direction vectors (movement direction)
        Vector2 dirA = new Vector2(1, 1);   // move right + down
        Vector2 dirB = new Vector2(1, -1);  // move right + up
        Vector2 dirC = new Vector2(-1, 1);  // move left + down

        // Main loop (runs every frame)
        while (!Raylib.WindowShouldClose())
        {
            // Delta time (time between frames)
            float dt = Raylib.GetFrameTime();

            // Screen size
            int width = Raylib.GetScreenWidth();
            int height = Raylib.GetScreenHeight();

            // Move points (position += direction * speed * time)
            A += dirA * speed * dt;
            B += dirB * speed * dt;
            C += dirC * speed * dt;

            // Bounce on left/right edges (X axis)
            if (A.X < 0 || A.X > width) dirA.X *= -1;
            if (B.X < 0 || B.X > width) dirB.X *= -1;
            if (C.X < 0 || C.X > width) dirC.X *= -1;

            // Bounce on top/bottom edges (Y axis)
            if (A.Y < 0 || A.Y > height) dirA.Y *= -1;
            if (B.Y < 0 || B.Y > height) dirB.Y *= -1;
            if (C.Y < 0 || C.Y > height) dirC.Y *= -1;

            // Start drawing
            Raylib.BeginDrawing();

            // Clear screen (black background)
            Raylib.ClearBackground(new Color(0, 0, 0, 255));

            // Draw triangle lines (3 sides)
            Raylib.DrawLineV(A, B, new Color(0, 255, 0, 255));       // green
            Raylib.DrawLineV(B, C, new Color(255, 255, 0, 255));     // yellow
            Raylib.DrawLineV(C, A, new Color(100, 200, 255, 255));   // light blue

            // Finish drawing
            Raylib.EndDrawing();
        }

        // Close window when program ends
        Raylib.CloseWindow();
    }
}