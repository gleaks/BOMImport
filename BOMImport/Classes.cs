using CsvHelper.Configuration;
using Flurl.Http;
using MaterialDesignThemes.Wpf;
using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Globalization;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;

namespace BOMImport
{
    public record BOMLine
    {
        public string Count { get; set; }
        public string ComponentName { get; set; }
        public string RefDes { get; set; }
        public string Value { get; set; }
        public string FTIPartNumber { get; set; }
        public string PatternName { get; set; }
    }
    public sealed class BOMLineMap : ClassMap<BOMLine>
    {
        public BOMLineMap()
        {
            // Automatically try and map as many header fields as possible, as there can be a variable amount of columns in each BOM
            AutoMap(CultureInfo.InvariantCulture);
            // Hand map the most important header columns, especially ones that don't auto-format
            Map(m => m.FTIPartNumber).Name("FTI Part Number");
        }
    }

    public class ERPLine : IComparable<ERPLine>
    {
        public string FTIPartNumber { get; set; }
        public int Qty { get; set; }
        public string RefDes { get; set; }
        public string ComponentName { get; set; }
        public string Error { get; set; }
        public bool HasError { get; set; }
        public int LineNumber { get; set; }
        public int CompareTo(ERPLine compareLine)
        {
            return compareLine == null ? 1 : FTIPartNumber.CompareTo(compareLine.FTIPartNumber);
        }
    }

    public class Credentials
    {
        public static string APIKeyText
        {
            get => Properties.Settings.Default.api_key;
            set => Properties.Settings.Default.api_key = value;
        }
        public static string APISecretText
        {
            get => Properties.Settings.Default.api_secret;
            set => Properties.Settings.Default.api_secret = value;
        }
    }
    public static class ERPNext
    {
        public static string Username { get; set; }
        public static async Task<string> Login(MainWindow mainWindow, string u = null, string p = null)
        {
            string username = u ?? Credentials.APIKeyText;
            string password = p ?? Credentials.APISecretText;
            try
            {
                dynamic result = await "https://focusedtest.frappe.cloud/api/method/frappe.auth.get_logged_user"
                    .WithBasicAuth(username, password)
                    .GetAsync()
                    .ReceiveJson();
                Username = result.message;
                mainWindow.usernameTxt.Text = result.message;
                mainWindow.loginIcon.Kind = PackIconKind.CheckCircleOutline;
                mainWindow.loginIcon.Foreground = Brushes.Green;
                return result.message;
            }
            catch (FlurlHttpException)
            {
                mainWindow.loginIcon.Kind = PackIconKind.AlertCircleOutline;
                mainWindow.loginIcon.Foreground = Brushes.Red;
                mainWindow.usernameTxt.Text = "Login Error";
                return "ERROR";
            }
        }
        public static bool ErrorCheck(List<ERPLine> erpLines, ERPImport erpImport)
        {
            erpLines.Sort();
            int index = 1;
            bool hasErrors = false;
            // Iterate through each consolidated line that will be imported into ERPNext
            foreach (ERPLine line in erpLines)
            {
                line.HasError = false;
                line.Error = null;
                // Split the RefDes line by whitespace, so that it is possible to count the number of reference designators
                string[] refDesSplit = line.RefDes.Split(" ");
                // An empty error message if the refdes count matches with the BOM count
                // If the refdes count doesnt match with the BOM count then populate the error message
                if (refDesSplit.Length != line.Qty)
                {
                    string refDesMismatch = "*MISMATCH WITH REFDES QTY* ";
                    line.Error += refDesMismatch;
                    hasErrors = true;
                    line.HasError = true;
                }
                else if (!int.TryParse(line.FTIPartNumber, out int n) && Math.Floor(Math.Log10(n) + 1) != 6)
                {
                    string noFTIPart = "*INVALID FTI PART #*";
                    line.Error += noFTIPart;
                    hasErrors = true;
                    line.HasError = true;
                }
                line.LineNumber = index;
                index++;
            }
            if (hasErrors)
            {
                erpImport.snackbarERP.MessageQueue.Enqueue("There are errors in this BOM.");
                erpImport.txtBomError.Visibility = Visibility.Visible;
                erpImport.btnImport.IsEnabled = false;
            }
            else
            {
                erpImport.snackbarERP.MessageQueue.Clear();
                erpImport.txtBomError.Visibility = Visibility.Hidden;
                erpImport.btnImport.IsEnabled = true;
            }
            erpImport.dgBOM.Items.Refresh();

            return hasErrors;
        }
        public static async Task<ExpandoObject> NewBOM(string bomPart, List<ERPLine> erpLines, bool submit)
        {
            string queryString = "{ \"item\" : \"" + bomPart + "\", \"company\" : \"Focused Test Inc\", \"quantity\" : \"1\", \"currency\" : \"USD\", ";
            queryString += "\"conversion_rate\" : \"1\", \"items\" : [ ";
            foreach (ERPLine line in erpLines)
            {
                queryString += "{ \"item_code\" : \"" + line.FTIPartNumber + "\", \"qty\" : \"" + line.Qty + "\", \"uom\" : \"Nos\", \"rate\" : \"0\", ";
                queryString += "\"refdes\" : \"" + line.RefDes + "\" }";

                // If the line is the last line
                if (erpLines.IndexOf(line) == erpLines.Count - 1)
                {
                    queryString += " ] }";
                }
                else
                {
                    queryString += ", ";
                }
            }
            dynamic result = await "https://focusedtest.frappe.cloud/api/resource/BOM"
                .WithBasicAuth(Credentials.APIKeyText, Credentials.APISecretText)
                .PostStringAsync(queryString)
                .ReceiveJson();
            if (result.data.name != null)
            {
                return result.data;
            }
            else
            {
                dynamic errorObject = new ExpandoObject();
                errorObject.error = "THERE WAS AN ERROR";
                return errorObject;
            }
        }
    }
}
