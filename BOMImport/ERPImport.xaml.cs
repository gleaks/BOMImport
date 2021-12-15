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
                    erpDialog.IsOpen = true;
                    try
                    {
                        dynamic bomResult = await ERPNext.NewBOM(txtBomPart.Text, erpLines, (bool)checkSubmit.IsChecked);
                        if (bomResult.name != null)
                        {
                            string name = Convert.ToString(bomResult.name);
                            string url = "https://focusedtest.frappe.cloud/desk#Form/BOM/" + name;
                            mainWindow.snackbarMain.MessageQueue.Enqueue(name + " was succesfully imported into ERPNext", "OPEN LINK", param => mainWindow.OpenLink(param), url, false, true, TimeSpan.FromSeconds(10));
                            Close();
                        }
                    }
                    catch (Exception ex)
                    {
                        mainWindow.txtErrorPopup.Text = ex.ToString();
                        mainWindow.errorPopup.IsOpen = true;
                    }
                    finally
                    {
                        erpDialog.IsOpen = false;
                    }


                }
                else
                {
                    snackbarERP.MessageQueue.Enqueue("ERROR: Please enter a valid FTI Part");
                    txtBomPart.BorderBrush = Brushes.Red;
                    txtBomPartError.Visibility = Visibility.Visible;
                }
            }
        }
        private void TxtBomPart_Changed(object sender, RoutedEventArgs e)
        {
            snackbarERP.MessageQueue.Clear();
            txtBomPart.ClearValue(TextBox.BorderBrushProperty);
            txtBomPartError.Visibility = Visibility.Hidden;
        }

        private void BtnTest_Click(object sender, RoutedEventArgs e)
        {
            hasErrors = ERPNext.ErrorCheck(erpLines, this);
        }

    }
}
