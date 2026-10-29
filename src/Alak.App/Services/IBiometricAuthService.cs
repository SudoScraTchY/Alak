using System.Threading.Tasks;

namespace Alak.App.Services;

public interface IBiometricAuthService
{
    bool IsAvailable();
    Task<bool> AuthenticateAsync(string title, string subtitle, string cancelText = "Cancel");
}
