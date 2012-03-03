using System.Web.Security;
using Kudu.Services.Authorization;

namespace Kudu.Services.Web
{
    public class MembershipUserValidator : IUserValidator
    {
        public bool Validate(string username, string password)
        {
            return Membership.ValidateUser(username, password);
        }
    }
}