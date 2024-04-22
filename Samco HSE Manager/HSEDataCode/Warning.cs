using System;
using DevExpress.Xpo;
using DevExpress.Xpo.Metadata;
using DevExpress.Data.Filtering;
using System.Collections.Generic;
using System.ComponentModel;
using System.Reflection;
namespace Samco_HSE.HSEData
{

    public partial class Warning
    {
        public Warning(Session session) : base(session) { }
        public override void AfterConstruction() { base.AfterConstruction(); }
    }

}
