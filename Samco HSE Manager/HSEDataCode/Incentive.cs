using System;
using DevExpress.Xpo;
using DevExpress.Xpo.Metadata;
using DevExpress.Data.Filtering;
using System.Collections.Generic;
using System.ComponentModel;
using System.Reflection;
namespace Samco_HSE.HSEData
{

    public partial class Incentive
    {
        public Incentive() : base(Session.DefaultSession) { }
        public Incentive(Session session) : base(session) { }
        public override void AfterConstruction() { base.AfterConstruction(); }
    }

}
