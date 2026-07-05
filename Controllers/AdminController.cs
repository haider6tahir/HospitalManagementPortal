using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using HospitalManagementPortal.Data;
using HospitalManagementPortal.Models;
using HospitalManagementPortal.Services;

namespace HospitalManagementPortal.Controllers;

[Authorize(Roles = "Admin")]
public class AdminController : Controller
{
    private readonly HospitalDbContext _context;
    private readonly INotificationService _notificationService;
    private readonly IPasswordService _passwordService;
    private readonly IWebHostEnvironment _webHostEnvironment;

    public AdminController(
        HospitalDbContext context, 
        INotificationService notificationService,
        IPasswordService passwordService,
        IWebHostEnvironment webHostEnvironment)
    {
        _context = context;
        _notificationService = notificationService;
        _passwordService = passwordService;
        _webHostEnvironment = webHostEnvironment;
    }

    public async Task<IActionResult> Dashboard()
    {
        // 1. Statistics
        ViewBag.TotalPatients = await _context.PatientProfiles.CountAsync();
        ViewBag.TotalDoctors = await _context.DoctorProfiles.CountAsync(d => d.Status == "Approved");
        ViewBag.PendingDoctorApprovals = await _context.DoctorProfiles.CountAsync(d => d.Status == "Pending");
        
        var today = DateTime.Today;
        ViewBag.TodayAppointments = await _context.Appointments
            .CountAsync(a => EF.Functions.DateDiffDay(a.AppointmentDate, today) == 0);

        ViewBag.PendingAppointmentsCount = await _context.Appointments.CountAsync(a => a.Status == "Pending");
        ViewBag.ApprovedAppointmentsCount = await _context.Appointments.CountAsync(a => a.Status == "Approved");
        ViewBag.RejectedAppointmentsCount = await _context.Appointments.CountAsync(a => a.Status == "Rejected");

        // Calculate Available Doctors Today
        int dayOfWeekInt = (int)DateTime.Today.DayOfWeek;
        var availableDoctorIds = await _context.Availabilities
            .Where(av => av.DayOfWeek == dayOfWeekInt)
            .Select(av => av.DoctorId)
            .Distinct()
            .ToListAsync();
        ViewBag.AvailableDoctorsToday = await _context.DoctorProfiles
            .CountAsync(d => d.Status == "Approved" && availableDoctorIds.Contains(d.Id));

        // 2. Pending Doctors List
        var pendingDoctors = await _context.DoctorProfiles
            .Include(d => d.User)
            .Where(d => d.Status == "Pending")
            .ToListAsync();

        // 3. Pending Appointments List
        var pendingAppointments = await _context.Appointments
            .Include(a => a.Patient).ThenInclude(p => p.User)
            .Include(a => a.Doctor).ThenInclude(d => d.User)
            .Where(a => a.Status == "Pending")
            .OrderBy(a => a.AppointmentDate)
            .ToListAsync();

        // 4. Recent Activity
        var recentNotifications = await _context.Notifications
            .Include(n => n.User)
            .OrderByDescending(n => n.CreatedAt)
            .Take(5)
            .ToListAsync();

        ViewBag.PendingDoctors = pendingDoctors;
        ViewBag.PendingAppointments = pendingAppointments;
        ViewBag.RecentActivity = recentNotifications;

        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ApproveDoctor(int id)
    {
        var doc = await _context.DoctorProfiles.Include(d => d.User).FirstOrDefaultAsync(d => d.Id == id);
        if (doc == null) return NotFound();

        doc.Status = "Approved";
        await _context.SaveChangesAsync();

        await _notificationService.SendNotificationAsync(doc.UserId, $"Dear {doc.User.FullName}, your doctor profile has been approved! You can now log in and manage your portal.");

        TempData["SuccessMessage"] = $"Doctor {doc.User.FullName} has been approved.";
        return RedirectToAction(nameof(Dashboard));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RejectDoctor(int id)
    {
        var doc = await _context.DoctorProfiles.Include(d => d.User).FirstOrDefaultAsync(d => d.Id == id);
        if (doc == null) return NotFound();

        doc.Status = "Rejected";
        await _context.SaveChangesAsync();

        await _notificationService.SendNotificationAsync(doc.UserId, $"Dear {doc.User.FullName}, your doctor registration was rejected by the administrator.");

        TempData["SuccessMessage"] = $"Doctor {doc.User.FullName} application has been rejected.";
        return RedirectToAction(nameof(Dashboard));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ApproveAppointment(int id)
    {
        var app = await _context.Appointments
            .Include(a => a.Patient).ThenInclude(p => p.User)
            .Include(a => a.Doctor).ThenInclude(d => d.User)
            .FirstOrDefaultAsync(a => a.Id == id);

        if (app == null) return NotFound();

        app.Status = "Approved";
        await _context.SaveChangesAsync();

        await _notificationService.SendNotificationAsync(app.Patient.UserId, $"Your appointment with Dr. {app.Doctor.User.FullName} on {app.AppointmentDate:dd MMM yyyy, hh:mm tt} has been approved!");
        await _notificationService.SendNotificationAsync(app.Doctor.UserId, $"New approved appointment booked by Patient {app.Patient.User.FullName} for {app.AppointmentDate:dd MMM yyyy, hh:mm tt}.");

        TempData["SuccessMessage"] = "Appointment approved successfully.";
        return RedirectToAction(nameof(Dashboard));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RejectAppointment(int id)
    {
        var app = await _context.Appointments
            .Include(a => a.Patient).ThenInclude(p => p.User)
            .Include(a => a.Doctor).ThenInclude(d => d.User)
            .FirstOrDefaultAsync(a => a.Id == id);

        if (app == null) return NotFound();

        app.Status = "Rejected";
        await _context.SaveChangesAsync();

        await _notificationService.SendNotificationAsync(app.Patient.UserId, $"Your appointment request with Dr. {app.Doctor.User.FullName} on {app.AppointmentDate:dd MMM yyyy} was declined.");

        TempData["SuccessMessage"] = "Appointment has been rejected.";
        return RedirectToAction(nameof(Dashboard));
    }

    public async Task<IActionResult> Doctors()
    {
        var doctors = await _context.DoctorProfiles
            .Include(d => d.User)
            .ToListAsync();
        return View(doctors);
    }

    public async Task<IActionResult> Patients()
    {
        var patients = await _context.PatientProfiles
            .Include(p => p.User)
            .ToListAsync();
        return View(patients);
    }

    public async Task<IActionResult> Appointments()
    {
        var appointments = await _context.Appointments
            .Include(a => a.Patient).ThenInclude(p => p.User)
            .Include(a => a.Doctor).ThenInclude(d => d.User)
            .OrderByDescending(a => a.AppointmentDate)
            .ToListAsync();
        return View(appointments);
    }

    // ==========================================
    // ADMIN DOCTOR CRUD ACTIONS
    // ==========================================

    [HttpGet]
    public IActionResult AddDoctor()
    {
        return View(new AdminDoctorViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddDoctor(AdminDoctorViewModel model)
    {
        if (string.IsNullOrEmpty(model.Password))
        {
            ModelState.AddModelError("Password", "Password is required for new doctors.");
        }

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var emailExists = await _context.Users.AnyAsync(u => u.Email == model.Email);
        if (emailExists)
        {
            ModelState.AddModelError("Email", "An account with this email address already exists.");
            return View(model);
        }

        string? imagePath = null;
        if (model.ProfileImage != null && model.ProfileImage.Length > 0)
        {
            var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif" };
            var extension = Path.GetExtension(model.ProfileImage.FileName).ToLower();
            if (!allowedExtensions.Contains(extension))
            {
                ModelState.AddModelError("ProfileImage", "Only image files (.jpg, .jpeg, .png, .gif) are allowed.");
                return View(model);
            }

            var uploadDir = Path.Combine(_webHostEnvironment.WebRootPath, "uploads", "doctors");
            if (!Directory.Exists(uploadDir))
            {
                Directory.CreateDirectory(uploadDir);
            }

            var uniqueFileName = Guid.NewGuid().ToString() + extension;
            var filePath = Path.Combine(uploadDir, uniqueFileName);

            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await model.ProfileImage.CopyToAsync(stream);
            }

            imagePath = "/uploads/doctors/" + uniqueFileName;
        }

        var user = new User
        {
            Id = Guid.NewGuid().ToString(),
            FullName = model.FullName,
            Email = model.Email,
            PasswordHash = _passwordService.HashPassword(model.Password!),
            Role = "Doctor",
            RegistrationDate = DateTime.Now,
            IsActive = true
        };

        var doctorProfile = new DoctorProfile
        {
            UserId = user.Id,
            Specialization = model.Specialization,
            Qualifications = model.Qualifications,
            Biography = model.Biography,
            ExperienceYears = model.ExperienceYears,
            Status = model.Status,
            ProfilePicturePath = imagePath
        };

        _context.Users.Add(user);
        _context.DoctorProfiles.Add(doctorProfile);
        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = $"Doctor Dr. {model.FullName} created successfully!";
        return RedirectToAction(nameof(Doctors));
    }

    [HttpGet]
    public async Task<IActionResult> EditDoctor(int id)
    {
        var doc = await _context.DoctorProfiles.Include(d => d.User).FirstOrDefaultAsync(d => d.Id == id);
        if (doc == null) return NotFound();

        var model = new AdminDoctorViewModel
        {
            Id = doc.Id,
            FullName = doc.User.FullName,
            Email = doc.User.Email,
            Specialization = doc.Specialization,
            Qualifications = doc.Qualifications,
            Biography = doc.Biography,
            ExperienceYears = doc.ExperienceYears,
            Status = doc.Status,
            ExistingImagePath = doc.ProfilePicturePath
        };

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditDoctor(AdminDoctorViewModel model)
    {
        if (model.Id == null) return NotFound();

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var doc = await _context.DoctorProfiles.Include(d => d.User).FirstOrDefaultAsync(d => d.Id == model.Id);
        if (doc == null) return NotFound();

        var emailExists = await _context.Users.AnyAsync(u => u.Email == model.Email && u.Id != doc.UserId);
        if (emailExists)
        {
            ModelState.AddModelError("Email", "An account with this email address already exists.");
            return View(model);
        }

        if (model.ProfileImage != null && model.ProfileImage.Length > 0)
        {
            var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif" };
            var extension = Path.GetExtension(model.ProfileImage.FileName).ToLower();
            if (!allowedExtensions.Contains(extension))
            {
                ModelState.AddModelError("ProfileImage", "Only image files (.jpg, .jpeg, .png, .gif) are allowed.");
                model.ExistingImagePath = doc.ProfilePicturePath;
                return View(model);
            }

            // Delete old file if exists
            if (!string.IsNullOrEmpty(doc.ProfilePicturePath))
            {
                var oldFilePath = Path.Combine(_webHostEnvironment.WebRootPath, doc.ProfilePicturePath.TrimStart('/'));
                if (System.IO.File.Exists(oldFilePath))
                {
                    System.IO.File.Delete(oldFilePath);
                }
            }

            var uploadDir = Path.Combine(_webHostEnvironment.WebRootPath, "uploads", "doctors");
            if (!Directory.Exists(uploadDir))
            {
                Directory.CreateDirectory(uploadDir);
            }

            var uniqueFileName = Guid.NewGuid().ToString() + extension;
            var filePath = Path.Combine(uploadDir, uniqueFileName);

            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await model.ProfileImage.CopyToAsync(stream);
            }

            doc.ProfilePicturePath = "/uploads/doctors/" + uniqueFileName;
        }

        doc.User.FullName = model.FullName;
        doc.User.Email = model.Email;
        
        if (!string.IsNullOrEmpty(model.Password))
        {
            doc.User.PasswordHash = _passwordService.HashPassword(model.Password);
        }

        doc.Specialization = model.Specialization;
        doc.Qualifications = model.Qualifications;
        doc.Biography = model.Biography;
        doc.ExperienceYears = model.ExperienceYears;
        doc.Status = model.Status;

        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = $"Doctor Dr. {model.FullName} updated successfully!";
        return RedirectToAction(nameof(Doctors));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteDoctor(int id)
    {
        var doc = await _context.DoctorProfiles.Include(d => d.User).FirstOrDefaultAsync(d => d.Id == id);
        if (doc == null) return NotFound();

        // Delete profile picture from disk if exists
        if (!string.IsNullOrEmpty(doc.ProfilePicturePath))
        {
            var filePath = Path.Combine(_webHostEnvironment.WebRootPath, doc.ProfilePicturePath.TrimStart('/'));
            if (System.IO.File.Exists(filePath))
            {
                System.IO.File.Delete(filePath);
            }
        }

        // Delete the user record which cascades to the doctor profile automatically
        _context.Users.Remove(doc.User);
        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = $"Doctor account deleted successfully.";
        return RedirectToAction(nameof(Doctors));
    }

    // ==========================================
    // ADMIN PATIENT CRUD ACTIONS
    // ==========================================

    [HttpGet]
    public IActionResult AddPatient()
    {
        return View(new AdminPatientViewModel { DateOfBirth = DateTime.Today.AddYears(-20) });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddPatient(AdminPatientViewModel model)
    {
        if (string.IsNullOrEmpty(model.Password))
        {
            ModelState.AddModelError("Password", "Password is required for new patients.");
        }

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var emailExists = await _context.Users.AnyAsync(u => u.Email == model.Email);
        if (emailExists)
        {
            ModelState.AddModelError("Email", "An account with this email address already exists.");
            return View(model);
        }

        var user = new User
        {
            Id = Guid.NewGuid().ToString(),
            FullName = model.FullName,
            Email = model.Email,
            PasswordHash = _passwordService.HashPassword(model.Password!),
            Role = "Patient",
            RegistrationDate = DateTime.Now,
            IsActive = true
        };

        var patientProfile = new PatientProfile
        {
            UserId = user.Id,
            DateOfBirth = model.DateOfBirth,
            Gender = model.Gender,
            BloodGroup = model.BloodGroup,
            MedicalHistorySummary = model.MedicalHistorySummary
        };

        _context.Users.Add(user);
        _context.PatientProfiles.Add(patientProfile);
        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = $"Patient {model.FullName} created successfully!";
        return RedirectToAction(nameof(Patients));
    }

    [HttpGet]
    public async Task<IActionResult> EditPatient(int id)
    {
        var patient = await _context.PatientProfiles.Include(p => p.User).FirstOrDefaultAsync(p => p.Id == id);
        if (patient == null) return NotFound();

        var model = new AdminPatientViewModel
        {
            Id = patient.Id,
            FullName = patient.User.FullName,
            Email = patient.User.Email,
            DateOfBirth = patient.DateOfBirth,
            Gender = patient.Gender,
            BloodGroup = patient.BloodGroup,
            MedicalHistorySummary = patient.MedicalHistorySummary
        };

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditPatient(AdminPatientViewModel model)
    {
        if (model.Id == null) return NotFound();

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var patient = await _context.PatientProfiles.Include(p => p.User).FirstOrDefaultAsync(p => p.Id == model.Id);
        if (patient == null) return NotFound();

        var emailExists = await _context.Users.AnyAsync(u => u.Email == model.Email && u.Id != patient.UserId);
        if (emailExists)
        {
            ModelState.AddModelError("Email", "An account with this email address already exists.");
            return View(model);
        }

        patient.User.FullName = model.FullName;
        patient.User.Email = model.Email;

        if (!string.IsNullOrEmpty(model.Password))
        {
            patient.User.PasswordHash = _passwordService.HashPassword(model.Password);
        }

        patient.DateOfBirth = model.DateOfBirth;
        patient.Gender = model.Gender;
        patient.BloodGroup = model.BloodGroup;
        patient.MedicalHistorySummary = model.MedicalHistorySummary;

        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = $"Patient {model.FullName} updated successfully!";
        return RedirectToAction(nameof(Patients));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeletePatient(int id)
    {
        var patient = await _context.PatientProfiles.Include(p => p.User).FirstOrDefaultAsync(p => p.Id == id);
        if (patient == null) return NotFound();

        _context.Users.Remove(patient.User);
        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = $"Patient account deleted successfully.";
        return RedirectToAction(nameof(Patients));
    }
}
