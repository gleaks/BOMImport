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
using System.Data;
using System.Collections.ObjectModel;
using System.Diagnostics;

namespace BOMImport
{
    /// <summary>
    /// Interaction logic for ERPImport.xaml
    /// </summary>
    public partial class ERPImport : Window
    {
        private readonly List<ERPLine> erpLines;
        private bool hasErrors;
        public ERPImport(List<ERPLine> x)
        {
            erpLines = x;
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

                    var bomResult = await ERPNext.NewBOM(txtBomPart.Text, erpLines, (bool)(checkSubmit.IsChecked));
                    if (bomResult != "ERROR")
                    {
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
