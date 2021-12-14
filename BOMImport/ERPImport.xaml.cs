using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Dynamic;
using System.Windows.Documents;
using System.Windows.Navigation;

namespace BOMImport
{
    /// <summary>
    /// Interaction logic for ERPImport.xaml
    /// </summary>
    public partial class ERPImport : Window
    {
        private readonly List<ERPLine> erpLines;
        private readonly MainWindow mainWindow;
        private readonly bool hasErrors;
        public ERPImport(MainWindow y, List<ERPLine> x)
        {
            erpLines = x;
            mainWindow = y;
            DataContext = erpLines;
            InitializeComponent();
            int index = 1;
            // Iterate through each consolidated line that will be imported into ERPNext
            foreach (ERPLine line in erpLines)
            {
                // Split the RefDes line by whitespace, so that it is possible to count the number of reference designators
                var refDesSplit = line.RefDes.Split(" ");
                // An empty error message if the refdes count matches with the BOM count
                // If the refdes count doesnt match with the BOM count then populate the error message
                if (refDesSplit.Length != line.Qty) 
                {
                    string refDesMismatch = "*MISMATCH WITH REFDES QTY* ";
                    line.Error += refDesMismatch;
                    hasErrors = true;
                }
                if (!int.TryParse(line.FTIPartNumber, out int n) && Math.Floor(Math.Log10(n) + 1) != 6)
                {
                    string noFTIPart = "*INVALID FTI PART #*";
                    line.Error += noFTIPart;
                    hasErrors = true;
                    dgBOM.BorderBrush = Brushes.Red;
                    txtBomError.Visibility = Visibility.Visible;
                    btnImport.IsEnabled = false;
                }
                line.LineNumber = index;
                index++;
            }
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
                    txtBomPart.BorderBrush = Brushes.Red;
                    txtBomPartError.Visibility = Visibility.Visible;
                }
            }
        }
        private void TxtBomPart_Changed(object sender, RoutedEventArgs e)
        {
            txtBomPart.ClearValue(TextBox.BorderBrushProperty);
            txtBomPartError.Visibility = Visibility.Hidden;
        }

    }
}
