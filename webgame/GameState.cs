using System;
using System.Collections.Generic;
using System.Numerics; // Вместо Microsoft.Xna.Framework
using System.Linq;
using System.Timers;

namespace webgame
{
    public class GameState
    {
        // События для уведомления UI об изменениях
        public event Action OnStateChanged;
        public event Action<Enemy> OnEnemySpawned;
        public event Action<Enemy> OnEnemyDied;
        public event Action OnWaveCompleted;
        public event Action OnGameOver;

        // Игровые параметры
        private int _gold = 150;
        public int Gold
        {
            get => _gold;
            set
            {
                if (_gold != value)
                {
                    _gold = value;
                    NotifyStateChanged();
                }
            }
        }

        private int _playerHealth = 100;
        public int PlayerHealth
        {
            get => _playerHealth;
            set
            {
                if (_playerHealth != value)
                {
                    _playerHealth = value;
                    if (_playerHealth <= 0)
                    {
                        OnGameOver?.Invoke();
                    }
                    NotifyStateChanged();
                }
            }
        }

        private int _currentWave = 1;
        public int CurrentWave
        {
            get => _currentWave;
            set
            {
                if (_currentWave != value)
                {
                    _currentWave = value;
                    NotifyStateChanged();
                }
            }
        }

        private bool _isWaveActive;
        public bool IsWaveActive
        {
            get => _isWaveActive;
            set
            {
                if (_isWaveActive != value)
                {
                    _isWaveActive = value;
                    NotifyStateChanged();
                }
            }
        }

        public bool IsGameOver => PlayerHealth <= 0;

        // Коллекции
        public List<Enemy> Enemies { get; private set; } = new List<Enemy>();
        public List<Tower> Towers { get; private set; } = new List<Tower>();

        // Места для башен
        public List<TowerSlot> TowerSlotsLeft { get; private set; } = new List<TowerSlot>();
        public List<TowerSlot> TowerSlotsRight { get; private set; } = new List<TowerSlot>();

        // Игровые параметры
        private float spawnTimer;
        private int enemiesToSpawn;
        private Random random = new Random();

        // Для анимаций и визуальных эффектов
        public List<Projectile> Projectiles { get; private set; } = new List<Projectile>();
        public List<DamageNumber> DamageNumbers { get; private set; } = new List<DamageNumber>();

        // Таймер для игрового цикла
        private System.Timers.Timer gameTimer;
        private DateTime lastUpdateTime;

        public GameState()
        {
            InitializeTowerSlots();
            StartGameLoop();
        }

        private void InitializeTowerSlots()
        {
            // Слоты слева
            for (int i = 0; i < 5; i++)
            {
                TowerSlotsLeft.Add(new TowerSlot
                {
                    Position = new Vector2(100, 100 + i * 80),
                    IsOccupied = false,
                    Side = EnemySide.Left,
                    Index = i
                });
            }

            // Слоты справа
            for (int i = 0; i < 5; i++)
            {
                TowerSlotsRight.Add(new TowerSlot
                {
                    Position = new Vector2(700, 100 + i * 80),
                    IsOccupied = false,
                    Side = EnemySide.Right,
                    Index = i
                });
            }
        }

        private void StartGameLoop()
        {
            gameTimer = new System.Timers.Timer(16); // ~60 FPS (1000/60 ≈ 16ms)
            gameTimer.Elapsed += (sender, e) => Update();
            gameTimer.AutoReset = true;
            gameTimer.Start();
            lastUpdateTime = DateTime.Now;
        }

        public void Update()
        {
            if (IsGameOver) return;

            var now = DateTime.Now;
            float deltaTime = (float)(now - lastUpdateTime).TotalSeconds;
            lastUpdateTime = now;

            // Ограничиваем deltaTime, чтобы избежать больших скачков
            deltaTime = Math.Min(deltaTime, 0.1f);

            if (IsWaveActive)
            {
                UpdateWave(deltaTime);
            }

            UpdateEnemies(deltaTime);
            UpdateTowers(deltaTime);
            UpdateProjectiles(deltaTime);
            UpdateDamageNumbers(deltaTime);

            NotifyStateChanged();
        }

        private void UpdateWave(float deltaTime)
        {
            // Спавн врагов
            if (enemiesToSpawn > 0)
            {
                spawnTimer += deltaTime;
                if (spawnTimer >= 0.8f)
                {
                    SpawnEnemy();
                    enemiesToSpawn--;
                    spawnTimer = 0;
                }
            }

            // Завершение волны
            if (Enemies.Count == 0 && enemiesToSpawn == 0 && IsWaveActive)
            {
                CompleteWave();
            }
        }

        private void SpawnEnemy()
        {
            EnemySide side = (random.Next(2) == 0) ? EnemySide.Left : EnemySide.Right;
            Vector2 startPosition = (side == EnemySide.Left)
                ? new Vector2(50, -30)
                : new Vector2(750, -30);

            var enemy = new Enemy(side, CurrentWave, startPosition);
            Enemies.Add(enemy);
            OnEnemySpawned?.Invoke(enemy);
        }

        private void CompleteWave()
        {
            IsWaveActive = false;
            CurrentWave++;
            Gold += 50 + CurrentWave * 10;
            OnWaveCompleted?.Invoke();
        }

