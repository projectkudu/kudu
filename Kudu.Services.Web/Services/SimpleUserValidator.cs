using Kudu.Services.Authorization;

namespace Kudu.Services.Web {
    public class SimpleUserValidator : IUserValidator {
        public bool Validate(string username, string password) {
            return username == "admin" && password == "kudu";
        }
    }
}