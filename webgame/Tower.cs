using System.Numerics;
using System.Collections.Generic;
using System;

namespace webgame
{
    public enum TowerType
    {
        Archer,
        IceMage,
        FireMage,
        Witch
    }

    public class Tower
    {
        public TowerType Type { get; set; }
        public Vector2 Position { get; set; }
        public int Damage { get; set; }
        public float Range { get; set; }
        public float FireRate { get; set; }
        public float Cooldown { get; set; }
        public int Cost { get; set; }
        public string Name { get; set; }

        public string ColorHtml { get; set; }

        public Tower(TowerType type, Vector2 position)
        {
            Type = type;
            Position = position;

            switch (type)
            {
                case TowerType.Archer:
                    Damage = 15; Range = 150; FireRate = 2.0f; Cost = 50;
                    ColorHtml = "#00FF00"; Name = "Archer"; break; // Green

                case TowerType.IceMage:
                    Damage = 8; Range = 120; FireRate = 1.0f; Cost = 80;
                    ColorHtml = "#00FFFF"; Name = "Ice Mage"; break; // Cyan

                case TowerType.FireMage:
                    Damage = 20; Range = 100; FireRate = 0.7f; Cost = 100;
                    ColorHtml = "#FF4500"; Name = "Fire Mage"; break; // OrangeRed

                case TowerType.Witch:
                    Damage = 10; Range = 130; FireRate = 1.5f; Cost = 120;
                    ColorHtml = "#800080"; Name = "Witch"; break; // Purple
            }

            Cooldown = 0;
        }

        // Обновленный Update без GameTime
        public void Update(float deltaTime, List<Enemy> enemies, GameState gameState)
        {
            Cooldown -= deltaTime;

            if (Cooldown > 0) return;

            Enemy target = null;
            float minDistance = float.MaxValue;

            foreach (var enemy in enemies)
            {
                if (!enemy.IsAlive) continue;

                float distance = Vector2.Distance(enemy.Position, Position);

                if (distance <= Range && distance < minDistance)
                {
                    minDistance = distance;
                    target = enemy;
                }
            }

            if (target != null)
            {
                target.Health -= Damage;

                if (!target.IsAlive)
                {
                    gameState.Gold += target.GoldReward;
                }

                Cooldown = 1.0f / FireRate;
            }
        }

        public bool IsEnemyInRange(Enemy enemy)
        {
            if (!enemy.IsAlive) return false;
            return Vector2.Distance(enemy.Position, Position) <= Range;
        }

    }
}