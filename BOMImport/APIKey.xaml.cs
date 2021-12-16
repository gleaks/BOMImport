using System.Windows;

namespace BOMImport
{
    public partial class APIKey : Window
    {
        private readonly MainWindow mainWindow;
        public APIKey(MainWindow x, Credentials y)
        {
            InitializeComponent();
            Credentials credentials = y;
            mainWindow = x;
            // Set the DataContext of the login fields to the pre-existing credentials class
            DataContext = credentials;
        }

        // If you click the cancel button, just close the window
        private void CredentialCancel_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
        // If you click the OK button
        private async void CredentialOK_Click(object sender, RoutedEventArgs e)
        {
            // Attempt to Login to ERPNext with the login & password supplied in this window
            var loginUser = await ERPNext.Login(mainWindow, apiKeyText.Text, apiSecretText.Text);
            // If the login does not work then display an error
            if (loginUser == "ERROR") { errorMessageTxt.Visibility = Visibility.Visible; }
            // If the login worked then close the window
            else { Close(); }
        }
    }
}
