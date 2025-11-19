using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using ST10382638_PROG_POE.Data;
using ST10382638_PROG_POE.Models;
using ST10382638_PROG_POE.Service;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddIdentity<ApplicationUser, IdentityRole>()
    .AddEntityFrameworkStores<AppDbContext>()
    .AddDefaultTokenProviders();

builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Account/Login";
    options.AccessDeniedPath = "/Account/AccessDenied";
});


builder.Services.AddScoped<ClaimDownload>();
builder.Services.AddScoped<LecturerInvoiceReport>();


var app = builder.Build();

// ---------------- Seed Identity (roles + demo users) ----------------


// ---------------- Seed Identity (roles + demo users) ----------------
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;

    var context = services.GetRequiredService<AppDbContext>();
    // Make sure the database (and Identity tables) exist
    context.Database.EnsureCreated();

    var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
    var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();

    // 1) Ensure required roles exist
    string[] roleNames = { "Lecturer", "Coordinator", "Manager", "HR" };

    foreach (var roleName in roleNames)
    {
        if (!await roleManager.RoleExistsAsync(roleName))
        {
            await roleManager.CreateAsync(new IdentityRole(roleName));
        }
    }

    // 2) Seed demo users and link them to roles via AspNetUserRoles

    // NOTE:
    //   - Roles are NOT stored in AspNetUsers.
    //   - User <-> Role link is stored in AspNetUserRoles (UserRole join table).
    //   - AddToRoleAsync will create the correct AspNetUserRoles row.

    // Lecturer user
    await SeedUserAsync(
        userManager,
        email: "lecturer@demo.local",
        firstName: "Demo",
        surname: "Lecturer",
        password: "Lecturer123!",
        roleName: "Lecturer");

    // Coordinator user
    await SeedUserAsync(
        userManager,
        email: "coordinator@demo.local",
        firstName: "Demo",
        surname: "Coordinator",
        password: "Coordinator123!",
        roleName: "Coordinator");

    // Manager user
    await SeedUserAsync(
        userManager,
        email: "manager@demo.local",
        firstName: "Demo",
        surname: "Manager",
        password: "Manager123!",
        roleName: "Manager");

    // HR user
    await SeedUserAsync(
        userManager,
        email: "hr@demo.local",
        firstName: "Demo",
        surname: "HR",
        password: "Hr123!",
        roleName: "HR");

    var lecturerUser = await userManager.FindByEmailAsync("lecturer@demo.local");
    if (lecturerUser != null)
    {
        // Only create a profile if this user does not already have one.
        var hasProfile = await context.LecturerProfile
            .AnyAsync(lp => lp.UserId == lecturerUser.Id);

        if (!hasProfile)
        {
            context.LecturerProfile.Add(new LecturerProfile
            {
                // Demo values – these can be adjusted in the UI later if needed.
                HourlyRate = 500.00,   // example hourly rate for the demo lecturer
                IsAvailable = true,    // lecturer is available by default
                UserId = lecturerUser.Id
            });

            await context.SaveChangesAsync();
        }
    }
}

static async Task SeedUserAsync(
    UserManager<ApplicationUser> userManager,
    string email,
    string firstName,
    string surname,
    string password,
    string roleName)
{
    // 1) Check if user already exists
    var user = await userManager.FindByEmailAsync(email);
    if (user == null)
    {
        user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            FirstName = firstName,
            Surname = surname,
            EmailConfirmed = true
        };

        var result = await userManager.CreateAsync(user, password);
        if (!result.Succeeded)
        {
            // In a real app you would log the errors.
            // For the POE, this is enough to avoid crashing the app.
            return;
        }
    }

    // 2) Make sure user has the role (creates row in AspNetUserRoles)
    if (!await userManager.IsInRoleAsync(user, roleName))
    {
        await userManager.AddToRoleAsync(user, roleName);
    }
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Account}/{action=Login}/{id?}");

app.Run();