using System;
using System.Collections;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Xml;

namespace Kudu.SiteManagement.Configuration.Section
{
    public abstract class NamedElementCollection<T> : ConfigurationElementCollection, ICollection<T> where T : NamedConfigurationElement
    {
        public IEnumerable<T> Items
        {
            get { return this; }
        }

        protected override ConfigurationElement CreateNewElement()
        {
            //Note: We should hit OnDeserializeUnrecognizedElement instead with this type of configuration block.
            throw new ConfigurationErrorsException(); 
        }

        protected override bool OnDeserializeUnrecognizedElement(string elementName, XmlReader reader)
        {
            Type elementType = ResolveTypeName(elementName);
            if(elementType == null)
                return base.OnDeserializeUnrecognizedElement(elementName, reader);

            NamedConfigurationElement element = Activator.CreateInstance(elementType) as NamedConfigurationElement;
            if (element == null)
                return base.OnDeserializeUnrecognizedElement(elementName, reader);

            element.DeserializeElement(reader);
            BaseAdd(element, true);
            return true;
        }

        protected abstract Type ResolveTypeName(string elementName);

        #region IEnumerator<T> / ICollection<T> Implementation

        IEnumerator<T> IEnumerable<T>.GetEnumerator()
        {
            return this.OfType<T>().GetEnumerator();
        }

        public void Add(T item)
        {
            BaseAdd(item);
        }

        public void Clear()
        {
            BaseClear();
        }

        public bool Contains(T item)
        {
            return BaseIndexOf(item) != -1;
        }

        public void CopyTo(T[] array, int arrayIndex)
        {
            CopyTo(array, arrayIndex);
        }

        public bool Remove(T item)
        {
            if (BaseIndexOf(item) == -1)
                return false;

            BaseRemove(item);
            return true;
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1033:InterfaceMethodsShouldBeCallableByChildTypes", Scope = "member", Target = "Kudu.SiteManagement.Configuration.Section.NamedElementCollection`1.#System.Collections.Generic.ICollection`1<!0>.IsReadOnly", Justification = "This conflicts with the method IsReadOnly() of the base class which provides the same functionality.")]
        bool ICollection<T>.IsReadOnly
        {
            get
            {
                return base.IsReadOnly();
            }
        }

        #endregion
    }
}