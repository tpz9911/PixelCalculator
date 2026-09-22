using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Navigation;

namespace PixelCalculator
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            DataContext = new MainWindowViewModel();
        }

        private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
        {
            try
            {
                // .NET 8 中需显式设置 UseShellExecute = true 才能调用系统默认浏览器打开 URL
                Process.Start(new ProcessStartInfo
                {
                    FileName = e.Uri.AbsoluteUri,
                    UseShellExecute = true
                });
                e.Handled = true;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"打开链接失败：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void IntegerTextBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            e.Handled = !e.Text.All(char.IsDigit);
        }

        private void DecimalTextBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            TextBox textBox = sender as TextBox;
            if (textBox == null) return;

            string newText = textBox.Text.Insert(textBox.CaretIndex, e.Text);
            e.Handled = !IsTextDecimal(newText);
        }

        private bool IsTextDecimal(string text)
        {
            if (text == "." || text == ",") return true;
            return double.TryParse(text, out _);
        }

        private void TextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
        }

        // 自定义比例输入框：限制最多只能输入两位大于0的整数 (1~99)
        private void CustomRatioTextBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            if (sender is not TextBox textBox) return;

            if (!e.Text.All(char.IsDigit))
            {
                e.Handled = true;
                return;
            }

            string currentText = textBox.Text;
            int selectionStart = textBox.SelectionStart;
            int selectionLength = textBox.SelectionLength;
            string proposedText = currentText.Remove(selectionStart, selectionLength).Insert(selectionStart, e.Text);

            if (proposedText.Length > 2 || proposedText.StartsWith("0"))
            {
                e.Handled = true;
                return;
            }

            if (int.TryParse(proposedText, out int val))
            {
                if (val < 1 || val > 99)
                {
                    e.Handled = true;
                }
            }
            else
            {
                e.Handled = true;
            }
        }

        // 避免 RadioButton 内部拦截 MouseCapture 导致 TextBox 失焦
        private void CustomRatioTextBox_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is TextBox textBox)
            {
                if (textBox.DataContext is RatioItem ratioItem && DataContext is MainWindowViewModel vm)
                {
                    vm.SelectedRatio = ratioItem;
                }

                if (!textBox.IsFocused)
                {
                    textBox.Focus();
                    e.Handled = true;
                }
            }
        }

        private void CustomRatioTextBox_GotFocus(object sender, RoutedEventArgs e)
        {
            if (sender is TextBox textBox && textBox.DataContext is RatioItem ratioItem && DataContext is MainWindowViewModel vm)
            {
                vm.SelectedRatio = ratioItem;
            }
        }

        // 拦截并过滤非法剪贴板粘贴数据
        private void CustomRatioTextBox_Pasting(object sender, DataObjectPastingEventArgs e)
        {
            if (e.DataObject.GetDataPresent(DataFormats.Text))
            {
                string pasteText = (string)e.DataObject.GetData(DataFormats.Text);
                if (sender is TextBox textBox)
                {
                    string currentText = textBox.Text;
                    int selectionStart = textBox.SelectionStart;
                    int selectionLength = textBox.SelectionLength;
                    string proposedText = currentText.Remove(selectionStart, selectionLength).Insert(selectionStart, pasteText);

                    if (proposedText.Length > 2 || proposedText.StartsWith("0") || !int.TryParse(proposedText, out int val) || val < 1 || val > 99)
                    {
                        e.CancelCommand();
                    }
                }
            }
            else
            {
                e.CancelCommand();
            }
        }
    }

    /// <summary>
    /// WPF ViewModel 数据驱动基础类
    /// </summary>
    public class ObservableObject : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    /// <summary>
    /// 存储比例信息及动态矩形渲染的高级项
    /// </summary>
    public class RatioItem : ObservableObject
    {
        private string _name;
        private double _widthRatio = 1;
        private double _heightRatio = 1;
        private string _customWidthText;
        private string _customHeightText;

        public bool IsCustom { get; set; }

        public string Name
        {
            get => _name;
            set { _name = value; OnPropertyChanged(); }
        }

        public double WidthRatio
        {
            get => _widthRatio;
            set
            {
                if (Math.Abs(_widthRatio - value) > 0.0001)
                {
                    _widthRatio = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(DisplayWidth));
                    OnPropertyChanged(nameof(DisplayHeight));
                }
            }
        }

        public double HeightRatio
        {
            get => _heightRatio;
            set
            {
                if (Math.Abs(_heightRatio - value) > 0.0001)
                {
                    _heightRatio = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(DisplayWidth));
                    OnPropertyChanged(nameof(DisplayHeight));
                }
            }
        }

        public string CustomWidthText
        {
            get => _customWidthText;
            set
            {
                if (_customWidthText == value) return;
                _customWidthText = value;
                OnPropertyChanged();
                UpdateCustomRatio();
                OnPropertyChanged(nameof(DisplayWidth));
                OnPropertyChanged(nameof(DisplayHeight));
            }
        }

        public string CustomHeightText
        {
            get => _customHeightText;
            set
            {
                if (_customHeightText == value) return;
                _customHeightText = value;
                OnPropertyChanged();
                UpdateCustomRatio();
                OnPropertyChanged(nameof(DisplayWidth));
                OnPropertyChanged(nameof(DisplayHeight));
            }
        }

        public Action OnRatioChanged { get; set; }

        private void UpdateCustomRatio()
        {
            if (int.TryParse(_customWidthText, out int w) && w > 0 &&
                int.TryParse(_customHeightText, out int h) && h > 0)
            {
                WidthRatio = w;
                HeightRatio = h;
                OnRatioChanged?.Invoke();
            }
        }

        // 当自定义项尚未同时填入两者时，保持正方形占位；填完后动态按比例缩放
        public double DisplayWidth
        {
            get
            {
                if (IsCustom && (string.IsNullOrWhiteSpace(_customWidthText) || string.IsNullOrWhiteSpace(_customHeightText)))
                {
                    return 24;
                }
                return WidthRatio >= HeightRatio ? 24 : Math.Max(2, 24 * (WidthRatio / HeightRatio));
            }
        }

        public double DisplayHeight
        {
            get
            {
                if (IsCustom && (string.IsNullOrWhiteSpace(_customWidthText) || string.IsNullOrWhiteSpace(_customHeightText)))
                {
                    return 24;
                }
                return HeightRatio >= WidthRatio ? 24 : Math.Max(2, 24 * (HeightRatio / WidthRatio));
            }
        }
    }

    /// <summary>
    /// 主界面的核心计算视图模型
    /// </summary>
    public class MainWindowViewModel : ObservableObject
    {
        private enum InputSource
        {
            Width,
            Height,
            Megapixels
        }

        private RatioItem _selectedRatio;
        private int? _width;
        private int? _height;
        private string _megapixelsRaw;
        private string _totalPixelsDisplay = "-";
        private bool _isUpdating;
        private InputSource _lastSource = InputSource.Width;

        public List<RatioItem> Ratios { get; }

        public MainWindowViewModel()
        {
            Ratios = new List<RatioItem>
            {
                new RatioItem { Name = "1:1", WidthRatio = 1, HeightRatio = 1 },
                new RatioItem { Name = "16:9", WidthRatio = 16, HeightRatio = 9 },
                new RatioItem { Name = "9:16", WidthRatio = 9, HeightRatio = 16 },
                new RatioItem { Name = "4:3", WidthRatio = 4, HeightRatio = 3 },
                new RatioItem { Name = "3:4", WidthRatio = 3, HeightRatio = 4 },
                new RatioItem { Name = "3:2", WidthRatio = 3, HeightRatio = 2 },
                new RatioItem { Name = "2:3", WidthRatio = 2, HeightRatio = 3 }
            };

            // 最下方加入自定义比例项
            var customItem = new RatioItem
            {
                Name = "自定义",
                IsCustom = true,
                WidthRatio = 1,
                HeightRatio = 1
            };
            customItem.OnRatioChanged = () =>
            {
                if (SelectedRatio == customItem)
                {
                    OnSelectedRatioChanged();
                }
            };
            Ratios.Add(customItem);

            SelectedRatio = Ratios[1];
        }

        public RatioItem SelectedRatio
        {
            get => _selectedRatio;
            set
            {
                if (_selectedRatio == value) return;
                _selectedRatio = value;
                OnPropertyChanged(nameof(SelectedRatio));
                OnSelectedRatioChanged();
            }
        }

        public int? Width
        {
            get => _width;
            set
            {
                if (_width == value) return;
                _width = value;
                OnPropertyChanged(nameof(Width));

                if (!_isUpdating)
                {
                    _isUpdating = true;
                    _lastSource = InputSource.Width;
                    try
                    {
                        CalculateFromWidth();
                    }
                    finally
                    {
                        _isUpdating = false;
                    }
                }
            }
        }

        public int? Height
        {
            get => _height;
            set
            {
                if (_height == value) return;
                _height = value;
                OnPropertyChanged(nameof(Height));

                if (!_isUpdating)
                {
                    _isUpdating = true;
                    _lastSource = InputSource.Height;
                    try
                    {
                        CalculateFromHeight();
                    }
                    finally
                    {
                        _isUpdating = false;
                    }
                }
            }
        }

        public string MegapixelsRaw
        {
            get => _megapixelsRaw;
            set
            {
                if (_megapixelsRaw == value) return;
                _megapixelsRaw = value;
                OnPropertyChanged(nameof(MegapixelsRaw));

                if (!_isUpdating)
                {
                    _isUpdating = true;
                    _lastSource = InputSource.Megapixels;
                    try
                    {
                        CalculateFromMegapixels();
                    }
                    finally
                    {
                        _isUpdating = false;
                    }
                }
            }
        }

        public string TotalPixelsDisplay
        {
            get => _totalPixelsDisplay;
            set
            {
                if (_totalPixelsDisplay == value) return;
                _totalPixelsDisplay = value;
                OnPropertyChanged(nameof(TotalPixelsDisplay));
            }
        }

        private void OnSelectedRatioChanged()
        {
            if (_isUpdating) return;

            _isUpdating = true;
            try
            {
                switch (_lastSource)
                {
                    case InputSource.Megapixels:
                        if (double.TryParse(_megapixelsRaw, out double mp) && mp > 0)
                        {
                            CalculateFromMegapixels();
                        }
                        else if (_width.HasValue)
                        {
                            CalculateFromWidth();
                        }
                        break;

                    case InputSource.Height:
                        if (_height.HasValue)
                        {
                            CalculateFromHeight();
                        }
                        else if (_width.HasValue)
                        {
                            CalculateFromWidth();
                        }
                        break;

                    case InputSource.Width:
                    default:
                        if (_width.HasValue)
                        {
                            CalculateFromWidth();
                        }
                        else if (_height.HasValue)
                        {
                            CalculateFromHeight();
                        }
                        else if (double.TryParse(_megapixelsRaw, out double mpVal) && mpVal > 0)
                        {
                            CalculateFromMegapixels();
                        }
                        break;
                }
            }
            finally
            {
                _isUpdating = false;
            }
        }

        private void CalculateFromWidth()
        {
            if (SelectedRatio != null && _width.HasValue && _width.Value > 0)
            {
                int calculatedHeight = (int)Math.Round((double)_width.Value * SelectedRatio.HeightRatio / SelectedRatio.WidthRatio);
                _height = calculatedHeight;
                OnPropertyChanged(nameof(Height));

                double mp = (double)_width.Value * calculatedHeight / 1_000_000.0;
                _megapixelsRaw = mp.ToString("0.###");
                OnPropertyChanged(nameof(MegapixelsRaw));
            }
            else if (!_width.HasValue)
            {
                _height = null;
                _megapixelsRaw = string.Empty;
                OnPropertyChanged(nameof(Height));
                OnPropertyChanged(nameof(MegapixelsRaw));
            }

            UpdateTotalPixelsDisplay();
        }

        private void CalculateFromHeight()
        {
            if (SelectedRatio != null && _height.HasValue && _height.Value > 0)
            {
                int calculatedWidth = (int)Math.Round((double)_height.Value * SelectedRatio.WidthRatio / SelectedRatio.HeightRatio);
                _width = calculatedWidth;
                OnPropertyChanged(nameof(Width));

                double mp = (double)calculatedWidth * _height.Value / 1_000_000.0;
                _megapixelsRaw = mp.ToString("0.###");
                OnPropertyChanged(nameof(MegapixelsRaw));
            }
            else if (!_height.HasValue)
            {
                _width = null;
                _megapixelsRaw = string.Empty;
                OnPropertyChanged(nameof(Width));
                OnPropertyChanged(nameof(MegapixelsRaw));
            }

            UpdateTotalPixelsDisplay();
        }

        private void CalculateFromMegapixels()
        {
            if (string.IsNullOrWhiteSpace(_megapixelsRaw))
            {
                _width = null;
                _height = null;
                OnPropertyChanged(nameof(Width));
                OnPropertyChanged(nameof(Height));
                UpdateTotalPixelsDisplay();
                return;
            }

            if (SelectedRatio != null && double.TryParse(_megapixelsRaw, out double mp) && mp > 0)
            {
                double totalPixels = mp * 1_000_000.0;
                double ratio = SelectedRatio.WidthRatio / SelectedRatio.HeightRatio;

                _width = (int)Math.Round(Math.Sqrt(totalPixels * ratio));
                _height = (int)Math.Round(Math.Sqrt(totalPixels / ratio));
                OnPropertyChanged(nameof(Width));
                OnPropertyChanged(nameof(Height));
            }

            UpdateTotalPixelsDisplay();
        }

        private void UpdateTotalPixelsDisplay()
        {
            if (_width.HasValue && _height.HasValue && _width.Value > 0 && _height.Value > 0)
            {
                long total = (long)_width.Value * _height.Value;
                TotalPixelsDisplay = $"{total:N0} 像素";
            }
            else
            {
                TotalPixelsDisplay = "-";
            }
        }
    }
}