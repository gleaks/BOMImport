using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace BOMImport
{
    public partial class ERPImport : Window
    {
        private readonly List<ERPLine> erpLines;
        private readonly MainWindow mainWindow;
        private bool hasErrors;

        public ERPImport(MainWindow y, List<ERPLine> x)
        {
            // Load in data from the MainWindow page
            erpLines = x;
            mainWindow = y;
            // Setting the DataContext is what builds the DataTable on this page from the erpLines List
            DataContext = erpLines;
            InitializeComponent();
            // Immediately after loading the page run the error check against the list
            hasErrors = ERPNext.ErrorCheck(erpLines, this);
        }

        // The Import button - to begin the Import into ERPNext
        private async void BtnImport_Click(object sender, RoutedEventArgs e)
        {
            // Double check that there are no errors that were caught in the error check before importing
            if (hasErrors != true)
            {
                // Check to see that the FTI Part # that is supposed to be the parent of this BOM actually is a proper FTI Part # (integer & 6 digits)
                if (int.TryParse(txtBomPart.Text, out int n) && Math.Floor(Math.Log10(n) + 1) == 6)
                {
                    // Open the loading icon and disable all controls on the page while it asyncronously imports
                    erpDialog.IsOpen = true;
                    try
                    {
                        // Run the NewBOM function (in Classes) on erpLines - bringing in the parent FTI Part # and whether the BOM should be submitted after import
                        dynamic bomResult = await ERPNext.NewBOM(txtBomPart.Text, erpLines, (bool)checkSubmit.IsChecked);

                        // After trying to import the BOM check the response from the ERPNext server to check that the import was succesful
                        // AKA: it responded with the name of the new BOM that was created
                        if (bomResult.name != null)
                        {
                            // Convert the name of the BOM into a string (it is stored as a dynamic object)
                            string name = Convert.ToString(bomResult.name);
                            // Build the link to the new BOM
                            string url = "https://focusedtest.frappe.cloud/desk#Form/BOM/" + name;
                            // Display a popup on the MainWindow that shows the import was succesful and a link to the new BOM
                            // Also includes a time-delay so the popup stays open at least 10 seconds
                            mainWindow.snackbarMain.MessageQueue.Enqueue(name + " was succesfully imported into ERPNext", "OPEN LINK", param => mainWindow.OpenLink(param), url, false, true, TimeSpan.FromSeconds(10));
                            // Close this window after success
                            Close();
                        }
                    }
                    // If there is an error with importing then display the error and catch out of the import process
                    catch (Exception ex)
                    {
                        mainWindow.txtErrorPopup.Text = ex.ToString();
                        mainWindow.errorPopup.IsOpen = true;
                    }
                    // Whether things are a success or not close the loading icon and re-enable the controls on the page
                    finally
                    {
                        erpDialog.IsOpen = false;
                    }


                }
                // If there is not a valid FTI Part # in the BOM parent field then display an error
                else
                {
                    snackbarERP.MessageQueue.Enqueue("ERROR: Please enter a valid FTI Part");
                    txtBomPart.BorderBrush = Brushes.Red;
                    txtBomPartError.Visibility = Visibility.Visible;
                }
            }
        }
        // After an error for a bad FTI Part # has been displayed, get rid of the error as soon as someone starts typing
        // in the BOM parent field
        private void TxtBomPart_Changed(object sender, RoutedEventArgs e)
        {
            snackbarERP.MessageQueue.Clear();
            txtBomPart.ClearValue(TextBox.BorderBrushProperty);
            txtBomPartError.Visibility = Visibility.Hidden;
        }
        // This is the button to re-check errors. It runs the error check again
        private void BtnTest_Click(object sender, RoutedEventArgs e)
        {
            hasErrors = ERPNext.ErrorCheck(erpLines, this);
        }

    }
}
