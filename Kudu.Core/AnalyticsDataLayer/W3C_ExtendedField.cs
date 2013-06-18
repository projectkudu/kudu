using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Kudu.Core.AnalyticsDataLayer
{
    class W3C_ExtendedField:LogFields, IComparable
    {
        public W3C_ExtendedField(string fieldName)
        {
            LogField = fieldName;
        }

        public override string Field
        {
            get { return LogField; }
        }

        public int CompareTo(object obj)
        {
            W3C_ExtendedField other = obj as W3C_ExtendedField;
            if (other != null)
            {
                return String.Compare(this.LogField, other.LogField, StringComparison.OrdinalIgnoreCase);
            }
            else
            {
                throw new ArgumentException("object is not of type W3C_ExtendedField");
            }
        }

        public override string ToString()
        {
            return LogField;
        }

        protected override string LogField { get; set; }
    }
}
