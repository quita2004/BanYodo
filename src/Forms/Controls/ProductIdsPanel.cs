using BanYodo.Models;

namespace BanYodo.Forms.Controls
{
    public class ProductIdsPanel : UserControl
    {
        private Configuration? _configuration;
        private TextBox _productIdsTextBox = null!;
        private Button _saveButton = null!;
        private Label _countLabel = null!;

        public ProductIdsPanel()
        {
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            this.Size = new Size(400, 180);
            this.BorderStyle = BorderStyle.FixedSingle;

            // Title
            var titleLabel = new Label
            {
                Text = "Product IDs",
                Font = new Font("Segoe UI", 9, FontStyle.Bold),
                Location = new Point(10, 8),
                Size = new Size(100, 18)
            };

            // Multi-line TextBox for Product IDs
            _productIdsTextBox = new TextBox
            {
                Multiline = true,
                ScrollBars = ScrollBars.Vertical,
                Location = new Point(10, 30),
                Size = new Size(370, 100),
                Font = new Font("Consolas", 8),
                PlaceholderText = "PROD001\nPROD002\nPROD003"
            };

            // Count label
            _countLabel = new Label
            {
                Text = "0 Product IDs",
                Location = new Point(10, 140),
                Size = new Size(150, 18),
                Font = new Font("Segoe UI", 8),
                ForeColor = Color.Blue
            };

            // Save button
            _saveButton = new Button
            {
                Text = "Save",
                Location = new Point(305, 137),
                Size = new Size(75, 23),
                UseVisualStyleBackColor = true
            };

            // Event handlers
            _productIdsTextBox.TextChanged += ProductIdsTextBox_TextChanged;
            _saveButton.Click += SaveButton_Click;

            this.Controls.AddRange(new Control[]
            {
                titleLabel, _productIdsTextBox, _countLabel, _saveButton
            });
        }

        public void LoadProductIds(List<string> productIds)
        {
            if (productIds != null && productIds.Any())
            {
                _productIdsTextBox.Text = string.Join(Environment.NewLine, productIds);
            }
            else
            {
                _productIdsTextBox.Text = string.Empty;
            }
            UpdateCount();
        }

        public void SetConfiguration(Configuration configuration)
        {
            _configuration = configuration;
        }

        private void ProductIdsTextBox_TextChanged(object? sender, EventArgs e)
        {
            UpdateCount();
        }

        private void UpdateCount()
        {
            var lines = GetProductIds();
            _countLabel.Text = $"{lines.Count} Product ID{(lines.Count != 1 ? "s" : "")}";
        }

        private List<string> GetProductIds()
        {
            if (string.IsNullOrWhiteSpace(_productIdsTextBox.Text))
                return new List<string>();

            return _productIdsTextBox.Text
                .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(line => line.Trim())
                .Where(line => !string.IsNullOrWhiteSpace(line))
                .Distinct()
                .ToList();
        }

        private void SaveButton_Click(object? sender, EventArgs e)
        {
            if (_configuration != null)
            {
                // Clear existing product IDs
                _configuration.ProductIds.Clear();

                // Add new product IDs
                var productIds = GetProductIds();
                foreach (var productId in productIds)
                {
                    _configuration.AddProductId(productId);
                }

                OnProductIdsChanged?.Invoke();
                
                // Visual feedback
                _saveButton.Text = "Saved!";
                _saveButton.Enabled = false;
                
                var timer = new System.Windows.Forms.Timer();
                timer.Interval = 1000;
                timer.Tick += (s, ev) =>
                {
                    _saveButton.Text = "Save";
                    _saveButton.Enabled = true;
                    timer.Stop();
                    timer.Dispose();
                };
                timer.Start();
            }
        }

        public event Action? OnProductIdsChanged;
    }
}
