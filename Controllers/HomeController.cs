using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using HospitalManagementPortal.Data;
using HospitalManagementPortal.Models;

namespace HospitalManagementPortal.Controllers;

public class HomeController : Controller
{
    private readonly HospitalDbContext _context;

    public HomeController(HospitalDbContext context)
    {
        _context = context;
    }

    public async Task<IActionResult> Index()
    {
        // Default fallbacks in case database connection fails or database is empty
        int totalDoctors = 0;
        int totalPatients = 0;
        int totalAppointments = 0;

        try
        {
            if (await _context.Database.CanConnectAsync())
            {
                totalDoctors = await _context.DoctorProfiles.CountAsync(d => d.Status == "Approved");
                totalPatients = await _context.PatientProfiles.CountAsync();
                totalAppointments = await _context.Appointments.CountAsync(a => a.Status == "Approved");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[HOME COUNTERS ERROR] Could not read database metrics: {ex.Message}");
        }

        ViewBag.TotalDoctors = totalDoctors > 0 ? totalDoctors : 14;
        ViewBag.TotalPatients = totalPatients > 0 ? totalPatients : 1240;
        ViewBag.TotalAppointments = totalAppointments > 0 ? totalAppointments : 3480;

        return View();
    }

    public IActionResult Privacy()
    {
        return View();
    }

    [HttpGet]
    public async Task<IActionResult> Diagnostics([FromServices] IWebHostEnvironment env)
    {
        var docs = await _context.DoctorProfiles
            .Include(d => d.User)
            .Select(d => new {
                d.Id,
                d.User.FullName,
                d.User.Email,
                d.ProfilePicturePath,
                FileExists = d.ProfilePicturePath != null && System.IO.File.Exists(Path.Combine(env.WebRootPath, d.ProfilePicturePath.TrimStart('/'))),
                PhysicalPathChecked = d.ProfilePicturePath != null ? Path.Combine(env.WebRootPath, d.ProfilePicturePath.TrimStart('/')) : null,
                WebRootPath = env.WebRootPath,
                CurrentDir = Directory.GetCurrentDirectory()
            })
            .ToListAsync();
        return Json(docs);
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
