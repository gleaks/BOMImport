using System;
using System.IO;
using System.Windows;
using System.Globalization;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Net.Http;
using System.Net.Http.Headers;
using Microsoft.Win32;
using CsvHelper;
using CsvHelper.Configuration;
using Flurl.Http;

namespace BOMImport
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            Startup();
        }

        public async void Startup()
        {
            // Test the pre-existing login credentials in Settings
            var loginUser = await ERPNext.Login();
            // Display which user is logged in, or ERROR if not able to log in
            usernameTxt.Text = loginUser + "    ";

        }

        // Create an object that is a shortcut to the credentials stored in Settings
        public Credentials credentials = new();

        private void BtnOpenFile_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new()
            {
                Filter = "P-CAD BOM (*.bom)|*.bom|All files (*.*)|*.*"
            };
            if (openFileDialog.ShowDialog() == true)
            {
                // Read the CSV file into a string so it can be manipulated
                string fileString = File.ReadAllText(openFileDialog.FileName);
                // Remove any quotation marks that do not appear next to a newline, carriage break or a comma
                // AKA remove quotation marks inside quoted fields, eg: "6","TEST_POINT_0.04"_HOLE","DAC","PAD"
                string fixedFileString = Regex.Replace(fileString, "(?<!^|,)\\\"(?!\r?$|,)", "", RegexOptions.Multiline);
                // Find where the header line splits from the body of the CSV by splitting where there are 2 line breaks in a row
                // Because there is a blank line between the header and the body
                var fixHeaders = Regex.Split(fixedFileString, "\n(?=\r?$)", RegexOptions.Multiline);
                // Get rid of any line breaks inside of the header (index 0 of the split), P-CAD adds line breaks in the header sometimes
                var fixedHeader = fixHeaders[0].Replace(Environment.NewLine, "");
                // Combine the header and the body that was split earlier
                var fixedHeaders = "";
                foreach(var x in fixHeaders) { fixedHeaders += x; }
                // This will be the final, formatted list that is imported into ERPNext
                var erpLines = new List<ERPLine>();
                // Write the string that was opened into a MemoryStream (this is required for CSVHelper to read it)
                using (MemoryStream memoryStream = new())
                {
                    var streamWriter = new StreamWriter(memoryStream);
                    try
                    {
                        // Set up and write to the MemoryStream using StreamWriter. Then reset the memorystream back to 0.
                        streamWriter.Write(fixedHeaders);
                        streamWriter.Flush();
                        memoryStream.Seek(0, SeekOrigin.Begin);

                        // Open the MemoryStream using a StreamReader
                        using var streamReader = new StreamReader(memoryStream);
                        // Set the CSVHelper configuration
                        var config = new CsvConfiguration(CultureInfo.InvariantCulture)
                        {
                            // Set CSVHelper to ignore case sensitivity by always lowercasing the headers
                            PrepareHeaderForMatch = args => args.Header.ToLower(),
                            HeaderValidated = null,
                            MissingFieldFound = null
                        };
                        // Start CSVHelper by tying the streamreader & configuration to it
                        using var csvReader = new CsvReader(streamReader, config);
                        // Tie the Class Map defined in classes above to CSVHelper
                        csvReader.Context.RegisterClassMap<BOMLineMap>();

                        // Read each CSV line, line-by-line
                        var lines = csvReader.GetRecords<BOMLine>();
                        foreach (var line in lines)
                        {
                            // Make sure that the component isn't a mechanical hole or something marked as not used
                            if (!line.ComponentName.Contains("HOLE") &&
                                !line.ComponentName.Contains("TEST_PAD") &&
                                !line.ComponentName.Contains("NOT_USED") &&
                                !line.Value.Contains("Not Used") &&
                                !line.Value.Contains("NOT_USED") &&
                                !line.PatternName.Contains("BLANK"))
                            {
                                // See if this lines FTI Part # already exists in our erpLines list
                                var thisLine = erpLines.Find(d => d.FTIPartNumber == line.FTIPartNumber);
                                // If the part exists
                                if (thisLine != null) 
                                { 
                                    // Add this lines reference designator to the running total of RefDes in the list
                                    thisLine.RefDes += " " + line.RefDes; 
                                    // If this line has a count then it means it is a repeated part, so also add the quantity
                                    if (line.Count != "")
                                    {
                                        thisLine.Qty += int.Parse(line.Count);
                                    }
                                }
                                // If there is a number in the count field and no part with the same FTI part # exists then it is a new part entry
                                if (thisLine == null && line.Count != "")
                                {
                                    // Add the line details to a new object in our list
                                    erpLines.Add(new ERPLine() {
                                        FTIPartNumber = line.FTIPartNumber,
                                        Qty = int.Parse(line.Count),
                                        RefDes = line.RefDes,
                                        ComponentName = line.ComponentName
                                    });
                                }
                            }
                        }
                    }
                    // Kill the StreamWriter when done reading the lines
                    finally
                    {
                        streamWriter.Dispose();
                    }
                }
                // Sort the BOM by FTIPartNumber (this is set in the class definition above - CompareTo)
                erpLines.Sort();

                // Open a new window to proceed with BOM review & ERPNext import
                ERPImport erpImportWindow = new(this, erpLines);
                erpImportWindow.Show();
            }
        }

        private void BtnAPIKey_Click(object sender, RoutedEventArgs e)
        {
            APIKey apiKeyWindow = new(this, credentials);
            apiKeyWindow.Show();
        }
    }
}
