using CsvHelper;
using CsvHelper.Configuration;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using System.Windows;

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
            var loginUser = await ERPNext.Login(this);
        }

        // Create an object that is a shortcut to the credentials stored in Settings
        public Credentials credentials = new();

        // The button for Open BOM...
        private void BtnOpenFile_Click(object sender, RoutedEventArgs e)
        {
            // When someone clicks on OPEN BOM... then open a file dialog
            OpenFileDialog openFileDialog = new()
            {
                // Filter out only P-CAD BOMS, but also allow an option to select any file
                Filter = "P-CAD BOM (*.bom)|*.bom|All files (*.*)|*.*"
            };
            if (openFileDialog.ShowDialog() == true)
            {
                // Once the file is open try-catch the header manipulations
                try
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
                    // Combine the header and the body that was split earlier. Combine all segments of the body in case there were
                    // random line breaks in the body as well (this happens sometimes).
                    var fixedHeaders = "";
                    foreach (var x in fixHeaders) { fixedHeaders += x; }
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
                                PrepareHeaderForMatch = args => args.Header.ToLower()
                            };
                            // Start CSVHelper by tying the streamreader & configuration to it
                            using var csvReader = new CsvReader(streamReader, config);
                            // Tie the Class Map defined in classes above to CSVHelper
                            csvReader.Context.RegisterClassMap<BOMLineMap>();

                            try
                            {
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
                                        // See if this lines FTI Part # already exists in our erpLines list.
                                        // If this line has a blank FTI Part # then see if a part with the same ComponentName exists
                                        ERPLine thisLine = line.FTIPartNumber != ""
                                            ? erpLines.Find(d => d.FTIPartNumber == line.FTIPartNumber)
                                            : erpLines.Find(d => d.ComponentName == line.ComponentName);

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
                                            erpLines.Add(new ERPLine()
                                            {
                                                FTIPartNumber = line.FTIPartNumber,
                                                Qty = int.Parse(line.Count),
                                                RefDes = line.RefDes,
                                                ComponentName = line.ComponentName
                                            });
                                        }
                                    }
                                }
                            }
                            // If there are missing headers then pop up an error explaining that
                            catch (HeaderValidationException ex)
                            {
                                txtErrorPopup.Text = "ERROR: Headers Are Missing\n\n\n";
                                txtErrorPopup.Text += ex.ToString();
                                errorPopup.IsOpen = true;
                            }
                            // If CSVHelper fails for some reason
                            catch (Exception ex)
                            {
                                txtErrorPopup.Text = ex.ToString();
                                errorPopup.IsOpen = true;
                            }
                        }
                        // If the StreamWriter fails to write for some reason
                        catch (Exception ex)
                        {
                            txtErrorPopup.Text = ex.ToString();
                            errorPopup.IsOpen = true;
                        }
                        // Kill the StreamWriter when done reading the lines
                        finally
                        {
                            streamWriter.Dispose();
                        }

                        // Make sure there are actually lines inside ERPLines
                        if (erpLines.Count != 0)
                        {
                            // Open a new window to proceed with BOM review & ERPNext import
                            ERPImport erpImportWindow = new(this, erpLines);
                            erpImportWindow.Show();
                        }
                    }
                }
                // If the file can't be loaded for some reason
                catch (Exception ex)
                {
                    txtErrorPopup.Text = ex.ToString();
                    errorPopup.IsOpen = true;
                }
            }
        }

        // Open the Login page (APIKey page) if someone clicks on the Login button
        private void BtnAPIKey_Click(object sender, RoutedEventArgs e)
        {
            APIKey apiKeyWindow = new(this, credentials);
            apiKeyWindow.Show();
        }
        // The button to dismiss an error page when it pops up
        private void BtnErrorPopup_Click(object sender, RoutedEventArgs e)
        {
            errorPopup.IsOpen = false;
            txtErrorPopup.Text = "";
        }
        // Open the Help page if someone clicks the Help button
        private void BtnHelp_Click(object sender, RoutedEventArgs e)
        {
            Help helpWindow = new();
            helpWindow.Show();
        }
        // If someone clicks a link then open it in the default browser (code taken from MSDN)
        public void OpenLink(string url)
        {
            var sInfo = new System.Diagnostics.ProcessStartInfo(url)
            {
                UseShellExecute = true,
            };
            System.Diagnostics.Process.Start(sInfo);
        }
    }
}
