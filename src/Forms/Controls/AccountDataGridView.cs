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
            
            this.Controls.Add(_dataGridView);
        }

        private void SetupColumns()
        {
            // Account & Card Info column (combined)
            var accountCardColumn = new DataGridViewTextBoxColumn
            {
                Name = "AccountCard",
                HeaderText = "Account & Card (Username Password Card Month Year CVV)",
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
                Width = 100,
                Text = "Start",
                UseColumnTextForButtonValue = false
            };

            _dataGridView.Columns.AddRange(new DataGridViewColumn[]
            {
                accountCardColumn, proxyColumn, statusColumn, actionColumn
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
                    break;
                }
            }
        }

        private void RefreshData()
        {
            // get current rowIndex
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
                
                if (account != null)
                {
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
                    
                    // Trigger save event
                    OnAccountChanged?.Invoke(account);
                }
            }
        }

        private void DataGridView_CellClick(object? sender, DataGridViewCellEventArgs e)
        {
            if (e.ColumnIndex >= 0 && e.RowIndex >= 0 && 
                _dataGridView.Columns[e.ColumnIndex].Name == "Action")
            {
                var row = _dataGridView.Rows[e.RowIndex];
                var account = row.Tag as Account;
                
                if (account != null)
                {
                    OnAccountActionClicked?.Invoke(account);
                }
            }
        }

        public event Action<Account>? OnAccountActionClicked;
        public event Action<Account>? OnAccountChanged;
    }
}
