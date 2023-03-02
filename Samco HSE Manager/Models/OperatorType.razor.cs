using DevExpress.Blazor;
using Microsoft.AspNetCore.Components;

namespace Samco_HSE_Manager.Models;

public partial class OperatorType
{
    [Parameter] public GridDataColumnFilterRowCellTemplateContext FilterContext { get; set; } = null!;
    [Parameter] public RenderFragment ChildContent { get; set; } = null!;
    [Parameter] public bool ShowFilterButton { get; set; }

    List<OperatorTypeWrapper> OperatorTypes { get; set; } = null!;

    OperatorTypeWrapper CurrentOperatorType
    {
        get => OperatorTypes.First(ot => ot.Value == FilterContext.DataColumn.FilterRowOperatorType);
        set
        {
            FilterContext.Grid.BeginUpdate();
            FilterContext.DataColumn.FilterRowOperatorType = value.Value;
            FilterContext.Grid.EndUpdate();
            IsOpen = false;
        }
    }
    bool IsOpen { get; set; }
    string PositionTargetId => $"dropdown-target-container-{FilterContext.DataColumn.FieldName}";
    string IconCssClass => $"icon-class-{FilterContext.DataColumn.FieldName}";
    protected override void OnInitialized()
    {
        OperatorTypes = Enum.GetValues(typeof(GridFilterRowOperatorType))
            .OfType<GridFilterRowOperatorType>()
            .Select(t => new OperatorTypeWrapper(t, GetPersianName(t.ToString()))).ToList();
    }

    private string GetPersianName(string displayText)
    {
        return displayText switch
        {
            "Default" => "پیش فرض",
            "Equal" => "مساوی",
            "NotEqual" => "مخالف",
            "StartsWith" => "شروع با",
            "EndsWith" => "پایان با",
            "Contains" => "شامل",
            "Less" => "کمتر از",
            "LessOrEqual" => "کمتر یا مساوی",
            "Greater" => "بیشتر از",
            "GreaterOrEqual" => "بیشتر یا مساوی",
            _ => string.Empty
        };
    }

    class OperatorTypeWrapper
    {
        public OperatorTypeWrapper(GridFilterRowOperatorType value, string displayText)
        {
            Value = value;
            DisplayText = displayText;
        }
        public GridFilterRowOperatorType Value { get; set; }
        public string DisplayText { get; set; }
        public string IconPath => $"images/filterIcons/{Value}.svg";
    };
}