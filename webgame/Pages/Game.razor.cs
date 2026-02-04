using Microsoft.AspNetCore.Components;
using System.Timers;
using webgame.Shared.Models;

namespace webgame.Pages
{
    public partial class Game : IDisposable
    {
        private const int GridWidth = 20;
        private const int GridHeight = 15;
        private const int CellSize = 40;
        private const int StartLives = 20;
        private const int StartMoney = 100;
        private const int TowerCost = 50;
        private const int UpgradeCost = 30;
        private const int EnemyReward = 10;

        private GameCell[,] GameGrid = new GameCell[GridWidth, GridHeight];
        private List<Enemy> ActiveEnemies = new();
        private List<Tower> Towers = new();
        private List<PathPoint> EnemyPath = new();
        private Timer? GameTimer;
        private int NextEnemyId = 1;
        private int NextTowerId = 1;

        private int PlayerLives = StartLives;
        private int Money = StartMoney;
        private int CurrentWave = 0;
        private int WaveEnemiesCount = 5;
        private bool IsWaveActive = false;
        private int EnemiesSpawnedThisWave = 0;
        private (int X, int Y)? SelectedTower = null;
        private bool BuyMode = false;
        private DateTime LastSpawnTime = DateTime.Now;
        private Random random = new Random();

        private bool CanStartWave => !IsWaveActive && ActiveEnemies.Count == 0;
        private string Instructions => BuyMode
            ? "Êëèêíèòå íà ïóñòóþ êëåòêó ÷òîáû ïîñòàâèòü áàøíþ"
            : SelectedTower.HasValue
                ? $"Âûáðàíà áàøíÿ [{SelectedTower.Value.X},{SelectedTower.Value.Y}]"
                : "Êëèêíèòå íà áàøíþ ÷òîáû âûáðàòü";

        protected override void OnInitialized()
        {
            InitializeGrid();
            SetupPath();
            SetupTimer();
        }

        private void InitializeGrid()
        {
            for (int x = 0; x < GridWidth; x++)
            {
                for (int y = 0; y < GridHeight; y++)
                {
                    GameGrid[x, y] = new GameCell
                    {
                        X = x,
                        Y = y,
                        IsPath = false,
                        CanPlaceTower = true
                    };
                }
            }
        }

        private void SetupPath()
        {
            int pathY = GridHeight / 2;

            for (int x = 0; x < GridWidth; x++)
            {
                GameGrid[x, pathY].IsPath = true;
                GameGrid[x, pathY].CanPlaceTower = false;
                EnemyPath.Add(new PathPoint { X = x, Y = pathY });
            }

            GameGrid[0, pathY].IsStart = true;
            GameGrid[GridWidth - 1, pathY].IsEnd = true;
        }

        private void SetupTimer()
        {
            GameTimer = new Timer(100); // 10 FPS
            GameTimer.Elapsed += async (sender, e) => await InvokeAsync(GameUpdate);
            GameTimer.Start();
        }

        private void GameUpdate()
        {
            if (IsWaveActive)
            {
                SpawnEnemies();
                MoveEnemies();
                TowersShoot();
                CheckWaveEnd();
            }
            StateHasChanged();
        }

        private void SpawnEnemies()
        {
            if (EnemiesSpawnedThisWave >= WaveEnemiesCount)
                return;

            // Ñïàâíèì âðàãà êàæäóþ ñåêóíäó
            if ((DateTime.Now - LastSpawnTime).TotalSeconds >= 1.0)
            {
                var startPoint = EnemyPath.First();
                var enemy = new Enemy
                {
                    Id = NextEnemyId++,
                    X = startPoint.X,
                    Y = startPoint.Y,
                    Type = GetEnemyTypeForWave(),
                    MaxHealth = 50 + CurrentWave * 10,
                    Health = 50 + CurrentWave * 10,
                    Reward = EnemyReward + CurrentWave * 2,
                    Speed = 1
                };

                ActiveEnemies.Add(enemy);
                GameGrid[startPoint.X, startPoint.Y].Enemy = enemy;
                EnemiesSpawnedThisWave++;
                LastSpawnTime = DateTime.Now;
            }
        }

        private string GetEnemyTypeForWave()
        {
            if (CurrentWave >= 8) return random.Next(0, 3) == 0 ? "boss" : "tank";
            if (CurrentWave >= 5) return random.Next(0, 2) == 0 ? "tank" : "fast";
            if (CurrentWave >= 3) return random.Next(0, 3) == 0 ? "fast" : "normal";
            return "normal";
        }

