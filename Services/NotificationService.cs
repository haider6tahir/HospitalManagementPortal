using System;
using System.Threading.Tasks;
using HospitalManagementPortal.Data;
using HospitalManagementPortal.Models;

namespace HospitalManagementPortal.Services;

public class NotificationService : INotificationService
{
    private readonly HospitalDbContext _context;

    public NotificationService(HospitalDbContext context)
    {
        _context = context;
    }

    public async Task SendNotificationAsync(string userId, string message)
    {
        try
        {
            var notification = new Notification
            {
                UserId = userId,
                Message = message,
                IsRead = false,
                CreatedAt = DateTime.Now
            };

            _context.Notifications.Add(notification);
            await _context.SaveChangesAsync();

            // Simulate email printing in logs
            Console.WriteLine($"[NOTIFICATION EMAIL SIMULATION] To: {userId} | Msg: {message}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[NOTIFICATION ERROR] Failed to save/send notification: {ex.Message}");
        }
    }
}
