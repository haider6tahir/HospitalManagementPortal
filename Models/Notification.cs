using System;

namespace HospitalManagementPortal.Models;

public class Notification
{
    public int Id { get; set; }
    public string UserId { get; set; } = null!;
    public string Message { get; set; } = null!;
    public bool IsRead { get; set; }
    public DateTime CreatedAt { get; set; }

    public virtual User User { get; set; } = null!;
}