        private void UpdateEnemies(float deltaTime)
        {
            for (int i = Enemies.Count - 1; i >= 0; i--)
            {
                var enemy = Enemies[i];

                // Сохраняем предыдущую позицию для проверки достижения конца
                float previousY = enemy.Position.Y;

                enemy.Update();

                // Проверка достижения конца
                if (enemy.ReachedEnd)
                {
                    PlayerHealth -= 15;
                    RemoveEnemyAt(i);
                }
                // Проверка смерти
                else if (!enemy.IsAlive)
                {
                    Gold += enemy.GoldReward;
                    RemoveEnemyAt(i);
                }
            }
        }

        private void RemoveEnemyAt(int index)
        {
            var enemy = Enemies[index];
            Enemies.RemoveAt(index);
            OnEnemyDied?.Invoke(enemy);
        }

        private void UpdateTowers(float deltaTime)
        {
            foreach (var tower in Towers)
            {
                // Ищем цель для башни
                var target = FindNearestEnemyInRange(tower);

                if (target != null)
                {
                    tower.Update(deltaTime, Enemies, this);

                    // Создаем снаряд при атаке
                    if (tower.Cooldown <= 0)
                    {
                        CreateProjectile(tower, target);
                    }
                }
            }
        }

        private Enemy FindNearestEnemyInRange(Tower tower)
        {
            Enemy nearest = null;
            float minDistance = float.MaxValue;

            foreach (var enemy in Enemies)
            {
                if (!enemy.IsAlive) continue;

                float distance = Vector2.Distance(enemy.Position, tower.Position);
                if (distance <= tower.Range && distance < minDistance)
                {
                    minDistance = distance;
                    nearest = enemy;
                }
            }

            return nearest;
        }

        private void CreateProjectile(Tower tower, Enemy target)
        {
            Projectiles.Add(new Projectile
            {
                StartPosition = tower.Position,
                Target = target,
                Damage = tower.Damage,
                Speed = 500f,
                Color = tower.ColorHtml
            });
        }

        private void UpdateProjectiles(float deltaTime)
        {
            for (int i = Projectiles.Count - 1; i >= 0; i--)
            {
                var projectile = Projectiles[i];

                if (projectile.Update(deltaTime))
                {
                    // Снаряд достиг цели
                    if (projectile.Target != null && projectile.Target.IsAlive)
                    {
                        projectile.Target.Health -= projectile.Damage;

                        // Добавляем число урона
                        DamageNumbers.Add(new DamageNumber
                        {
                            Position = projectile.Target.Position,
                            Damage = projectile.Damage,
                            TimeToLive = 1f
                        });
                    }
                    Projectiles.RemoveAt(i);
                }
            }
        }

        private void UpdateDamageNumbers(float deltaTime)
        {
            for (int i = DamageNumbers.Count - 1; i >= 0; i--)
            {
                var damageNumber = DamageNumbers[i];
                damageNumber.TimeToLive -= deltaTime;

                if (damageNumber.TimeToLive <= 0)
                {
                    DamageNumbers.RemoveAt(i);
                }
            }
        }

        public void StartWave()
        {
            if (IsWaveActive || IsGameOver) return;

            IsWaveActive = true;
            enemiesToSpawn = 3 + CurrentWave * 2;
            spawnTimer = 0;
        }

        public bool TryPlaceTower(TowerType type, int slotIndex, bool isLeftSide)
        {
            var slots = isLeftSide ? TowerSlotsLeft : TowerSlotsRight;

            if (slotIndex < 0 || slotIndex >= slots.Count)
                return false;

            var slot = slots[slotIndex];

            if (slot.IsOccupied)
                return false;

            var tempTower = new Tower(type, Vector2.Zero);

            if (Gold < tempTower.Cost)
                return false;

            // Создаем и размещаем башню
            var newTower = new Tower(type, slot.Position);
            Towers.Add(newTower);

            slot.IsOccupied = true;
            slot.Tower = newTower;

            Gold -= tempTower.Cost;

            NotifyStateChanged();
            return true;
        }

        public TowerSlot GetSlotAtPosition(Vector2 position, float tolerance = 30f)
        {
            // Проверяем все слоты
            foreach (var slot in TowerSlotsLeft.Concat(TowerSlotsRight))
            {
                float distance = Vector2.Distance(position, slot.Position);
                if (distance < tolerance && !slot.IsOccupied)
                {
                    return slot;
                }
            }
            return null;
        }

        private void NotifyStateChanged()
        {
            OnStateChanged?.Invoke();
        }

        public void StopGameLoop()
        {
            gameTimer?.Stop();
            gameTimer?.Dispose();
        }
    }

    // Вспомогательные классы

    public class TowerSlot
    {
        public Vector2 Position { get; set; }
        public bool IsOccupied { get; set; }
        public EnemySide Side { get; set; }
        public int Index { get; set; }
        public Tower Tower { get; set; }
    }

    public class Projectile
    {
        public Vector2 StartPosition { get; set; }
        public Vector2 CurrentPosition { get; set; }
        public Enemy Target { get; set; }
        public int Damage { get; set; }
        public float Speed { get; set; }
        public float Progress { get; set; }
        public string Color { get; set; }

        public bool Update(float deltaTime)
        {
            if (Target == null || !Target.IsAlive)
                return true; // Цель мертва, снаряд исчезает

            CurrentPosition = Vector2.Lerp(StartPosition, Target.Position, Progress);
            Progress += deltaTime * Speed / 100f;

            return Progress >= 1f || Target == null || !Target.IsAlive;
        }
    }

    public class DamageNumber
    {
        public Vector2 Position { get; set; }
        public int Damage { get; set; }
        public float TimeToLive { get; set; }
    }
}