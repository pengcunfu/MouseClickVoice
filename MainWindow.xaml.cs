using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using System.Windows.Input;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using MessageBox = System.Windows.MessageBox;
using MessageBoxButton = System.Windows.MessageBoxButton;
using MessageBoxImage = System.Windows.MessageBoxImage;

namespace MouseClickVoice
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private MouseHook? _mouseHook;
        private AudioCapture? _audioCapture;
        private SpeechRecognizer? _speechRecognizer;
        private TextSimulator? _textSimulator;
        private bool _isRecording;
        private bool _isMouseDown;
        private readonly DispatcherTimer _statusTimer;
        private readonly Config _config;

        public MainWindow()
        {
            _config = Config.Instance;
            InitializeComponent(); // 这会自动调用InitializeComponents()
            LoadUserSettings(); // 重命名避免冲突
            InitializeServices();

            _statusTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(0.1)
            };
            _statusTimer.Tick += UpdateStatus;
            _statusTimer.Start();
        }

        private void LoadUserSettings()
        {
            // 从配置加载设置，使用延迟加载避免初始化顺序问题
            if (_config != null)
            {
                // 延迟设置以确保UI组件已完全初始化
                Dispatcher.BeginInvoke(() =>
                {
                    try
                    {
                        if (LongPressSlider != null && LongPressValueText != null)
                        {
                            LongPressSlider.Value = _config.LongPressDuration;
                            LongPressValueText.Text = $"{_config.LongPressDuration:F1}s";
                        }

                        if (LanguageComboBox != null)
                        {
                            foreach (ComboBoxItem item in LanguageComboBox.Items)
                            {
                                if (item.Tag?.ToString() == _config.RecognitionLanguage)
                                {
                                    LanguageComboBox.SelectedItem = item;
                                    break;
                                }
                            }
                        }

                        if (ShowNotificationsCheckBox != null && UseClipboardCheckBox != null)
                        {
                            ShowNotificationsCheckBox.IsChecked = _config.ShowNotifications;
                            UseClipboardCheckBox.IsChecked = _config.UseClipboard;
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"加载设置失败: {ex.Message}");
                    }
                });
            }
        }

        private void InitializeServices()
        {
            try
            {
                _mouseHook = new MouseHook();
                _mouseHook.MousePressed += OnMousePressed;
                _mouseHook.MouseReleased += OnMouseReleased;
                _mouseHook.LongPressDetected += OnLongPressDetected;

                _audioCapture = new AudioCapture();
                _audioCapture.StatusChanged += OnAudioStatusChanged;

                _speechRecognizer = new SpeechRecognizer();
                _speechRecognizer.TextRecognized += OnTextRecognized;
                _speechRecognizer.StatusChanged += OnRecognitionStatusChanged;
                _speechRecognizer.Error += OnSpeechError;

                _textSimulator = new TextSimulator(_config.TypingDelay);

                RecognitionStatusText.Text = "已初始化";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"初始化服务失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void StartButton_Click(object sender, RoutedEventArgs e)
        {
            StartService();
        }

        private void StopButton_Click(object sender, RoutedEventArgs e)
        {
            StopService();
        }

        private async Task StartService()
        {
            try
            {
                _mouseHook?.Start();
                await Task.Delay(100);

                StartButton.IsEnabled = false;
                StopButton.IsEnabled = true;

                ShowNotification("服务已启动", "按住鼠标左键1.5秒开始语音输入");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"启动服务失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void StopService()
        {
            try
            {
                _mouseHook?.Stop();
                _audioCapture?.StopRecording();
                _speechRecognizer?.StopListening();

                _isRecording = false;
                _isMouseDown = false;

                StartButton.IsEnabled = true;
                StopButton.IsEnabled = false;

                ShowNotification("服务已停止", "语音输入功能已关闭");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"停止服务失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void OnMousePressed(object? sender, MouseEventArgs e)
        {
            _isMouseDown = true;
            MouseStatusText.Text = "按下";
        }

        private void OnMouseReleased(object? sender, MouseEventArgs e)
        {
            _isMouseDown = false;
            MouseStatusText.Text = "释放";

            if (_isRecording)
            {
                StopRecording();
            }
        }

        private async void OnLongPressDetected(object? sender, MouseEventArgs e)
        {
            if (_isMouseDown && !_isRecording)
            {
                await StartRecording();
            }
        }

        private async Task StartRecording()
        {
            try
            {
                _isRecording = true;
                _audioCapture?.StartRecording(_config.SampleRate, _config.Channels, _config.BitDepth);
                _speechRecognizer?.StartListening();
            }
            catch (Exception ex)
            {
                ShowNotification("录音启动失败", ex.Message);
                _isRecording = false;
            }
        }

        private void StopRecording()
        {
            try
            {
                _isRecording = false;
                _audioCapture?.StopRecording();
                _speechRecognizer?.StopListening();
            }
            catch (Exception ex)
            {
                ShowNotification("录音停止失败", ex.Message);
            }
        }

        private async void OnTextRecognized(object? sender, string text)
        {
            if (string.IsNullOrEmpty(text))
                return;

            await Dispatcher.InvokeAsync(() =>
            {
                LastRecognizedText.Text = text;
            });

            try
            {
                if (_config.UseClipboard)
                {
                    _textSimulator?.InsertText(text);
                }
                else
                {
                    await _textSimulator?.TypeTextAsync(text)!;
                }

                ShowNotification("文字输入完成", text);
            }
            catch (Exception ex)
            {
                ShowNotification("文字输入失败", ex.Message);
            }
        }

        private void OnAudioStatusChanged(object? sender, string status)
        {
            Dispatcher.InvokeAsync(() =>
            {
                RecordingStatusText.Text = status;
            });
        }

        private void OnRecognitionStatusChanged(object? sender, string status)
        {
            Dispatcher.InvokeAsync(() =>
            {
                RecognitionStatusText.Text = status;
            });
        }

        private void OnSpeechError(object? sender, Exception error)
        {
            Dispatcher.InvokeAsync(() =>
            {
                ShowNotification("语音识别错误", error.Message);
            });
        }

        private void UpdateStatus(object? sender, EventArgs e)
        {
            if (!_isMouseDown && MouseStatusText.Text != "等待中...")
            {
                MouseStatusText.Text = "等待中...";
            }
        }

        private void LongPressSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (LongPressValueText != null)
            {
                LongPressValueText.Text = $"{e.NewValue:F1}s";
                _config.LongPressDuration = e.NewValue;
                _config.Save();
            }
        }

        private void LanguageComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                if (LanguageComboBox?.SelectedItem is ComboBoxItem item && item.Tag != null)
                {
                    _config.RecognitionLanguage = item.Tag.ToString()!;
                    _config.Save();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"语言选择错误: {ex.Message}");
            }
        }

        private void ShowNotificationsCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            _config.ShowNotifications = ShowNotificationsCheckBox.IsChecked == true;
            _config.Save();
        }

        private void UseClipboardCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            _config.UseClipboard = UseClipboardCheckBox.IsChecked == true;
            _config.Save();
        }

        private void ShowNotification(string title, string message)
        {
            if (_config.ShowNotifications)
            {
                // 这里可以使用Windows通知API
                System.Diagnostics.Debug.WriteLine($"{title}: {message}");
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            try
            {
                StopService();
                _statusTimer?.Stop();

                _mouseHook?.Dispose();
                _audioCapture?.Dispose();
                _speechRecognizer?.Dispose();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"清理资源时出错: {ex.Message}");
            }

            base.OnClosed(e);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                Close();
            }
            base.OnKeyDown(e);
        }
    }
}