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

[Authorize(Roles = "Patient")]
public class PatientController : Controller
{
    private readonly HospitalDbContext _context;
    private readonly INotificationService _notificationService;

    public PatientController(HospitalDbContext context, INotificationService notificationService)
    {
        _context = context;
        _notificationService = notificationService;
    }

    private async Task<PatientProfile?> GetCurrentPatientProfileAsync()
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId)) return null;

        return await _context.PatientProfiles
            .Include(p => p.User)
            .FirstOrDefaultAsync(p => p.UserId == userId);
    }

    public async Task<IActionResult> Dashboard()
    {
        var patient = await GetCurrentPatientProfileAsync();
        if (patient == null) return RedirectToAction("Login", "Account");

        // Stats
        ViewBag.PendingRequestsCount = await _context.Appointments
            .CountAsync(a => a.PatientId == patient.Id && a.Status == "Pending");

        ViewBag.ApprovedRequestsCount = await _context.Appointments
            .CountAsync(a => a.PatientId == patient.Id && a.Status == "Approved");

        ViewBag.RejectedRequestsCount = await _context.Appointments
            .CountAsync(a => a.PatientId == patient.Id && a.Status == "Rejected");

        // Next Appointment Info
        var patientLocalTime = TimezoneHelper.GetUserLocalTime(HttpContext);
        var nextApp = await _context.Appointments
            .Include(a => a.Doctor).ThenInclude(d => d.User)
            .Where(a => a.PatientId == patient.Id && a.Status == "Approved" && a.AppointmentDate >= patientLocalTime)
            .OrderBy(a => a.AppointmentDate)
            .FirstOrDefaultAsync();
        ViewBag.NextAppointment = nextApp;

        // Appointment List
        var appointments = await _context.Appointments
            .Include(a => a.Doctor).ThenInclude(d => d.User)
            .Where(a => a.PatientId == patient.Id)
            .OrderByDescending(a => a.AppointmentDate)
            .ToListAsync();

        // Recent Notifications
        var notifications = await _context.Notifications
            .Where(n => n.UserId == patient.UserId)
            .OrderByDescending(n => n.CreatedAt)
            .Take(4)
            .ToListAsync();

        ViewBag.Appointments = appointments;
        ViewBag.Notifications = notifications;
        ViewBag.PatientName = patient.User.FullName;

        return View();
    }

    [HttpGet]
    public async Task<IActionResult> BookAppointment(string? specialty = null)
    {
        var query = _context.DoctorProfiles.Include(d => d.User).Where(d => d.Status == "Approved");
        
        if (!string.IsNullOrEmpty(specialty))
        {
            query = query.Where(d => d.Specialization == specialty);
        }

        var doctors = await query.ToListAsync();
        
        // Pass specializations for filter dropdown
        var specialities = await _context.DoctorProfiles
            .Where(d => d.Status == "Approved")
            .Select(d => d.Specialization)
            .Distinct()
            .ToListAsync();

        ViewBag.Specialities = specialities;
        ViewBag.SelectedSpecialty = specialty;
        ViewBag.PatientLocalTime = TimezoneHelper.GetUserLocalTime(HttpContext);
        
        return View(doctors);
    }

    [HttpGet]
    public async Task<IActionResult> GetDoctorSchedule(int doctorId)
    {
        var doctor = await _context.DoctorProfiles
            .Include(d => d.User)
            .FirstOrDefaultAsync(d => d.Id == doctorId);
        
        if (doctor == null) return NotFound();

        var availability = await _context.Availabilities
            .Where(a => a.DoctorId == doctorId)
            .OrderBy(a => a.DayOfWeek)
            .ToListAsync();

        // Helper to format days
        string GetDayName(int day) => day switch
        {
            0 => "Sunday", 1 => "Monday", 2 => "Tuesday", 3 => "Wednesday",
            4 => "Thursday", 5 => "Friday", 6 => "Saturday", _ => "Unknown"
        };

        var scheduleData = availability.Select(a => new {
            day = GetDayName(a.DayOfWeek),
            dayNumber = a.DayOfWeek,
            start = DateTime.Today.Add(a.StartTime).ToString("hh:mm tt"),
            end = DateTime.Today.Add(a.EndTime).ToString("hh:mm tt")
        }).ToList();

        return Json(new {
            doctorName = doctor.User.FullName,
            specialization = doctor.Specialization,
            schedule = scheduleData
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateAppointment(int doctorId, DateTime appointmentDate, string symptoms)
    {
        var patient = await GetCurrentPatientProfileAsync();
        if (patient == null) return RedirectToAction("Login", "Account");

        var doctor = await _context.DoctorProfiles.Include(d => d.User).FirstOrDefaultAsync(d => d.Id == doctorId);
        if (doctor == null) return NotFound();

        // 1. Validate that the date is in the present or future
        var patientLocalTime = TimezoneHelper.GetUserLocalTime(HttpContext);
        if (appointmentDate < patientLocalTime)
        {
            TempData["ErrorMessage"] = "You cannot book an appointment in the past.";
            return RedirectToAction(nameof(BookAppointment));
        }

        // 2. Validate that the appointment falls within the doctor's availability shift hours
        int selectDayOfWeek = (int)appointmentDate.DayOfWeek;
        var appTime = appointmentDate.TimeOfDay;
        var matchedAvailability = await _context.Availabilities
            .FirstOrDefaultAsync(a => a.DoctorId == doctorId 
                                   && a.DayOfWeek == selectDayOfWeek
                                   && appTime >= a.StartTime 
                                   && appTime <= a.EndTime);

        if (matchedAvailability == null)
        {
            TempData["ErrorMessage"] = $"Dr. {doctor.User.FullName} is not scheduled to work at the selected time ({appointmentDate:hh:mm tt}) on {appointmentDate.DayOfWeek}. Please review their schedule.";
            return RedirectToAction(nameof(BookAppointment));
        }

        // 3. Double-booking check: verify that no other patient has booked an approved slot within a 30-minute window
        var slotStart = appointmentDate.AddMinutes(-29);
        var slotEnd = appointmentDate.AddMinutes(29);
        var isDoubleBooked = await _context.Appointments
            .AnyAsync(a => a.DoctorId == doctorId 
                        && a.Status == "Approved" 
                        && a.AppointmentDate > slotStart 
                        && a.AppointmentDate < slotEnd);

        if (isDoubleBooked)
        {
            TempData["ErrorMessage"] = "This time slot is already booked by another patient. Please select a different time.";
            return RedirectToAction(nameof(BookAppointment));
        }

        // 4. Create and Auto-Approve the appointment
        var appointment = new Appointment
        {
            PatientId = patient.Id,
            DoctorId = doctorId,
            AppointmentDate = appointmentDate,
            SymptomDescription = symptoms,
            Status = "Approved", // Directly booked & approved
            CreatedAt = DateTime.Now
        };

        _context.Appointments.Add(appointment);
        await _context.SaveChangesAsync();

        // 5. Send notifications to patient and doctor
        await _notificationService.SendNotificationAsync(patient.UserId, $"Your appointment with Dr. {doctor.User.FullName} for {appointmentDate:dd MMM yyyy, hh:mm tt} has been successfully booked and confirmed!");
        await _notificationService.SendNotificationAsync(doctor.UserId, $"Patient {patient.User.FullName} has booked an appointment with you for {appointmentDate:dd MMM yyyy, hh:mm tt}.");

        TempData["SuccessMessage"] = $"Appointment booked successfully with Dr. {doctor.User.FullName}!";
        return RedirectToAction(nameof(Dashboard));
    }

    [HttpGet]
    public async Task<IActionResult> Notifications()
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId)) return RedirectToAction("Login", "Account");

        var list = await _context.Notifications
            .Where(n => n.UserId == userId)
            .OrderByDescending(n => n.CreatedAt)
            .ToListAsync();

        return View(list);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkAsRead(int id)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId)) return RedirectToAction("Login", "Account");

        var notification = await _context.Notifications
            .FirstOrDefaultAsync(n => n.Id == id && n.UserId == userId);

        if (notification != null)
        {
            notification.IsRead = true;
            await _context.SaveChangesAsync();
        }

        return RedirectToAction(nameof(Notifications));
    }

    [HttpGet]
    public async Task<IActionResult> GetBookedSlots(int doctorId, string date)
    {
        if (!DateTime.TryParse(date, out DateTime parsedDate))
        {
            return BadRequest("Invalid date format.");
        }

        var bookedTimes = await _context.Appointments
            .Where(a => a.DoctorId == doctorId 
                     && (a.Status == "Approved" || a.Status == "Conducted") 
                     && a.AppointmentDate.Date == parsedDate.Date)
            .Select(a => a.AppointmentDate.ToString("HH:mm"))
            .ToListAsync();

        int selectDayOfWeek = (int)parsedDate.DayOfWeek;
        var shifts = await _context.Availabilities
            .Where(a => a.DoctorId == doctorId && a.DayOfWeek == selectDayOfWeek)
            .Select(a => new {
                start = a.StartTime.ToString(@"hh\:mm"),
                end = a.EndTime.ToString(@"hh\:mm")
            })
            .ToListAsync();

        return Json(new {
            bookedTimes = bookedTimes,
            shifts = shifts
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UploadPrescription(int appointmentId, Microsoft.AspNetCore.Http.IFormFile prescriptionFile)
    {
        var patient = await GetCurrentPatientProfileAsync();
        if (patient == null) return RedirectToAction("Login", "Account");

        var appointment = await _context.Appointments
            .Include(a => a.Doctor).ThenInclude(d => d.User)
            .FirstOrDefaultAsync(a => a.Id == appointmentId && a.PatientId == patient.Id);

        if (appointment == null) return NotFound();

        if (prescriptionFile != null && prescriptionFile.Length > 0)
        {
            var extension = System.IO.Path.GetExtension(prescriptionFile.FileName).ToLower();
            var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".pdf" };
            if (!allowedExtensions.Contains(extension))
            {
                TempData["ErrorMessage"] = "Only JPG, PNG, or PDF files are allowed.";
                return RedirectToAction(nameof(Dashboard));
            }

            var uploadDir = System.IO.Path.Combine(System.IO.Directory.GetCurrentDirectory(), "wwwroot", "uploads", "prescriptions");
            if (!System.IO.Directory.Exists(uploadDir))
            {
                System.IO.Directory.CreateDirectory(uploadDir);
            }

            var uniqueFileName = Guid.NewGuid().ToString() + extension;
            var filePath = System.IO.Path.Combine(uploadDir, uniqueFileName);

            using (var stream = new System.IO.FileStream(filePath, System.IO.FileMode.Create))
            {
                await prescriptionFile.CopyToAsync(stream);
            }

            if (!string.IsNullOrEmpty(appointment.PatientPrescriptionPath))
            {
                var oldPhysicalPath = System.IO.Path.Combine(System.IO.Directory.GetCurrentDirectory(), "wwwroot", appointment.PatientPrescriptionPath.TrimStart('/'));
                if (System.IO.File.Exists(oldPhysicalPath))
                {
                    System.IO.File.Delete(oldPhysicalPath);
                }
            }

            appointment.PatientPrescriptionPath = "/uploads/prescriptions/" + uniqueFileName;
            await _context.SaveChangesAsync();

            await _notificationService.SendNotificationAsync(appointment.Doctor.UserId, 
                $"Patient {patient.User.FullName} uploaded a clinical report/prescription for your conducted appointment on {appointment.AppointmentDate:dd MMM yyyy}.");

            TempData["SuccessMessage"] = "Prescription report uploaded successfully!";
        }
        else
        {
            TempData["ErrorMessage"] = "Please select a valid file to upload.";
        }

        return RedirectToAction(nameof(Dashboard));
    }

    [HttpGet]
    public async Task<IActionResult> GetAppointmentsJson()
    {
        var patient = await GetCurrentPatientProfileAsync();
        if (patient == null) return Unauthorized();

        var appointments = await _context.Appointments
            .Include(a => a.Doctor).ThenInclude(d => d.User)
            .Where(a => a.PatientId == patient.Id && a.Status == "Approved")
            .ToListAsync();

        var events = appointments.Select(a => new
        {
            id = a.Id,
            title = $"Appointment: Dr. {a.Doctor.User.FullName}",
            start = a.AppointmentDate.ToString("yyyy-MM-ddTHH:mm:ss"),
            end = a.AppointmentDate.AddMinutes(20).ToString("yyyy-MM-ddTHH:mm:ss"),
            className = "border-start border-success border-3"
        });

        return Json(events);
    }
}
