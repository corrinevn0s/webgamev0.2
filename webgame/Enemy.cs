using System.Numerics; 

namespace webgame
{
    public enum EnemySide
    {
        Left,
        Right
    }

    public class Enemy
    {
        public EnemySide Side { get; set; }
        public int Health { get; set; }
        public int MaxHealth { get; set; }
        public float Speed { get; set; }
        public int GoldReward { get; set; }
        public Vector2 Position { get; set; } // System.Numerics.Vector2
        public bool IsAlive => Health > 0;
        public bool ReachedEnd => Position.Y >= 550;

        public Rectangle Bounds => new Rectangle(
            (int)Position.X - 15,
            (int)Position.Y - 15,
            30, 30);

        public Enemy(EnemySide side, int waveNumber, Vector2 startPosition)
        {
            Side = side;
            Position = startPosition;

            MaxHealth = 30 + waveNumber * 10;
            Health = MaxHealth;
            Speed = 1.0f + waveNumber * 0.1f;
            GoldReward = 10 + waveNumber * 3;
        }

        public void Update()
        {
            Position = new Vector2(Position.X, Position.Y + Speed);
        }
    }

    public struct Rectangle
    {
        public int X { get; set; }
        public int Y { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }

        public Rectangle(int x, int y, int width, int height)
        {
            X = x; Y = y; Width = width; Height = height;
        }
    }
}