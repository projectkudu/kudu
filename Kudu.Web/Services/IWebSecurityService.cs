namespace Kudu.Web.Services
{
    public interface IWebSecurityService
    {
        int MinPasswordLength { get; set; }

        bool ChangePassword(string userName, string oldPassword, string newPassword);

        bool ConfirmAccount(string confirmationToken);

        void Logout();

        void Login(string userName, string password);

        string CreateUserAndAccount(string userName, string password, bool requireConfirmationToken);

        bool Login(string userName, string password, bool rememberMe);
    }
}
