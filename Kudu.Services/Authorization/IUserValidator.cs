
namespace Kudu.Services.Authorization
{
    public interface IUserValidator
    {
        bool Validate(string username, string password);
    }
}
