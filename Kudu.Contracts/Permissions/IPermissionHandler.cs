namespace Kudu.Contracts.Permissions
{
    /// <summary>
    /// This handler is meant for Linux enviroment only
    /// </summary>
    public interface IPermissionHandler
    {
        void Chmod(string permission, string filePath);
    }
}
