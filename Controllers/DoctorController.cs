using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using HospitalManagementPortal.Data;
using HospitalManagementPortal.Models;
using HospitalManagementPortal.Services;

namespace HospitalManagementPortal.Controllers;

[Authorize(Roles = "Doctor")]
public class DoctorController : Controller
{
    private readonly HospitalDbContext _context;
    private readonly INotificationService _notificationService;

    public DoctorController(HospitalDbContext context, INotificationService notificationService)
    {
        _context = context;
        _notificationService = notificationService;
    }

    private async Task<DoctorProfile?> GetCurrentDoctorProfileAsync()
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId)) return null;

        return await _context.DoctorProfiles
            .Include(d => d.User)
            .FirstOrDefaultAsync(d => d.UserId == userId);
    }

    public async Task<IActionResult> Dashboard()
    {
        var doc = await GetCurrentDoctorProfileAsync();
        if (doc == null) return RedirectToAction("Login", "Account");

        var doctorLocalTime = TimezoneHelper.GetUserLocalTime(HttpContext);
        var doctorLocalDate = doctorLocalTime.Date;

        // Statistics
        ViewBag.TodayAppointmentsCount = await _context.Appointments
            .CountAsync(a => a.DoctorId == doc.Id && a.Status == "Approved" && EF.Functions.DateDiffDay(a.AppointmentDate, doctorLocalDate) == 0);

        ViewBag.UpcomingAppointmentsCount = await _context.Appointments
            .CountAsync(a => a.DoctorId == doc.Id && a.Status == "Approved" && a.AppointmentDate > doctorLocalTime);

        ViewBag.ConsultationsCompleted = await _context.Appointments
            .CountAsync(a => a.DoctorId == doc.Id && !string.IsNullOrEmpty(a.ConsultationNotes));

        // Lists
        var todayAppointments = await _context.Appointments
            .Include(a => a.Patient).ThenInclude(p => p.User)
            .Where(a => a.DoctorId == doc.Id && a.Status == "Approved" && EF.Functions.DateDiffDay(a.AppointmentDate, doctorLocalDate) == 0)
            .OrderBy(a => a.AppointmentDate)
            .ToListAsync();

        var upcomingAppointments = await _context.Appointments
            .Include(a => a.Patient).ThenInclude(p => p.User)
            .Where(a => a.DoctorId == doc.Id && a.Status == "Approved" && a.AppointmentDate > doctorLocalTime && EF.Functions.DateDiffDay(a.AppointmentDate, doctorLocalDate) != 0)
            .OrderBy(a => a.AppointmentDate)
            .ToListAsync();

        ViewBag.TodayAppointments = todayAppointments;
        ViewBag.UpcomingAppointments = upcomingAppointments;
        ViewBag.DoctorName = doc.User.FullName;

        return View();
    }

    [HttpGet]
    public async Task<IActionResult> Availability()
    {
        var doc = await GetCurrentDoctorProfileAsync();
        if (doc == null) return RedirectToAction("Login", "Account");

        var schedule = await _context.Availabilities
            .Where(a => a.DoctorId == doc.Id)
            .OrderBy(a => a.DayOfWeek)
            .ToListAsync();

        return View(schedule);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddAvailability(int dayOfWeek, string startTime, string endTime)
    {
        var doc = await GetCurrentDoctorProfileAsync();
        if (doc == null) return RedirectToAction("Login", "Account");

        if (TimeSpan.TryParse(startTime, out var start) && TimeSpan.TryParse(endTime, out var end))
        {
            if (end <= start)
            {
                TempData["ErrorMessage"] = "End time must be after start time.";
                return RedirectToAction(nameof(Availability));
            }

            var availability = new Availability
            {
                DoctorId = doc.Id,
                DayOfWeek = dayOfWeek,
                StartTime = start,
                EndTime = end
            };

            _context.Availabilities.Add(availability);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Availability block added successfully.";
        }
        else
        {
            TempData["ErrorMessage"] = "Invalid time format.";
        }

        return RedirectToAction(nameof(Availability));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RemoveAvailability(int id)
    {
        var doc = await GetCurrentDoctorProfileAsync();
        if (doc == null) return RedirectToAction("Login", "Account");

        var av = await _context.Availabilities.FirstOrDefaultAsync(a => a.Id == id && a.DoctorId == doc.Id);
        if (av != null)
        {
            _context.Availabilities.Remove(av);
            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = "Availability block removed successfully.";
        }

        return RedirectToAction(nameof(Availability));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddConsultationNotes(int appointmentId, string notes)
    {
        var doc = await GetCurrentDoctorProfileAsync();
        if (doc == null) return RedirectToAction("Login", "Account");

        var appointment = await _context.Appointments
            .Include(a => a.Patient).ThenInclude(p => p.User)
            .FirstOrDefaultAsync(a => a.Id == appointmentId && a.DoctorId == doc.Id);

        if (appointment == null) return NotFound();

        appointment.ConsultationNotes = notes;
        await _context.SaveChangesAsync();

        // Send patient notification
        await _notificationService.SendNotificationAsync(appointment.Patient.UserId, $"Dr. {doc.User.FullName} has added consultation notes to your appointment from {appointment.AppointmentDate:dd MMM yyyy}.");

        TempData["SuccessMessage"] = "Consultation notes saved successfully.";
        return RedirectToAction(nameof(Dashboard));
    }

    public async Task<IActionResult> Consultations()
    {
        var doc = await GetCurrentDoctorProfileAsync();
        if (doc == null) return RedirectToAction("Login", "Account");

        var consultations = await _context.Appointments
            .Include(a => a.Patient).ThenInclude(p => p.User)
            .Where(a => a.DoctorId == doc.Id && !string.IsNullOrEmpty(a.ConsultationNotes))
            .OrderByDescending(a => a.AppointmentDate)
            .ToListAsync();

        return View(consultations);
    }
}
