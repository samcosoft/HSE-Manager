using DevExpress.Xpo;
using DevExpress.Xpo.Metadata;
using Microsoft.AspNetCore.Components;

namespace Samco_HSE_Manager.Pages.Shared;

public partial class ReportDesign
{
    [Inject] private IDataLayer DataLayer { get; set; } = null!;

    private Session? Session1 { get; set; }
    private readonly Dictionary<string, object> _hseDataSources = new();
    protected override void OnInitialized()
    {
        Session1 = new Session(DataLayer);
        foreach (XPClassInfo info in Session1.Dictionary.Classes)
        {
            if (info.IsPersistent && info.IsVisibleInDesignTime)
            {
                var dataSource = new XPObjectSource
                {
                    ConnectionStringName = "MainDatabase"
                };
                dataSource.SetEntityType(info.ClassType);
                _hseDataSources.Add(info.TableName, dataSource); ;
            }
        }

    }
}