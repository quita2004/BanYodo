using BanYodo.Models;

namespace BanYodo.Forms.Controls
{
    public class PurchaseModePanel : UserControl
    {
        private Configuration? _configuration;
        private RadioButton _scanModeRadio = null!;
        private RadioButton _fixedTimeModeRadio = null!;
        private DateTimePicker _fixedTimePicker = null!;
        private NumericUpDown _scanIntervalNumeric = null!;

        public PurchaseModePanel()
        {
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            this.Size = new Size(300, 180);
            this.BorderStyle = BorderStyle.FixedSingle;

            // Title
            var titleLabel = new Label
            {
                Text = "Kiểu mua hàng",
                Font = new Font("Segoe UI", 9, FontStyle.Bold),
                Location = new Point(10, 8),
                Size = new Size(120, 18)
            };

            // Scan Mode
            _scanModeRadio = new RadioButton
            {
                Text = "Scan Mode",
                Location = new Point(10, 30),
                Size = new Size(100, 18),
                Checked = true
            };

            // Scan interval input
            var scanIntervalLabel = new Label
            {
                Text = "Scan every:",
                Location = new Point(30, 50),
                Size = new Size(70, 18),
                Font = new Font("Segoe UI", 8)
            };

            _scanIntervalNumeric = new NumericUpDown
            {
                Location = new Point(105, 48),
                Size = new Size(50, 20),
                Minimum = 1,
                Maximum = 300,
                Value = 5,
                DecimalPlaces = 0
            };

            var secondsLabel = new Label
            {
                Text = "seconds",
                Location = new Point(160, 50),
                Size = new Size(50, 18),
                Font = new Font("Segoe UI", 8)
            };

            // Fixed Time Mode
            _fixedTimeModeRadio = new RadioButton
            {
                Text = "Fixed Time Mode",
                Location = new Point(10, 85),
                Size = new Size(130, 18)
            };

            _fixedTimePicker = new DateTimePicker
            {
                Format = DateTimePickerFormat.Custom,
                CustomFormat = "HH:mm:ss",
                ShowUpDown = true,
                Location = new Point(30, 105),
                Size = new Size(90, 23),
                Enabled = false
            };

            // Event handlers
            _scanModeRadio.CheckedChanged += ScanModeRadio_CheckedChanged;
            _fixedTimeModeRadio.CheckedChanged += FixedTimeModeRadio_CheckedChanged;
            _fixedTimePicker.ValueChanged += FixedTimePicker_ValueChanged;
            _scanIntervalNumeric.ValueChanged += ScanIntervalNumeric_ValueChanged;

            this.Controls.AddRange(new Control[]
            {
                titleLabel, _scanModeRadio, scanIntervalLabel, _scanIntervalNumeric, 
                secondsLabel, _fixedTimeModeRadio, _fixedTimePicker
            });
        }

        public void LoadConfiguration(Configuration configuration)
        {
            _configuration = configuration;
            
            if (configuration.PurchaseMode == PurchaseMode.FixedTime)
            {
                _fixedTimeModeRadio.Checked = true;
                _fixedTimePicker.Enabled = true;
                _scanIntervalNumeric.Enabled = false;
                
                if (configuration.FixedTime.HasValue)
                {
                    _fixedTimePicker.Value = configuration.FixedTime.Value;
                }
            }
            else
            {
                _scanModeRadio.Checked = true;
                _fixedTimePicker.Enabled = false;
                _scanIntervalNumeric.Enabled = true;
                _scanIntervalNumeric.Value = configuration.ScanSecond.HasValue 
                    ? (decimal)configuration.ScanSecond.Value 
                    : 5; // Default value if not set
            }
        }

        private void ScanModeRadio_CheckedChanged(object? sender, EventArgs e)
        {
            if (_scanModeRadio.Checked && _configuration != null)
            {
                _configuration.PurchaseMode = PurchaseMode.ScanMode;
                _fixedTimePicker.Enabled = false;
                _scanIntervalNumeric.Enabled = true;
                OnConfigurationChanged?.Invoke();
            }
        }

        private void FixedTimeModeRadio_CheckedChanged(object? sender, EventArgs e)
        {
            if (_fixedTimeModeRadio.Checked && _configuration != null)
            {
                _configuration.PurchaseMode = PurchaseMode.FixedTime;
                _fixedTimePicker.Enabled = true;
                _scanIntervalNumeric.Enabled = false;
                _configuration.FixedTime = _fixedTimePicker.Value;
                OnConfigurationChanged?.Invoke();
            }
        }

        private void FixedTimePicker_ValueChanged(object? sender, EventArgs e)
        {
            if (_configuration != null && _fixedTimeModeRadio.Checked)
            {
                _configuration.FixedTime = _fixedTimePicker.Value;
                OnConfigurationChanged?.Invoke();
            }
        }

        private void ScanIntervalNumeric_ValueChanged(object? sender, EventArgs e)
        {
            if (_configuration != null && _scanModeRadio.Checked)
            {
                // Store scan interval in configuration if needed
                OnConfigurationChanged?.Invoke();
                _configuration.ScanSecond = (uint)_scanIntervalNumeric.Value;
                if (_configuration.ScanSecond < 1)
                {
                    _configuration.ScanSecond = 1; // Ensure minimum value
                }
            }
        }

        public int GetScanInterval()
        {
            return (int)_scanIntervalNumeric.Value;
        }

        public event Action? OnConfigurationChanged;
    }
}
