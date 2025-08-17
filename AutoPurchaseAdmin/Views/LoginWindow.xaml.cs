using AutoPurchaseAdmin.ViewModels;
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
    /// Interaction logic for LoginWindow.xaml
    /// </summary>
    public partial class LoginWindow : Window
    {
        public LoginWindow()
        {
            InitializeComponent();

            var viewModel = new LoginViewModel();
            this.DataContext = viewModel;

            // Gán callback
            // Callback mở MainWindow và đóng LoginWindow
            //viewModel.CloseWindowAction = () => this.Close();
            viewModel.OpenMainWindowAction = () =>
            {
                App.Current.Dispatcher.Invoke(() =>
                {
                    // Lưu vào App để WPF giữ reference
                    ((App)Application.Current).MainWin = new LicenseWindow(viewModel.ApiService);
                    ((App)Application.Current).MainWin.Show();

                    // Đóng LoginWindow sau khi MainWindow hiện
                    this.Close();
                });
            };
        }

        private void PasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
        {
            if (DataContext is LoginViewModel vm)
                vm.Password = ((PasswordBox)sender).Password;
        }
    }
}
