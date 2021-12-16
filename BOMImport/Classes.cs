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
    // This is the class that each individual line of the BOM is originally written to by CSVHelper
    // after initially opening a BOM
    public record BOMLine
    {
        public string Count { get; set; }
        public string ComponentName { get; set; }
        public string RefDes { get; set; }
        public string Value { get; set; }
        public string FTIPartNumber { get; set; }
        public string PatternName { get; set; }
    }
    // The Map that sets what headers should go to what objects in our BOMLine class
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
    // This is the class that is the final, formatted BOM that is displayed on the ERPImport page and is imported into ERPNext
    public class ERPLine : IComparable<ERPLine>
    {
        public string FTIPartNumber { get; set; }
        public int Qty { get; set; }
        public string RefDes { get; set; }
        public string ComponentName { get; set; }
        public string Error { get; set; }
        public bool HasError { get; set; }
        public int LineNumber { get; set; }
        // SORTING - This function allows ERPLine.Sort() to work properly, sorts a-z
        public int CompareTo(ERPLine compareLine)
        {
            return compareLine == null ? 1 : FTIPartNumber.CompareTo(compareLine.FTIPartNumber);
        }
    }
    // This is a shortcut to access & modify the login settings - they are stored in Properties->Settings
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
    // This is a static class that contains methods to access the functions of the ERPNext API
    public static class ERPNext
    {
        // The username of the person that is logged in - this is returned by the server on succesful login
        public static string Username { get; set; }
        // Function to login to the API of ERPNext & return the username
        public static async Task<string> Login(MainWindow mainWindow, string u = null, string p = null)
        {
            // If the username and password are set in the overload then use them, otherwise use the default login stored in Settings
            string username = u ?? Credentials.APIKeyText;
            string password = p ?? Credentials.APISecretText;
            try
            {
                // Make a GET Http call with Flurl to the ERPNext server, using Basic Authorization, to ask for the name of the logged in user
                dynamic result = await "https://focusedtest.frappe.cloud/api/method/frappe.auth.get_logged_user"
                    .WithBasicAuth(username, password)
                    .GetAsync()
                    .ReceiveJson();
                // The server responds with a JSON object, that Flurl then places into a Dynamic object. Read the actual "message" node of the JSON object
                // to get the logged in username
                Username = result.message;
                // Set the MainWindow display to show the username of the logged in user
                mainWindow.usernameTxt.Text = result.message;
                // Change the icon on the MainWindow to show a green check mark
                mainWindow.loginIcon.Kind = PackIconKind.CheckCircleOutline;
                mainWindow.loginIcon.Foreground = Brushes.Green;
                // Return the username as part of the function
                return result.message;
            }
            // If there is an error communicating with the server
            // If there is an error actually importing it is handled differently - because the server will still respond with a message
            // containing error instead of the username
            catch (FlurlHttpException)
            {
                mainWindow.loginIcon.Kind = PackIconKind.AlertCircleOutline;
                mainWindow.loginIcon.Foreground = Brushes.Red;
                mainWindow.usernameTxt.Text = "Login Error";
                return "ERROR";
            }
        }
        // Run an error check on an already complete & formatted ERPLine list
        public static bool ErrorCheck(List<ERPLine> erpLines, ERPImport erpImport)
        {
            // Sort the list by FTI Part #
            erpLines.Sort();
            // Create an index that is used as "line numbers"
            int index = 1;
            // If an error pops up on any line of the BOM, set the universal error flag so it can't import
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
                // If the FTI Part # for the line is not valid - aka it is not a 6 digit integer
                else if (!int.TryParse(line.FTIPartNumber, out int n) && Math.Floor(Math.Log10(n) + 1) != 6)
                {
                    string noFTIPart = "*INVALID FTI PART #*";
                    line.Error += noFTIPart;
                    hasErrors = true;
                    line.HasError = true;
                }
                // Set the line number to the index and increase the index by 1
                line.LineNumber = index;
                index++;
            }
            // If the universal hasErrors flag is true then don't allow import and display the error message
            if (hasErrors)
            {
                erpImport.snackbarERP.MessageQueue.Enqueue("There are errors in this BOM.");
                erpImport.txtBomError.Visibility = Visibility.Visible;
                erpImport.btnImport.IsEnabled = false;
            }
            // Otherwise there are not errors. This clears out the previous error statement and allows import if it was blocked before
            else
            {
                erpImport.snackbarERP.MessageQueue.Clear();
                erpImport.txtBomError.Visibility = Visibility.Hidden;
                erpImport.btnImport.IsEnabled = true;
            }
            // Refresh the actual display of the DataGrid to get rid of errors and re-order the list
            erpImport.dgBOM.Items.Refresh();

            return hasErrors;
        }
        // The function to actually import the complete formatted BOM into ERPNext
        public static async Task<ExpandoObject> NewBOM(string bomPart, List<ERPLine> erpLines, bool submit)
        {
            // Start building a JSON object as a string that will be sent as the main body of the HTTP request. This includes all the required fields
            // to import a BOM into ERPNext
            string queryString = "{ \"item\" : \"" + bomPart + "\", \"company\" : \"Focused Test Inc\", \"quantity\" : \"1\", \"currency\" : \"USD\", ";
            queryString += "\"conversion_rate\" : \"1\", \"items\" : [ ";
            foreach (ERPLine line in erpLines)
            {
                // Continue to build the string for each line of the BOM, including each lines details
                queryString += "{ \"item_code\" : \"" + line.FTIPartNumber + "\", \"qty\" : \"" + line.Qty + "\", \"uom\" : \"Nos\", \"rate\" : \"0\", ";
                queryString += "\"refdes\" : \"" + line.RefDes + "\" }";

                // If the line is the last line
                if (erpLines.IndexOf(line) == erpLines.Count - 1)
                {
                    // End the JSON list
                    queryString += " ] }";
                }
                else
                {
                    // Otherwise keep the list going
                    queryString += ", ";
                }
            }
            // Once we have iterated through the BOM, take the big old queryString we built and send it as an HTTP post request
            // along with our login details.
            dynamic result = await "https://focusedtest.frappe.cloud/api/resource/BOM"
                .WithBasicAuth(Credentials.APIKeyText, Credentials.APISecretText)
                .PostStringAsync(queryString)
                .ReceiveJson();
            // If we receive back the name of the BOM from the server then it was imported correctly. Return back the entire server message
            if (result.data.name != null)
            {
                return result.data;
            }
            // Otherwise the import failed for some reason. Create a dynamic ExpandoObject and return that with an error message
            else
            {
                dynamic errorObject = new ExpandoObject();
                errorObject.error = "THERE WAS AN ERROR";
                return errorObject;
            }
        }
    }
}
