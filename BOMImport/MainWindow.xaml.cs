using System;
using System.IO;
using System.Windows;
using System.Globalization;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Microsoft.Win32;
using CsvHelper;
using CsvHelper.Configuration;

namespace BOMImport
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        public record BOMLine
        {
            public string Count { get; set; }
            public string ComponentName { get; set; }
            public string RefDes { get; set; }
            public string Value { get; set; }
            public string PartNumber { get; set; }
            public string DistributorPartNum { get; set; }
            public string Manufacturer { get; set; }
            public string MfgPartNum { get; set; }
            public string FTIPartNumber { get; set; }
        }

        public sealed class BOMLineMap : ClassMap<BOMLine>
        {
            public BOMLineMap()
            {
                // Automatically try and map as many header fields as possible, as there can be a variable amount of columns in each BOM
                AutoMap(CultureInfo.InvariantCulture);
                // Hand map the most important header columns, especially ones that don't auto-format
                Map(m => m.PartNumber).Name("Part Number");
                Map(m => m.DistributorPartNum).Name("Distributor Part Num");
                Map(m => m.MfgPartNum).Name("Mfg Part Num");
                Map(m => m.FTIPartNumber).Name("FTI Part Number");
            }
        }

        public class ERPLine : IComparable<ERPLine>
        {
            public string FTIPartNumber { get; set; }
            public string Qty { get; set; }
            public string RefDes { get; set; }
            public int CompareTo(ERPLine compareLine)
            {
                return compareLine == null ? 1 : FTIPartNumber.CompareTo(compareLine.FTIPartNumber);
            }
        }

        private void BtnOpenFile_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog();
            openFileDialog.Filter = "P-CAD BOM (*.bom)|*.bom|All files (*.*)|*.*";
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
                var fixedHeaders = fixedHeader + fixHeaders[1];
                // This will be the final, formatted list that is imported into ERPNext
                var erpLines = new List<ERPLine>();
                // Write the string that was opened into a MemoryStream (this is required for CSVHelper to read it)
                using (MemoryStream memoryStream = new MemoryStream())
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
                                !line.Value.Contains("NOT_USED"))
                            {
                                // Here is where I am consolidating each individual RefDes line into a single line with a qty & concatenated RefDes line
                                // If there is nothing in the Count field that means it is just a unique refdes line
                                if (line.Count == "")
                                {
                                    // Find the previously entered line object that contains this part number
                                    var thisLine = erpLines.Find(d => d.FTIPartNumber == line.FTIPartNumber);
                                    // Update the previously entered line's RefDes to include the RefDes on this line
                                    if (thisLine != null) { thisLine.RefDes += " " + line.RefDes; }
                                }
                                else
                                {
                                    // If there is a number in the Count field then create a new object in our ERPLine list
                                    erpLines.Add(new ERPLine() { 
                                        FTIPartNumber = line.FTIPartNumber, 
                                        Qty = line.Count,
                                        RefDes = line.RefDes 
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

                // Iterate through each consolidated line that will be imported into ERPNext
                foreach (ERPLine line in erpLines)
                {
                    // Split the RefDes line by whitespace, so that it is possible to count the number of reference designators
                    var refDesSplit = line.RefDes.Split(" ");
                    // An empty error message if the refdes count matches with the BOM count
                    string refDesMismatch = "";
                    // If the refdes count doesnt match with the BOM count then populate the error message
                    if (refDesSplit.Length != Int32.Parse(line.Qty)) { refDesMismatch += " *MISMATCH WITH REFDES QTY* "; }
                    // Display each line of the consolidated BOM in the text input on the MainWindow
                    txtEditor.Text += line.FTIPartNumber + " - " + line.Qty + refDesMismatch + " - " + line.RefDes + Environment.NewLine;
                }
            }
        }
    }
}
