using Microsoft.AspNetCore.Http;
using System;

namespace HospitalManagementPortal.Services;

public static class TimezoneHelper
{
    public static DateTime GetUserLocalTime(HttpContext httpContext)
    {
        if (httpContext.Request.Cookies.TryGetValue("timezoneoffset", out string? offsetStr) && 
            int.TryParse(offsetStr, out int offsetMinutes))
        {
            return DateTime.UtcNow.AddMinutes(-offsetMinutes);
        }
        return DateTime.Now; // Fallback to server local time
    }
}
