using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace AutoPurchaseAdmin.Views
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class LicenseWindow : Window
    {
        private readonly ApiClientService _apiService;

        // Constructor mới nhận ApiClientService
        public LicenseWindow(ApiClientService apiService)
        {
            InitializeComponent();
            _apiService = apiService;

        }

        // Constructor mặc định để Designer không lỗi
        public LicenseWindow() : this(null) { }
    }
}
