using System.Threading.Tasks;

namespace HospitalManagementPortal.Services;

public interface INotificationService
{
    Task SendNotificationAsync(string userId, string message);
}
