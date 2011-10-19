using Kudu.Services.Authorization;
using System.Web.Security;

namespace Kudu.Services.Web {
    public class FormsAuthUserValidator : IUserValidator {
        public bool Validate(string username, string password) {
            return Membership.ValidateUser(username, password);
        }
    }
}