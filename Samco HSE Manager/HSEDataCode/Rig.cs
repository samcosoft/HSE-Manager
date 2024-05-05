using System;
using DevExpress.Xpo;
using DevExpress.Xpo.Metadata;
using DevExpress.Data.Filtering;
using System.Collections.Generic;
using System.ComponentModel;
using System.Reflection;
namespace Samco_HSE.HSEData
{

    public partial class Rig
    {
        public Rig() : base(Session.DefaultSession) { }
        public Rig(Session session) : base(session) { }
        public override void AfterConstruction() { base.AfterConstruction(); }
    }

}