        private void MoveEnemies()
        {
            foreach (var enemy in ActiveEnemies.ToList())
            {
                var currentIndex = EnemyPath.FindIndex(p => p.X == enemy.X && p.Y == enemy.Y);
                if (currentIndex >= 0 && currentIndex < EnemyPath.Count - 1)
                {
                    // Î÷èùàåì ñòàðóþ êëåòêó
                    GameGrid[enemy.X, enemy.Y].Enemy = null;

                    var nextPoint = EnemyPath[currentIndex + 1];
                    enemy.X = nextPoint.X;
                    enemy.Y = nextPoint.Y;

                    if (GameGrid[enemy.X, enemy.Y].IsEnd)
                    {
                        PlayerLives--;
                        ActiveEnemies.Remove(enemy);
                        if (PlayerLives <= 0)
                        {
                            IsWaveActive = false;
                        }
                    }
                    else
                    {
                        GameGrid[enemy.X, enemy.Y].Enemy = enemy;
                    }
                }
            }
        }

        private void TowersShoot()
        {
            foreach (var tower in Towers)
            {
                if ((DateTime.Now - tower.LastShotTime).TotalMilliseconds < tower.FireRate)
                    continue;

                // Èùåì öåëü
                var target = ActiveEnemies
                    .Where(e => Math.Abs(e.X - tower.X) <= tower.Range &&
                                Math.Abs(e.Y - tower.Y) <= tower.Range)
                    .OrderBy(e => Math.Abs(e.X - tower.X) + Math.Abs(e.Y - tower.Y))
                    .FirstOrDefault();

                if (target != null)
                {
                    target.Health -= tower.Damage;
                    tower.TargetEnemyId = target.Id;
                    tower.LastShotTime = DateTime.Now;

                    if (target.Health <= 0)
                    {
                        Money += target.Reward;
                        ActiveEnemies.Remove(target);
                        GameGrid[target.X, target.Y].Enemy = null;
                        tower.TargetEnemyId = null;
                    }
                }
                else
                {
                    tower.TargetEnemyId = null;
                }
            }
        }

        private void CheckWaveEnd()
        {
            if (EnemiesSpawnedThisWave >= WaveEnemiesCount && ActiveEnemies.Count == 0)
            {
                IsWaveActive = false;
                CurrentWave++;
                WaveEnemiesCount = 5 + CurrentWave * 2;
                EnemiesSpawnedThisWave = 0;
                Money += 50; // Íàãðàäà çà âîëíó
                LastSpawnTime = DateTime.Now;
            }
        }

        private void OnCellClick(int x, int y)
        {
            var cell = GameGrid[x, y];

            if (cell.Tower != null)
            {
                SelectedTower = (x, y);
                BuyMode = false;
                return;
            }
            // Êëèê ïî ïóñòîé êëåòêå â ðåæèìå ïîêóïêè
            if (BuyMode && cell.CanPlaceTower && Money >= TowerCost)
            {
                PlaceTower(x, y);
                BuyMode = false;
            }
            else if (cell.CanPlaceTower)
            {
                SelectedTower = null;
            }
        }

        private void PlaceTower(int x, int y)
        {
            var tower = new Tower
            {
                Id = NextTowerId++,
                X = x,
                Y = y,
                LastShotTime = DateTime.Now.AddSeconds(-1)
            };

            GameGrid[x, y].Tower = tower;
            GameGrid[x, y].CanPlaceTower = false;
            Towers.Add(tower);
            Money -= TowerCost;
            SelectedTower = (x, y);
        }

        private void StartWave()
        {
            if (!IsWaveActive && ActiveEnemies.Count == 0)
            {
                IsWaveActive = true;
                EnemiesSpawnedThisWave = 0;
                LastSpawnTime = DateTime.Now;
            }
        }

        private void UpgradeTower()
        {
            if (!SelectedTower.HasValue || Money < UpgradeCost)
                return;

            var (x, y) = SelectedTower.Value;
            var cell = GameGrid[x, y];

            if (cell.Tower != null)
            {
                cell.Tower.Level++;
                cell.Tower.Damage += 10;
                cell.Tower.Range += 1;
                cell.Tower.FireRate = Math.Max(200, cell.Tower.FireRate - 100);
                Money -= UpgradeCost;
            }
        }

        private void SellTower()
        {
            if (!SelectedTower.HasValue)
                return;

            var (x, y) = SelectedTower.Value;
            var cell = GameGrid[x, y];

            if (cell.Tower != null)
            {
                Money += TowerCost / 2;
                Towers.Remove(cell.Tower);
                cell.Tower = null;
                cell.CanPlaceTower = true;
                SelectedTower = null;
            }
        }

        private void RestartGame()
        {
            PlayerLives = StartLives;
            Money = StartMoney;
            CurrentWave = 0;
            WaveEnemiesCount = 5;
            IsWaveActive = false;
            ActiveEnemies.Clear();
            Towers.Clear();
            SelectedTower = null;
            BuyMode = false;
            NextEnemyId = 1;
            NextTowerId = 1;

            InitializeGrid();
            SetupPath();
        }

        public void Dispose()
        {
            GameTimer?.Stop();
            GameTimer?.Dispose();
        }
    }

}
