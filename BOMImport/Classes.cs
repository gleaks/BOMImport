using System;
using System.Globalization;
using System.Threading.Tasks;
using Flurl.Http;
using CsvHelper.Configuration;

namespace BOMImport
{
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

    public class Credentials
    {
        public string APIKeyText
        {
            get { return Properties.Settings.Default.api_key; }
            set { Properties.Settings.Default.api_key = value; }
        }
        public string APISecretText
        {
            get { return Properties.Settings.Default.api_secret; }
            set { Properties.Settings.Default.api_secret = value; }
        }
    }
    public static class ERPNext
    {
        public static string Username { get; set; }
        public static async Task<String> Login(string username, string password)
        {
            try
            {
                var result = await "https://focusedtest.frappe.cloud/api/method/frappe.auth.get_logged_user"
                    .WithBasicAuth(username, password)
                    .GetAsync()
                    .ReceiveJson();
                ERPNext.Username = result.message;
                return result.message;
            }
            catch (FlurlHttpException)
            {
                return "ERROR";
            }
        }
    }
}
