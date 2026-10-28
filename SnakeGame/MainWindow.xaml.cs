using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Media;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace SnakeGame
{
    public partial class MainWindow : Window
    {
        private const int GridSize = 20;
        private const int CellSize = 20;
        private const int WinScore = int.MaxValue;

        private readonly List<Point> _snake = new List<Point>();
        private readonly List<bool> _snakeGoldenSegments = new List<bool>();
        private readonly Random _random = new Random();
        private readonly List<Ellipse> _fireworks = new List<Ellipse>();
        private SoundPlayer _backgroundMusic;
        private SoundPlayer _appleSound;
        private SoundPlayer _goldenAppleSound;
        private SoundPlayer _levelUpSound;
        private SoundPlayer _winnerSound;
        private SoundPlayer _gameOverSound;

        private Point _food;
        private Point _direction;
        private Point _queuedDirection;
        private DispatcherTimer _gameTimer;
        private DispatcherTimer _fireworkTimer;
        private DispatcherTimer _proColorTimer;
        private int _score;
        private int _bestScore;
        private bool _isNewBestScore;
        private int _winCount;
        private bool _isRunning;
        private bool _isGameOver;
        private bool _isGoldenFood;
        private bool _winnerReached;
        private int _normalApplesSinceGolden;
        private int _currentSpeedTier;
        private int _proColorIndex;
        private bool _isProMode;
        private bool _isHardMode;
        private GameMode _gameMode = GameMode.Easy;
        private int _activePage;
        private Grid _homePage;
        private Grid _modePage;
        private readonly Brush[] _proSnakeColors =
        {
            Brushes.HotPink,
            Brushes.Red,
            Brushes.DodgerBlue,
            Brushes.Gold,
            Brushes.LimeGreen,
            Brushes.White
        };
        private enum GameMode
        {
            Easy,
            Hard,
            Pro
        }
        private const double GoldenFoodChance = 0.12;
        private const int GoldenAppleEveryNormalApples = 7;

        public MainWindow()
        {
            InitializeComponent();
            BuildNavigationPages();
            ResetPreviousBestScores();
            _bestScore = LoadBestScore();
            _winCount = LoadWinCount();
            Loaded += MainWindow_Loaded;
            Closed += MainWindow_Closed;
            KeyDown += MainWindow_KeyDown;
            MouseLeftButtonDown += MainWindow_MouseLeftButtonDown;
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            ConfigureLargerDisplay();
            ApplyNeonTextEffects(this);
            InitializeAudio();
            StartBackgroundMusic();
            InitializeGame(false);
            ShowHomePage();
        }

        private void BuildNavigationPages()
        {
            var root = Content as Grid;
            if (root == null)
            {
                return;
            }

            _homePage = CreatePage("#e51f3048", "#e50a4ca8", "#fbbf24");
            var homeStack = CreatePageStack();
            homeStack.Children.Add(CreatePageText("SNAKE", 58, "#fbbf24", true));
            homeStack.Children.Add(CreatePageButton("PRESS START", "#fbbf24", "#422006", HomeStartButton_Click, 22));
            _homePage.Children.Add(homeStack);
            root.Children.Add(_homePage);

            _modePage = CreatePage("#e51d3154", "#e50b4f8a", "#fbbf24");
            var modeStack = CreatePageStack();
            modeStack.Children.Add(CreatePageText("CHOOSE THE GAME MODE", 30, "#fef3c7", true));
            modeStack.Children.Add(CreatePageText("Same arcade rules. Different intensity.", 14, "#bfdbfe", false, 8));
            modeStack.Children.Add(CreatePageButton("EASY MODE", "#4ade80", "#052e16", EasyModeButton_Click, 18));
            modeStack.Children.Add(CreatePageButton("HARD MODE", "#f87171", "#450a0a", HardModeButton_Click, 12));
            modeStack.Children.Add(CreatePageButton("PRO MODE", "#e879f9", "#3b0764", ProModeButton_Click, 12));
            _modePage.Children.Add(modeStack);
            root.Children.Add(_modePage);
        }

        private void ApplyBrandingText(DependencyObject parent)
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                var textBlock = child as TextBlock;
                if (textBlock != null)
                {
                    if (textBlock.Text == "ERDIS-ALKION EDITION")
                    {
                        textBlock.Visibility = Visibility.Collapsed;
                    }
                }

                ApplyBrandingText(child);
            }
        }

        private Grid CreatePage(string startColor, string endColor, string frameColor)
        {
            var page = new Grid
            {
                Visibility = Visibility.Collapsed
            };
            Panel.SetZIndex(page, 200);
            page.Background = new LinearGradientBrush
            {
                StartPoint = new Point(0, 0),
                EndPoint = new Point(1, 1),
                GradientStops = new GradientStopCollection
                {
                    new GradientStop((Color)ColorConverter.ConvertFromString(startColor), 0),
                    new GradientStop((Color)ColorConverter.ConvertFromString(endColor), 1)
                }
            };

            var frame = new Border
            {
                Margin = new Thickness(18),
                BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(frameColor)),
                BorderThickness = new Thickness(2),
                CornerRadius = new CornerRadius(24),
                Background = new SolidColorBrush(Color.FromArgb(35, 0, 0, 0)),
                IsHitTestVisible = false,
                Effect = new DropShadowEffect
                {
                    Color = (Color)ColorConverter.ConvertFromString(frameColor),
                    BlurRadius = 28,
                    ShadowDepth = 0,
                    Opacity = 0.75
                }
            };
            page.Children.Add(frame);
            return page;
        }

        private StackPanel CreatePageStack()
        {
            return new StackPanel
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Width = 500
            };
        }

        private TextBlock CreatePageText(string text, double size, string color, bool bold, double topMargin = 0)
        {
            var textBlock = new TextBlock
            {
                Text = text,
                FontSize = size,
                FontWeight = bold ? FontWeights.ExtraBold : FontWeights.SemiBold,
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(color)),
                HorizontalAlignment = HorizontalAlignment.Center,
                TextAlignment = TextAlignment.Center,
                Margin = new Thickness(0, topMargin, 0, 0),
                Effect = new DropShadowEffect
                {
                    Color = (Color)ColorConverter.ConvertFromString(color),
                    BlurRadius = 16,
                    ShadowDepth = 0,
                    Opacity = 0.95
                }
            };
            return textBlock;
        }

        private Button CreatePageButton(string text, string frameColor, string fillColor, RoutedEventHandler click, double topMargin)
        {
            var button = new Button
            {
                Content = text,
                Width = 260,
                Height = 58,
                Margin = new Thickness(0, topMargin, 0, 0),
                Padding = new Thickness(18, 8, 18, 8),
                FontSize = 20,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(frameColor)),
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(fillColor)),
                BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(frameColor)),
                BorderThickness = new Thickness(2),
                HorizontalAlignment = HorizontalAlignment.Center,
                Cursor = Cursors.Hand,
                Effect = new DropShadowEffect
                {
                    Color = (Color)ColorConverter.ConvertFromString(frameColor),
                    BlurRadius = 18,
                    ShadowDepth = 0,
                    Opacity = 0.85
                }
            };
            button.Click += click;
            return button;
        }

        private void ShowHomePage()
        {
            _activePage = 0;
            _homePage.Visibility = Visibility.Visible;
            _modePage.Visibility = Visibility.Collapsed;
            if (_gameTimer != null)
            {
                _gameTimer.Stop();
            }
        }

        private void HomeStartButton_Click(object sender, RoutedEventArgs e)
        {
            ShowModePage();
        }

        private void ShowModePage()
        {
            _activePage = 1;
            _homePage.Visibility = Visibility.Collapsed;
            _modePage.Visibility = Visibility.Visible;
            if (_gameTimer != null)
            {
                _gameTimer.Stop();
            }
        }

        private void EasyModeButton_Click(object sender, RoutedEventArgs e)
        {
            StartSelectedMode(GameMode.Easy);
        }

        private void HardModeButton_Click(object sender, RoutedEventArgs e)
        {
            StartSelectedMode(GameMode.Hard);
        }

        private void ProModeButton_Click(object sender, RoutedEventArgs e)
        {
            StartSelectedMode(GameMode.Pro);
        }

        private void StartSelectedMode(GameMode mode)
        {
            _gameMode = mode;
            _isHardMode = mode != GameMode.Easy;
            _bestScore = LoadBestScore();
            _activePage = 2;
            _homePage.Visibility = Visibility.Collapsed;
            _modePage.Visibility = Visibility.Collapsed;
            InitializeGame(true);
            Focus();
        }

        private void MainWindow_Closed(object sender, EventArgs e)
        {
            StopProColorTimer();
            StopBackgroundMusic();
            DisposeAudioPlayer(_backgroundMusic);
            DisposeAudioPlayer(_appleSound);
            DisposeAudioPlayer(_goldenAppleSound);
            DisposeAudioPlayer(_levelUpSound);
            DisposeAudioPlayer(_winnerSound);
            DisposeAudioPlayer(_gameOverSound);
        }

        private void ConfigureLargerDisplay()
        {
            Width = 760;
            Height = 700;
            MinWidth = 760;
            MinHeight = 700;
            GameCanvas.Width = GridSize * CellSize;
            GameCanvas.Height = GridSize * CellSize;

            var gameGrid = GameCanvas.Parent as Grid;
            var gameFrame = gameGrid == null ? null : gameGrid.Parent as Border;
            if (gameFrame != null)
            {
                gameFrame.Width = GridSize * CellSize + 36;
                gameFrame.Height = GridSize * CellSize + 36;
            }
        }

        private void ApplyNeonTextEffects(DependencyObject parent)
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                var textBlock = child as TextBlock;
                if (textBlock != null)
                {
                    var foreground = textBlock.Foreground as SolidColorBrush;
                    textBlock.Effect = new DropShadowEffect
                    {
                        Color = foreground == null ? Colors.Cyan : foreground.Color,
                        BlurRadius = 10,
                        ShadowDepth = 0,
                        Opacity = 0.9
                    };
                }

                ApplyNeonTextEffects(child);
            }
        }

        private void InitializeAudio()
        {
            try
            {
                _backgroundMusic = CreatePlayer(CreateMusicWave());
                _appleSound = CreatePlayer(CreateToneWave(740, 90, 0.18));
                _goldenAppleSound = CreatePlayer(CreateSequenceWave(
                    new[] { 523.25, 659.25, 783.99 },
                    new[] { 90, 90, 180 },
                    0.22));
                _levelUpSound = CreatePlayer(CreateSequenceWave(
                    new[] { 392.0, 523.25, 659.25 },
                    new[] { 100, 100, 220 },
                    0.2));
                _winnerSound = CreatePlayer(CreateSequenceWave(
                    new[] { 392.0, 523.25, 659.25, 783.99, 1046.5 },
                    new[] { 120, 120, 120, 160, 420 },
                    0.24));
                _gameOverSound = CreatePlayer(CreateSequenceWave(
                    new[] { 392.0, 330.0, 261.63, 196.0 },
                    new[] { 180, 180, 220, 420 },
                    0.22));
            }
            catch
            {
                _backgroundMusic = null;
                _appleSound = null;
                _goldenAppleSound = null;
                _levelUpSound = null;
                _winnerSound = null;
                _gameOverSound = null;
            }
        }

        private void StartBackgroundMusic()
        {
            try
            {
                if (_backgroundMusic != null)
                {
                    _backgroundMusic.PlayLooping();
                }
            }
            catch
            {
            }
        }

        private void StopBackgroundMusic()
        {
            try
            {
                if (_backgroundMusic != null)
                {
                    _backgroundMusic.Stop();
                }
            }
            catch
            {
            }
        }

        private void PlaySound(SoundPlayer sound)
        {
            try
            {
                if (sound != null)
                {
                    sound.Play();
                }
            }
            catch
            {
            }
        }

        private SoundPlayer CreatePlayer(byte[] waveData)
        {
            return new SoundPlayer(new MemoryStream(waveData));
        }

        private byte[] CreateMusicWave()
        {
            var notes = new[]
            {
                329.63, 392.0, 440.0, 493.88,
                440.0, 392.0, 329.63, 329.63,
                392.0, 440.0, 493.88, 523.25,
                493.88, 440.0, 392.0, 329.63
            };

            return CreateSequenceWave(notes, Enumerable.Repeat(160, notes.Length).ToArray(), 0.026, true);
        }

        private byte[] CreateToneWave(double frequency, int durationMilliseconds, double volume)
        {
            return CreateSequenceWave(new[] { frequency }, new[] { durationMilliseconds }, volume);
        }

        private byte[] CreateSequenceWave(double[] frequencies, int[] durations, double volume, bool retro = false)
        {
            const int sampleRate = 22050;
            var samples = new List<short>();

            for (int noteIndex = 0; noteIndex < frequencies.Length; noteIndex++)
            {
                int sampleCount = sampleRate * durations[noteIndex] / 1000;
                double frequency = frequencies[noteIndex];

                for (int sampleIndex = 0; sampleIndex < sampleCount; sampleIndex++)
                {
                    double time = sampleIndex / (double)sampleRate;
                    double envelope = Math.Min(1.0, sampleIndex / 500.0);
                    envelope *= Math.Min(1.0, (sampleCount - sampleIndex) / 900.0);
                    double phase = 2 * Math.PI * frequency * time;
                    double tone = retro
                        ? (Math.Sin(phase) + 0.35 * Math.Sin(phase * 2) + 0.15 * Math.Sin(phase * 3)) * 0.65
                        : Math.Sin(phase);
                    double value = frequency <= 0 ? 0 : tone * volume * envelope;
                    samples.Add((short)(value * short.MaxValue));
                }
            }

            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream))
            {
                writer.Write(new[] { 'R', 'I', 'F', 'F' });
                writer.Write(36 + samples.Count * 2);
                writer.Write(new[] { 'W', 'A', 'V', 'E' });
                writer.Write(new[] { 'f', 'm', 't', ' ' });
                writer.Write(16);
                writer.Write((short)1);
                writer.Write((short)1);
                writer.Write(22050);
                writer.Write(22050 * 2);
                writer.Write((short)2);
                writer.Write((short)16);
                writer.Write(new[] { 'd', 'a', 't', 'a' });
                writer.Write(samples.Count * 2);

                foreach (short sample in samples)
                {
                    writer.Write(sample);
                }

                return stream.ToArray();
            }
        }

        private void DisposeAudioPlayer(SoundPlayer sound)
        {
            if (sound != null)
            {
                sound.Dispose();
            }
        }

        private void MainWindow_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (_activePage != 2)
            {
                return;
            }

            if (_isGameOver)
            {
                InitializeGame(true);
                e.Handled = true;
            }
            else if (!_isRunning && !_winnerReached)
            {
                _isRunning = true;
                _gameTimer.Start();
                UpdateStatus("Game running. Use arrow keys or WASD.");
                UpdateLiveStatus();
                e.Handled = true;
            }
        }

        private void InitializeGame(bool startImmediately)
        {
            if (_fireworkTimer != null)
            {
                _fireworkTimer.Stop();
            }

            _snake.Clear();
            _snakeGoldenSegments.Clear();
            _fireworks.Clear();
            _snake.Add(new Point(10, 10));
            _snake.Add(new Point(9, 10));
            _snake.Add(new Point(8, 10));
            _snakeGoldenSegments.Add(false);
            _snakeGoldenSegments.Add(false);
            _snakeGoldenSegments.Add(false);

            _direction = new Point(1, 0);
            _queuedDirection = _direction;
            _score = 0;
            _isNewBestScore = false;
            _isGameOver = false;
            _isRunning = startImmediately;
            _winnerReached = false;
            _isGoldenFood = false;
            _normalApplesSinceGolden = 0;
            _currentSpeedTier = 0;
            _proColorIndex = 0;
            _isProMode = false;
            StopProColorTimer();
            WinnerOverlay.Visibility = Visibility.Collapsed;
            WinsTextBlock.Visibility = Visibility.Collapsed;
            GameOverOverlay.Visibility = Visibility.Collapsed;

            SpawnFood();
            UpdateScore();
            UpdateStatus(startImmediately ? "Game running. Use arrow keys or WASD." : "Offline. Click or press Space to start.");
            ApplyOuterBackgroundTheme();
            ApplySpeedTier();
            StartTimer();
            if (!startImmediately)
            {
                _gameTimer.Stop();
            }
            UpdateLiveStatus();
            Render();
        }

        private void StartTimer()
        {
            if (_gameTimer != null)
            {
                _gameTimer.Stop();
                _gameTimer.Tick -= GameTick;
            }

            _gameTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(_gameMode == GameMode.Pro ? 90 : (_isHardMode ? 125 : 180))
            };

            _gameTimer.Tick += GameTick;
            _gameTimer.Start();
        }

        private void StartProColorTimer()
        {
            if (_proColorTimer != null)
            {
                _proColorTimer.Stop();
                _proColorTimer.Tick -= ProColorTimer_Tick;
            }

            _proColorTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1.5)
            };
            _proColorTimer.Tick += ProColorTimer_Tick;
            _proColorTimer.Start();
        }

        private void StopProColorTimer()
        {
            if (_proColorTimer != null)
            {
                _proColorTimer.Stop();
                _proColorTimer.Tick -= ProColorTimer_Tick;
                _proColorTimer = null;
            }
        }

        private void ProColorTimer_Tick(object sender, EventArgs e)
        {
            _proColorIndex = (_proColorIndex + 1) % _proSnakeColors.Length;
            Render();
        }

        private void GameTick(object sender, EventArgs e)
        {
            if (!_isRunning || _isGameOver)
            {
                return;
            }

            _direction = _queuedDirection;
            var head = _snake[0];
            var nextHead = new Point(head.X + _direction.X, head.Y + _direction.Y);

            var isEating = Math.Abs(nextHead.X - _food.X) < 0.001 && Math.Abs(nextHead.Y - _food.Y) < 0.001;

            if (nextHead.X < 0 || nextHead.Y < 0 || nextHead.X >= GridSize || nextHead.Y >= GridSize)
            {
                EndGame();
                return;
            }

            var collisionCheck = isEating ? _snake : _snake.Take(_snake.Count - 1);
            if (collisionCheck.Any(segment => segment.X == nextHead.X && segment.Y == nextHead.Y))
            {
                EndGame();
                return;
            }

            bool newSegmentIsGolden = isEating && _isGoldenFood;
            _snake.Insert(0, nextHead);

            if (isEating)
            {
                var pointsEarned = _isGoldenFood ? 5 : 1;

                PlaySound(_isGoldenFood ? _goldenAppleSound : _appleSound);

                _score += pointsEarned;
                if (_score > _bestScore)
                {
                    _bestScore = _score;
                    _isNewBestScore = true;
                }
                SaveBestScore();

                if (_isGoldenFood)
                {
                    _normalApplesSinceGolden = 0;
                }
                else
                {
                    _normalApplesSinceGolden++;
                }

                _snakeGoldenSegments.Insert(0, newSegmentIsGolden);
                SpawnFood(_normalApplesSinceGolden >= GoldenAppleEveryNormalApples);
                UpdateScore();
                ApplySpeedTier();
            }
            else
            {
                _snakeGoldenSegments.Insert(0, false);
                _snake.RemoveAt(_snake.Count - 1);
                _snakeGoldenSegments.RemoveAt(_snakeGoldenSegments.Count - 1);
            }

            Render();

        }

        private void MainWindow_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Space)
            {
                if (_winnerReached)
                {
                    return;
                }

                if (_isGameOver)
                {
                    InitializeGame(true);
                }
                else
                {
                    _isRunning = !_isRunning;
                    if (_isRunning)
                    {
                        _gameTimer.Start();
                        UpdateStatus("Game running. Use arrow keys or WASD.");
                    }
                    else
                    {
                        _gameTimer.Stop();
                        UpdateStatus("Paused. Press Space to continue.");
                    }
                    UpdateLiveStatus();
                }

                e.Handled = true;
                return;
            }

            if (e.Key == Key.R)
            {
                if (_winnerReached)
                {
                    return;
                }

                InitializeGame(true);
                e.Handled = true;
                return;
            }

            if (_isGameOver || _winnerReached)
            {
                return;
            }

            SetDirection(e.Key);
            e.Handled = true;
        }

        private void TriggerWinnerState()
        {
            if (_winnerReached)
            {
                return;
            }

            _winnerReached = true;
            _isRunning = false;
            _isGameOver = true;
            _winCount++;
            SaveWinCount();
            _gameTimer.Stop();
            WinnerOverlay.Visibility = Visibility.Visible;
            UpdateStatus("Winner! Score 100 reached.");
            Render();
        }

        private void StartFireworks()
        {
            if (_fireworkTimer != null)
            {
                _fireworkTimer.Stop();
                _fireworkTimer.Tick -= FireworkTick;
            }

            _fireworkTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(180) };
            _fireworkTimer.Tick += FireworkTick;
            _fireworkTimer.Start();
        }

        private void FireworkTick(object sender, EventArgs e)
        {
            if (!_winnerReached)
            {
                return;
            }

            foreach (var f in _fireworks)
            {
                if (GameCanvas.Children.Contains(f))
                {
                    GameCanvas.Children.Remove(f);
                }
            }

            _fireworks.Clear();

            for (int i = 0; i < 10; i++)
            {
                var spark = new Ellipse
                {
                    Width = 8,
                    Height = 8,
                    Fill = new SolidColorBrush(GetRandomFireworkColor()),
                    Opacity = 0.9
                };

                double x = _random.Next(0, GridSize * CellSize);
                double y = _random.Next(0, GridSize * CellSize);
                Canvas.SetLeft(spark, x);
                Canvas.SetTop(spark, y);
                GameCanvas.Children.Add(spark);
                _fireworks.Add(spark);
            }
        }

        private Color GetRandomFireworkColor()
        {
            var colors = new[]
            {
                Colors.Red,
                Colors.Gold,
                Colors.Blue,
                Colors.Purple,
                Colors.Lime,
                Colors.Orange,
                Colors.Pink,
                Colors.Cyan
            };

            return colors[_random.Next(colors.Length)];
        }

        private void SetDirection(Key key)
        {
            Point newDirection;

            switch (key)
            {
                case Key.Up:
                case Key.W:
                    newDirection = new Point(0, -1);
                    break;
                case Key.Down:
                case Key.S:
                    newDirection = new Point(0, 1);
                    break;
                case Key.Left:
                case Key.A:
                    newDirection = new Point(-1, 0);
                    break;
                case Key.Right:
                case Key.D:
                    newDirection = new Point(1, 0);
                    break;
                default:
                    return;
            }

            if (_snake.Count > 0 && _direction.X + newDirection.X == 0 && _direction.Y + newDirection.Y == 0)
            {
                return;
            }

            _queuedDirection = newDirection;
        }

        private void SpawnFood(bool forceGolden = false)
        {
            var openCells = new List<Point>();

            for (int x = 1; x < GridSize - 1; x++)
            {
                for (int y = 1; y < GridSize - 1; y++)
                {
                    var candidate = new Point(x, y);
                    if (!_snake.Any(segment => segment.X == candidate.X && segment.Y == candidate.Y))
                    {
                        openCells.Add(candidate);
                    }
                }
            }

            if (openCells.Count == 0)
            {
                EndGame();
                return;
            }

            _isGoldenFood = forceGolden || _random.NextDouble() < GoldenFoodChance;
            _food = openCells[_random.Next(openCells.Count)];
        }

        private void EndGame()
        {
            _isRunning = false;
            _isGameOver = true;
            _gameTimer.Stop();
            PlaySound(_gameOverSound);
            UpdateLiveStatus();
            UpdateStatus("Game over! Click to restart or press R.");
            ShowGameOverSummary();
            GameOverOverlay.Visibility = Visibility.Visible;
            Render();
        }

        private void ShowGameOverSummary()
        {
            GameOverOverlay.Width = 280;
            GameOverOverlay.Height = 185;

            var summary = new StackPanel
            {
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center
            };

            summary.Children.Add(CreateOverlayText("YOU LOST", 30, "#f87171", true));
            summary.Children.Add(CreateOverlayText("Try again!", 16, "#f8fafc", false, 5));
            summary.Children.Add(CreateOverlayText("Your score was " + _score, 14, "#cbd5e1", false, 12));
            summary.Children.Add(CreateOverlayText(
                (_isNewBestScore ? "NEW BEST! " : "") + "Your best score is " + _bestScore,
                14,
                _isNewBestScore ? "#fbbf24" : "#94a3b8",
                true,
                5));
            var changeModeButton = new Button
            {
                Content = "CHANGE MODE",
                Width = 220,
                Height = 30,
                Margin = new Thickness(0, 10, 0, 0),
                FontSize = 11,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromRgb(251, 191, 36)),
                Background = new SolidColorBrush(Color.FromRgb(66, 32, 6)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(251, 191, 36)),
                BorderThickness = new Thickness(1),
                Cursor = Cursors.Hand
            };
            changeModeButton.Click += (sender, args) => ShowModePage();
            summary.Children.Add(changeModeButton);

            GameOverOverlay.Child = summary;
        }

        private TextBlock CreateOverlayText(string text, double fontSize, string color, bool bold, double topMargin = 0)
        {
            var textBlock = new TextBlock
            {
                Text = text,
                FontSize = fontSize,
                FontWeight = bold ? FontWeights.Bold : FontWeights.Normal,
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(color)),
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, topMargin, 0, 0),
                TextAlignment = TextAlignment.Center,
                Style = (Style)FindResource("NeonTextStyle")
            };

            return textBlock;
        }

        private int LoadBestScore()
        {
            try
            {
                var path = GetBestScorePath();
                if (File.Exists(path))
                {
                    var value = File.ReadAllText(path).Trim();
                    int loadedScore;
                    if (int.TryParse(value, out loadedScore))
                    {
                        return loadedScore;
                    }
                }
            }
            catch
            {
            }

            return 0;
        }

        private int LoadWinCount()
        {
            try
            {
                var path = GetWinCountPath();
                if (File.Exists(path))
                {
                    var value = File.ReadAllText(path).Trim();
                    int loadedWins;
                    if (int.TryParse(value, out loadedWins) && loadedWins >= 0)
                    {
                        return loadedWins;
                    }
                }
            }
            catch
            {
            }

            return 0;
        }

        private void SaveBestScore()
        {
            try
            {
                var path = GetBestScorePath();
                var directory = System.IO.Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                File.WriteAllText(path, _bestScore.ToString());
            }
            catch
            {
            }
        }

        private void SaveWinCount()
        {
            try
            {
                var path = GetWinCountPath();
                var directory = System.IO.Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                File.WriteAllText(path, _winCount.ToString());
            }
            catch
            {
            }
        }

        private void ResetPreviousBestScores()
        {
            try
            {
                var appDataFolder = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                var snakeFolder = System.IO.Path.Combine(appDataFolder, "SnakeGame");
                var resetMarker = System.IO.Path.Combine(snakeFolder, "bestscore-reset-v1.txt");

                if (File.Exists(resetMarker))
                {
                    return;
                }

                Directory.CreateDirectory(snakeFolder);
                File.Delete(System.IO.Path.Combine(snakeFolder, "bestscore.txt"));
                File.Delete(System.IO.Path.Combine(snakeFolder, "bestscore-easy.txt"));
                File.Delete(System.IO.Path.Combine(snakeFolder, "bestscore-hard.txt"));
                File.Delete(System.IO.Path.Combine(snakeFolder, "bestscore-pro.txt"));
                File.WriteAllText(resetMarker, "reset");
            }
            catch
            {
            }
        }

        private string GetBestScorePath()
        {
            return GetBestScorePath(_gameMode);
        }

        private string GetBestScorePath(GameMode mode)
        {
            var appDataFolder = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var snakeFolder = System.IO.Path.Combine(appDataFolder, "SnakeGame");
            return System.IO.Path.Combine(snakeFolder, "bestscore-" + mode.ToString().ToLowerInvariant() + ".txt");
        }

        private string GetWinCountPath()
        {
            var appDataFolder = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var snakeFolder = System.IO.Path.Combine(appDataFolder, "SnakeGame");
            return System.IO.Path.Combine(snakeFolder, "wincount.txt");
        }

        private void ApplyOuterBackgroundTheme()
        {
            var stop1 = BackgroundStop1;
            var stop2 = BackgroundStop2;
            var stop3 = BackgroundStop3;

            if (stop1 == null || stop2 == null || stop3 == null)
                return;

            if (_score >= WinScore)
            {
                stop1.Color = Color.FromRgb(24, 10, 48);
                stop2.Color = Color.FromRgb(76, 29, 112);
                stop3.Color = Color.FromRgb(137, 65, 160);
            }
            else if (_score >= 50)
            {
                stop1.Color = Color.FromRgb(48, 27, 4);
                stop2.Color = Color.FromRgb(122, 70, 5);
                stop3.Color = Color.FromRgb(205, 137, 16);
            }
            else if (_score >= 35)
            {
                stop1.Color = Color.FromRgb(4, 17, 55);
                stop2.Color = Color.FromRgb(8, 56, 132);
                stop3.Color = Color.FromRgb(30, 94, 190);
            }
            else if (_score >= 20)
            {
                stop1.Color = Color.FromRgb(42, 5, 14);
                stop2.Color = Color.FromRgb(111, 12, 27);
                stop3.Color = Color.FromRgb(190, 35, 48);
            }
            else
            {
                stop1.Color = Color.FromRgb(8, 11, 28);
                stop2.Color = Color.FromRgb(17, 23, 53);
                stop3.Color = Color.FromRgb(32, 32, 82);
            }
        }

        private void ApplySpeedTier()
        {
            if (_score >= 100 && !_isProMode)
            {
                _isProMode = true;
                _proColorIndex = 0;
                StartProColorTimer();
                UpdateStatus("PRO MODE unlocked! Maximum speed and color shift active.");
            }

            int newTier = 0;

            if (_score >= 100)
                newTier = 4;
            else if (_score >= 50)
                newTier = 3;
            else if (_score >= 35)
                newTier = 2;
            else if (_score >= 20)
                newTier = 1;

            if (newTier == _currentSpeedTier)
                return;

            _currentSpeedTier = newTier;

            int interval = _gameMode == GameMode.Pro ? 90 : (_isHardMode ? 125 : 180);
            if (_currentSpeedTier >= 1) interval = _gameMode == GameMode.Pro ? 75 : (_isHardMode ? 105 : 155);
            if (_currentSpeedTier >= 2) interval = _gameMode == GameMode.Pro ? 60 : (_isHardMode ? 90 : 130);
            if (_currentSpeedTier >= 3) interval = _gameMode == GameMode.Pro ? 48 : (_isHardMode ? 75 : 110);
            if (_currentSpeedTier >= 4) interval = _gameMode == GameMode.Pro ? 38 : (_isHardMode ? 55 : 70);

            if (_gameTimer != null)
            {
                _gameTimer.Interval = TimeSpan.FromMilliseconds(interval);
            }

            if (_currentSpeedTier == 1)
                UpdateStatus("Red form unlocked! Speed increased.");
            else if (_currentSpeedTier == 2)
                UpdateStatus("Blue form unlocked! Speed increased.");
            else if (_currentSpeedTier == 3)
                UpdateStatus("Golden form unlocked! Speed increased.");
            else if (_currentSpeedTier == 4)
                UpdateStatus("PRO MODE active! Maximum speed.");

            if (_currentSpeedTier > 0 && _currentSpeedTier < 4)
            {
                PlaySound(_levelUpSound);
            }

            ApplyOuterBackgroundTheme();
        }

        private void UpdateScore()
        {
            ScoreTextBlock.Text = "Score: " + _score;
            BestTextBlock.Text = "Best: " + _bestScore;
        }

        private void UpdateStatus(string message)
        {
            StatusTextBlock.Text = message;
        }

        private void UpdateLiveStatus()
        {
            if (_isRunning)
            {
                LiveStatusTextBlock.Text = "LIVE";
                LiveStatusTextBlock.Foreground = new SolidColorBrush(Color.FromRgb(134, 239, 172));
                LiveStatusDot.Fill = new SolidColorBrush(Color.FromRgb(74, 222, 128));
            }
            else
            {
                LiveStatusTextBlock.Text = "OFFLINE";
                LiveStatusTextBlock.Foreground = new SolidColorBrush(Color.FromRgb(248, 113, 113));
                LiveStatusDot.Fill = new SolidColorBrush(Color.FromRgb(239, 68, 68));
            }
        }

        private void Render()
        {
            GameCanvas.Children.Clear();
            GameOverOverlay.Visibility = _isGameOver ? Visibility.Visible : Visibility.Collapsed;
            WinnerOverlay.Visibility = _winnerReached ? Visibility.Visible : Visibility.Collapsed;
            ApplyOuterBackgroundTheme();

            for (int x = 0; x < GridSize; x++)
            {
                for (int y = 0; y < GridSize; y++)
                {
                    Brush cellFill = new SolidColorBrush((x + y) % 2 == 0
                        ? Color.FromRgb(15, 23, 42)
                        : Color.FromRgb(17, 24, 39));
                    Brush cellStroke = new SolidColorBrush(Color.FromRgb(30, 41, 59));

                    var cell = new Rectangle
                    {
                        Width = CellSize,
                        Height = CellSize,
                        Fill = cellFill,
                        Stroke = cellStroke,
                        StrokeThickness = 1
                    };

                    Canvas.SetLeft(cell, x * CellSize);
                    Canvas.SetTop(cell, y * CellSize);
                    GameCanvas.Children.Add(cell);
                }
            }

            Brush snakeHeadBrush = Brushes.LimeGreen;
            Brush snakeBodyBrush = Brushes.ForestGreen;

            if (_isProMode)
            {
                snakeHeadBrush = _proSnakeColors[_proColorIndex];
                snakeBodyBrush = _proSnakeColors[_proColorIndex];
            }
            else if (_score >= 50)
            {
                snakeHeadBrush = Brushes.Gold;
                snakeBodyBrush = Brushes.Goldenrod;
            }
            else if (_score >= 35)
            {
                snakeHeadBrush = Brushes.Blue;
                snakeBodyBrush = Brushes.DodgerBlue;
            }
            else if (_score >= 20)
            {
                snakeHeadBrush = Brushes.Red;
                snakeBodyBrush = Brushes.DarkRed;
            }

            for (int i = 0; i < _snake.Count; i++)
            {
                var segment = _snake[i];
                var isGolden = i < _snakeGoldenSegments.Count && _snakeGoldenSegments[i];

                var bodyPart = new Rectangle
                {
                    Width = CellSize,
                    Height = CellSize,
                    Fill = isGolden ? Brushes.Gold : (segment == _snake[0] ? snakeHeadBrush : snakeBodyBrush),
                    Stroke = isGolden ? Brushes.DarkGoldenrod : Brushes.LightGreen,
                    StrokeThickness = 1
                };

                Canvas.SetLeft(bodyPart, segment.X * CellSize);
                Canvas.SetTop(bodyPart, segment.Y * CellSize);
                GameCanvas.Children.Add(bodyPart);
            }

            var foodObject = new Ellipse
            {
                Width = CellSize - 4,
                Height = CellSize - 4,
                Fill = _isGoldenFood ? Brushes.Gold : Brushes.Red,
                Stroke = _isGoldenFood ? Brushes.DarkGoldenrod : Brushes.IndianRed,
                StrokeThickness = 2
            };

            Canvas.SetLeft(foodObject, _food.X * CellSize + 2);
            Canvas.SetTop(foodObject, _food.Y * CellSize + 2);
            GameCanvas.Children.Add(foodObject);

            if (_isGoldenFood)
            {
                var glow = new Ellipse
                {
                    Width = CellSize + 10,
                    Height = CellSize + 10,
                    Fill = Brushes.Transparent,
                    Stroke = Brushes.Gold,
                    StrokeThickness = 2,
                    Opacity = 0.7
                };

                Canvas.SetLeft(glow, _food.X * CellSize - 5);
                Canvas.SetTop(glow, _food.Y * CellSize - 5);
                GameCanvas.Children.Add(glow);
            }
        }
    }
}
