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
        private readonly Credentials credentials;
        public ERPImport(List<ERPLine> x, Credentials y)
        {
            erpLines = x;
            credentials = y;
            DataContext = erpLines;
            int index = 1;
            // Iterate through each consolidated line that will be imported into ERPNext
            foreach (ERPLine line in erpLines)
            {
                // Split the RefDes line by whitespace, so that it is possible to count the number of reference designators
                var refDesSplit = line.RefDes.Split(" ");
                // An empty error message if the refdes count matches with the BOM count
                string refDesMismatch = null;
                // If the refdes count doesnt match with the BOM count then populate the error message
                if (refDesSplit.Length != line.Qty) { refDesMismatch += " *MISMATCH WITH REFDES QTY* "; }
                // Set the error message in the object
                line.RefDesError = refDesMismatch;
                line.LineNumber = index;
                index++;
            }

            InitializeComponent();
        }
    }
}
