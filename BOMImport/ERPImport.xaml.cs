using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Dynamic;
using System.Windows.Documents;
using System.Windows.Navigation;
using MaterialDesignThemes.Wpf;
using System.Threading.Tasks;

namespace BOMImport
{
    /// <summary>
    /// Interaction logic for ERPImport.xaml
    /// </summary>
    public partial class ERPImport : Window
    {
        private readonly List<ERPLine> erpLines;
        private readonly MainWindow mainWindow;
        private bool hasErrors;

        public ERPImport(MainWindow y, List<ERPLine> x)
        {
            erpLines = x;
            mainWindow = y;
            DataContext = erpLines;
            InitializeComponent();
            hasErrors = ERPNext.ErrorCheck(erpLines, this);
        }

        private async void BtnImport_Click(object sender, RoutedEventArgs e)
        {
            if (hasErrors != true)
            {
                if (int.TryParse(txtBomPart.Text, out int n) && Math.Floor(Math.Log10(n) + 1) == 6)
                {

                    dynamic bomResult = await ERPNext.NewBOM(txtBomPart.Text, erpLines, (bool)checkSubmit.IsChecked);
                    if (bomResult.name != null)
                    {
                        var url = "https://focusedtest.frappe.cloud/desk#Form/BOM/" + bomResult.name;
                        //mainWindow.txtLogs.Text += bomResult.creation + ": " + bomResult.name + " succesfully imported into ERPNext @ " + url + "\n";
                        Close();
                    }
                }
                else
                {
                    snackbarMain.MessageQueue.Enqueue("ERROR: Please enter a valid FTI Part");
                    txtBomPart.BorderBrush = Brushes.Red;
                    txtBomPartError.Visibility = Visibility.Visible;
                }
            }
        }
        private void TxtBomPart_Changed(object sender, RoutedEventArgs e)
        {
            snackbarMain.MessageQueue.Clear();
            txtBomPart.ClearValue(TextBox.BorderBrushProperty);
            txtBomPartError.Visibility = Visibility.Hidden;
        }

        private void BtnTest_Click(object sender, RoutedEventArgs e)
        {
            hasErrors = ERPNext.ErrorCheck(erpLines, this);
        }

    }
}
