using AutoPurchaseAdmin.Views;
using System.Configuration;
using System.Data;
using System.Windows;

namespace AutoPurchaseAdmin
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        public LicenseWindow MainWin { get; set; }

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            var loginWindow = new LoginWindow();
            loginWindow.Show();
        }
    }

}
