using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Kudu.Web.Services
{
    public interface IMessengerService
    {
        void Send(string fromAddress, string toAddress, string subject, string body, bool p);
    }
}
