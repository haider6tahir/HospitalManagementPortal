using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using HospitalManagementPortal.Data;
using HospitalManagementPortal.Models;
using HospitalManagementPortal.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();

// Register SQL Server DB Context
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContext<HospitalDbContext>(options =>
    options.UseSqlServer(connectionString));

// Custom Cookie Authentication
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login";
        options.LogoutPath = "/Account/Logout";
        options.AccessDeniedPath = "/Account/AccessDenied";
        options.ExpireTimeSpan = TimeSpan.FromHours(2);
    });

// Register Custom Security & Business Services
builder.Services.AddScoped<IPasswordService, PasswordService>();
builder.Services.AddScoped<INotificationService, NotificationService>();

var app = builder.Build();

// Seed Default Admin User if DB can connect and table is empty
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        var context = services.GetRequiredService<HospitalDbContext>();
        var passwordService = services.GetRequiredService<IPasswordService>();
        
        // Try to connect to verify database exists before seeding
        if (context.Database.CanConnect())
        {
            var adminUser = context.Users.FirstOrDefault(u => u.Role == "Admin");
            if (adminUser == null)
            {
                var admin = new User
                {
                    Id = Guid.NewGuid().ToString(),
                    FullName = "System Administrator",
                    Email = "admin@hospital.com",
                    PasswordHash = passwordService.HashPassword("Admin@123"),
                    Role = "Admin",
                    RegistrationDate = DateTime.Now,
                    IsActive = true
                };
                context.Users.Add(admin);
                context.SaveChanges();
                Console.WriteLine("[DB SEED] Default Admin seeded successfully (admin@hospital.com / Admin@123)");
            }
        }
        else
        {
            Console.WriteLine("[DB SEED WARNING] Cannot connect to database. Please make sure to create the database in SSMS first.");
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[DB SEED EXCEPTION] Seeding did not complete. This is normal if the database has not been created in SSMS yet. Details: {ex.Message}");
    }
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();

app.UseAuthentication(); // Enable Authentication
app.UseAuthorization();  // Enable Authorization

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

app.Run();
