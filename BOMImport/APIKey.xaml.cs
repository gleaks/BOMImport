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

namespace BOMImport
{
    /// <summary>
    /// Interaction logic for APIKey.xaml
    /// </summary>
    public partial class APIKey : Window
    {
        private readonly MainWindow mainWindow;
        public APIKey(MainWindow x, Credentials y)
        {
            InitializeComponent();
            var credentials = y;
            mainWindow = x;
            DataContext = credentials;
        }

        private void CredentialCancel_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
        private async void CredentialOK_Click(object sender, RoutedEventArgs e)
        {
            var loginUser = await ERPNext.Login(apiKeyText.Text, apiSecretText.Text);
            if (loginUser != "ERROR")
            {
                this.Close();
                mainWindow.usernameTxt.Text = loginUser;
            }
            else
            {
                errorMessageTxt.Visibility = Visibility.Visible;
            }
        }
    }
}
