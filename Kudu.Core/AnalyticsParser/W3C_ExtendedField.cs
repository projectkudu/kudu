using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Kudu.Core.AnalyticsParser
{
    class W3C_ExtendedField:LogFields, IComparable
    {
        public W3C_ExtendedField(string fieldName)
        {
            logField = fieldName;
        }

        public override string Field
        {
            get { return logField; }
        }

        public int CompareTo(object obj)
        {
            W3C_ExtendedField other = obj as W3C_ExtendedField;
            if (other != null)
            {
                return String.Compare(this.logField, other.logField, StringComparison.OrdinalIgnoreCase);
            }
            else
            {
                throw new ArgumentException("object is not of type W3C_ExtendedField");
            }
        }

        public override string ToString()
        {
            return base.logField;
        }
    }
}
