using System.Windows;

namespace BOMImport
{
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
            Close();
        }
        private async void CredentialOK_Click(object sender, RoutedEventArgs e)
        {
            var loginUser = await ERPNext.Login(mainWindow, apiKeyText.Text, apiSecretText.Text);
            if (loginUser == "ERROR") { errorMessageTxt.Visibility = Visibility.Visible; }
            else { Close(); }
        }
    }
}
