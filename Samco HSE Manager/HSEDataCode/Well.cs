using System;
using DevExpress.Xpo;
using DevExpress.Xpo.Metadata;
using DevExpress.Data.Filtering;
using System.Collections.Generic;
using System.ComponentModel;
using System.Reflection;
namespace Samco_HSE.HSEData
{

    public partial class Well
    {
        public Well() : base(Session.DefaultSession) { }
        public Well(Session session) : base(session) { }
        public override void AfterConstruction() { base.AfterConstruction(); }
    }

}
