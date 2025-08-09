using BanYodo.Models;

namespace BanYodo.Forms.Controls
{
    public class AccountDataGridView : UserControl
    {
        private DataGridView _dataGridView;
        private List<Account> _accounts;

        public AccountDataGridView()
        {
            _accounts = new List<Account>();
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            _dataGridView = new DataGridView
            {
                Dock = DockStyle.Fill,
                AllowUserToAddRows = true,
                AllowUserToDeleteRows = true,
                AutoGenerateColumns = false,
                EditMode = DataGridViewEditMode.EditOnEnter,
                SelectionMode = DataGridViewSelectionMode.CellSelect
            };

            SetupColumns();
            
            _dataGridView.CellEndEdit += DataGridView_CellEndEdit;
            _dataGridView.CellClick += DataGridView_CellClick;
            _dataGridView.CellPainting += DataGridView_CellPainting;
            
            this.Controls.Add(_dataGridView);
        }

        private void SetupColumns()
        {
            // Account & Card Info column (combined)
            var accountCardColumn = new DataGridViewTextBoxColumn
            {
                Name = "AccountCard",
                HeaderText = "Username Password Card Month Year CVV",
                Width = 500
            };

            // Proxy column
            var proxyColumn = new DataGridViewTextBoxColumn
            {
                Name = "Proxy",
                HeaderText = "Proxy",
                Width = 300
            };

            // Status column
            var statusColumn = new DataGridViewTextBoxColumn
            {
                Name = "Status",
                HeaderText = "Status",
                Width = 100,
                ReadOnly = true
            };

            // Action button column
            var actionColumn = new DataGridViewButtonColumn
            {
                Name = "Action",
                HeaderText = "Action",
                Width = 80,
                Text = "Start",
                UseColumnTextForButtonValue = false
            };

            // Remove button column
            var removeColumn = new DataGridViewButtonColumn
            {
                Name = "Remove",
                HeaderText = "Remove",
                Width = 80,
                Text = "Remove",
                UseColumnTextForButtonValue = false
            };

            _dataGridView.Columns.AddRange(new DataGridViewColumn[]
            {
                accountCardColumn, proxyColumn, statusColumn, actionColumn, removeColumn
            });
        }

        public void LoadAccounts(List<Account> accounts)
        {
            _accounts = accounts ?? new List<Account>();
            RefreshData();
        }

        public void AddAccount(Account account)
        {
            if (account != null)
            {
                _accounts.Add(account);
                RefreshData();
            }
        }

        public void RemoveAccount(Account account)
        {
            if (account != null)
            {
                _accounts.Remove(account);
                RefreshData();
            }
        }

        public void ClearAllAccounts()
        {
            _accounts.Clear();
            RefreshData();
            OnAllAccountsCleared?.Invoke();
        }

        public Account? GetSelectedAccount()
        {
            if (_dataGridView.CurrentCell != null && _dataGridView.CurrentCell.RowIndex >= 0)
            {
                var selectedRow = _dataGridView.Rows[_dataGridView.CurrentCell.RowIndex];
                return selectedRow.Tag as Account;
            }
            return null;
        }

        public void RefreshAccountStatus(Account account)
        {
            foreach (DataGridViewRow row in _dataGridView.Rows)
            {
                if (row.Tag == account)
                {
                    row.Cells["Status"].Value = account.Status.ToString();
                    row.Cells["Action"].Value = account.IsRunning ? "Stop" : "Start";
                    
                    // Invalidate the action cell to trigger repaint
                    _dataGridView.InvalidateCell(row.Cells["Action"]);
                    
                    break;
                }
            }
        }

        private void RefreshData()
        {
            var currentRowIndex = _dataGridView.CurrentCell?.RowIndex ?? 0;
            currentRowIndex = currentRowIndex > 0 ? currentRowIndex - 1 : currentRowIndex;

            _dataGridView.Rows.Clear();

            foreach (var account in _accounts)
            {
                var rowIndex = _dataGridView.Rows.Add();
                var row = _dataGridView.Rows[rowIndex];

                // Combine account and card info in one field
                var accountCardInfo = $"{account.Username} {account.Password} {account.Card} {account.CardMonth} {account.CardYear} {account.CardCvv}";

                row.Cells["AccountCard"].Value = accountCardInfo;
                row.Cells["Proxy"].Value = account.Proxy;
                row.Cells["Status"].Value = account.Status.ToString();
                row.Cells["Action"].Value = account.IsRunning ? "Stop" : "Start";

                // Store reference to account in Tag
                row.Tag = account;
            }

            // set current rowIndex
            _dataGridView.CurrentCell = _dataGridView.Rows[currentRowIndex].Cells[0];
        }

