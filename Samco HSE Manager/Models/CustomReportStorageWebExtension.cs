// ReSharper disable once CheckNamespace
namespace Samco_HSE_Manager.Models;

using DevExpress.XtraReports.UI;
using System.ServiceModel;
using System.Web;

public class CustomReportStorageWebExtension : DevExpress.XtraReports.Web.Extensions.ReportStorageWebExtension
{
    private const string ReportDirectory = "Reports";
    private const string FileExtension = ".repx";
    public CustomReportStorageWebExtension()
    {
        if (!Directory.Exists(ReportDirectory))
        {
            Directory.CreateDirectory(ReportDirectory);
        }
    }

    private bool IsWithinReportsFolder(string url, string folder)
    {
        var rootDirectory = new DirectoryInfo(folder);
        var fileInfo = new FileInfo(Path.Combine(folder, url));
        return fileInfo.Directory!.FullName.ToLower().StartsWith(rootDirectory.FullName.ToLower());
    }

    public override bool CanSetData(string url)
    {
        // Determines whether a report with the specified URL can be saved.
        // Add custom logic that returns **false** for reports that should be read-only.
        // Return **true** if no validation is required.
        // This method is called only for valid URLs (if the **IsValidUrl** method returns **true**).

        return true;
    }

    public override bool IsValidUrl(string url)
    {
        // Determines whether the URL passed to the current report storage is valid.
        // Implement your own logic to prohibit URLs that contain spaces or other specific characters.
        // Return **true** if no validation is required.

        return Path.GetFileName(url) == url;
    }

    public override byte[] GetData(string url)
    {
        try
        {
            // Parse the string with the report name and parameter values.
            var parts = url.Split('?');
            var reportName = parts[0];
            var parametersQueryString = parts.Length > 1 ? parts[1] : string.Empty;

            // Create a report instance.
            XtraReport? report = null;

            if (Directory.EnumerateFiles(ReportDirectory).
                Select(Path.GetFileNameWithoutExtension).Contains(reportName))
            {
                var reportBytes = File.ReadAllBytes(Path.Combine(ReportDirectory, reportName + FileExtension));
                using var ms = new MemoryStream(reportBytes);
                report = XtraReport.FromStream(ms);
            }

            if (report != null)
            {
                // Apply the parameter values to the report.
                var parameters = HttpUtility.ParseQueryString(parametersQueryString);

                foreach (var parameterName in parameters.AllKeys)
                {
                    report.Parameters[parameterName].Value = Convert.ChangeType(
                        parameters.Get(parameterName), report.Parameters[parameterName].Type);
                }

                // Disable the Visible property for all report parameters
                // to hide the Parameters Panel in the viewer.
                foreach (var parameter in report.Parameters)
                {
                    parameter.Visible = false;
                }

                // If you do not hide the panel, disable the report's RequestParameters property.
                // report.RequestParameters = false;

                using var ms = new MemoryStream();
                report.SaveLayoutToXml(ms);
                return ms.ToArray();
            }
        }
        catch (Exception ex)
        {
            throw new DevExpress.XtraReports.Web.ClientControls.FaultException(
                "Could not get report data.", ex);
        }
        throw new DevExpress.XtraReports.Web.ClientControls.FaultException(
            $"Could not find report '{url}'.");
    }

    public override Dictionary<string, string> GetUrls()
    {
        // Returns a dictionary that contains the report names (URLs) and display names. 
        // The Report Designer uses this method to populate the Open Report and Save Report dialogs.

#pragma warning disable CS8619
        return Directory.GetFiles(ReportDirectory, "*" + FileExtension)
            .ToDictionary(Path.GetFileNameWithoutExtension);
#pragma warning restore CS8619
    }

    public override void SetData(XtraReport report, string url)
    {
        // Saves the specified report to the report storage with the specified name
        // (saves existing reports only). 
        if (!IsWithinReportsFolder(url, ReportDirectory))
            throw new FaultException(new FaultReason("Invalid report name."), new FaultCode("Server"), "GetData");
        report.SaveLayoutToXml(Path.Combine(ReportDirectory, url + FileExtension));
    }

    public override string SetNewData(XtraReport report, string defaultUrl)
    {
        // Allows you to validate and correct the specified name (URL).
        // This method also allows you to return the resulting name (URL),
        // and to save your report to a storage. The method is called only for new reports.
        SetData(report, defaultUrl);
        return defaultUrl;
    }
}