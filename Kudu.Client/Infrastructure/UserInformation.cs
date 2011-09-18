namespace Kudu.Client.Infrastructure {
    public class UserInformation : IUserInformation {
        public string UserName {
            get {
                return "Test <foo@test.com>";
            }
        }
    }
}