        private void DataGridView_CellEndEdit(object? sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex >= 0 && e.RowIndex < _dataGridView.Rows.Count)
            {
                var row = _dataGridView.Rows[e.RowIndex];
                var account = row.Tag as Account;
                
                // If account is null (new row), create a new account
                if (account == null)
                {
                    account = new Account();
                    row.Tag = account;
                    _accounts.Add(account);
                }
                
                var cell = row.Cells[e.ColumnIndex];
                var value = cell.Value?.ToString() ?? string.Empty;

                switch (_dataGridView.Columns[e.ColumnIndex].Name)
                {
                    case "AccountCard":
                        // Parse combined account and card info
                        var parts = value.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                        if (parts.Length >= 6)
                        {
                            account.Username = parts[0];
                            account.Password = parts[1];
                            account.Card = parts[2];
                            account.CardMonth = parts[3];
                            account.CardYear = parts[4];
                            account.CardCvv = parts[5];
                        }
                        else if (parts.Length >= 2)
                        {
                            account.Username = parts[0];
                            account.Password = parts[1];
                            // Clear card info if not provided
                            if (parts.Length < 3) account.Card = string.Empty;
                            if (parts.Length < 4) account.CardMonth = string.Empty;
                            if (parts.Length < 5) account.CardYear = string.Empty;
                            if (parts.Length < 6) account.CardCvv = string.Empty;
                        }
                        break;
                    case "Proxy":
                        account.Proxy = value;
                        break;
                }
                
                // Update status and action cells for new account
                row.Cells["Status"].Value = account.Status.ToString();
                row.Cells["Action"].Value = account.IsRunning ? "Stop" : "Start";
                
                // Invalidate cells to trigger repaint
                _dataGridView.InvalidateCell(row.Cells["Action"]);
                _dataGridView.InvalidateCell(row.Cells["Remove"]);
                
                // Trigger save event
                OnAccountChanged?.Invoke(account);
            }
        }

        private void DataGridView_CellClick(object? sender, DataGridViewCellEventArgs e)
        {
            if (e.ColumnIndex >= 0 && e.RowIndex >= 0)
            {
                var columnName = _dataGridView.Columns[e.ColumnIndex].Name;
                var row = _dataGridView.Rows[e.RowIndex];
                var account = row.Tag as Account;
                
                if (account != null)
                {
                    if (columnName == "Action")
                    {
                        OnAccountActionClicked?.Invoke(account);
                    }
                    else if (columnName == "Remove")
                    {
                        OnAccountRemoveClicked?.Invoke(account);
                    }
                }
            }
        }

        private void DataGridView_CellPainting(object? sender, DataGridViewCellPaintingEventArgs e)
        {
            // Paint action and remove column buttons
            if (e.ColumnIndex >= 0 && e.RowIndex >= 0 && e.Graphics != null)
            {
                var columnName = _dataGridView.Columns[e.ColumnIndex].Name;
                
                if (columnName == "Action" || columnName == "Remove")
                {
                    e.Paint(e.CellBounds, DataGridViewPaintParts.All);

                    var account = _dataGridView.Rows[e.RowIndex].Tag as Account;
                    if (account != null)
                    {
                        // Determine button color and text
                        Color buttonColor;
                        string buttonText;

                        if (columnName == "Action")
                        {
                            if (account.IsRunning)
                            {
                                buttonColor = Color.FromArgb(231, 76, 60); // Red for Stop
                                buttonText = "Stop";
                            }
                            else
                            {
                                buttonColor = Color.FromArgb(46, 204, 113); // Green for Start
                                buttonText = "Start";
                            }
                        }
                        else // Remove column
                        {
                            buttonColor = Color.FromArgb(231, 76, 60); // Red for Remove
                            buttonText = "Remove";
                        }

                        // Create button rectangle with some padding
                        var buttonRect = new Rectangle(
                            e.CellBounds.X + 2,
                            e.CellBounds.Y + 2,
                            e.CellBounds.Width - 4,
                            e.CellBounds.Height - 4);

                        // Fill button background
                        using (var brush = new SolidBrush(buttonColor))
                        {
                            e.Graphics.FillRectangle(brush, buttonRect);
                        }

                        // Draw button border
                        using (var pen = new Pen(Color.FromArgb(52, 73, 94), 1))
                        {
                            e.Graphics.DrawRectangle(pen, buttonRect);
                        }

                        // Draw button text
                        using (var font = new Font("Segoe UI", 8F, FontStyle.Bold))
                        using (var brush = new SolidBrush(Color.White))
                        {
                            var textSize = e.Graphics.MeasureString(buttonText, font);
                            var textRect = new PointF(
                                buttonRect.X + (buttonRect.Width - textSize.Width) / 2,
                                buttonRect.Y + (buttonRect.Height - textSize.Height) / 2);

                            e.Graphics.DrawString(buttonText, font, brush, textRect);
                        }
                    }

                    e.Handled = true;
                }
            }
        }

        public event Action<Account>? OnAccountActionClicked;
        public event Action<Account>? OnAccountChanged;
        public event Action<Account>? OnAccountRemoveClicked;
        public event Action? OnAllAccountsCleared;
    }
}
