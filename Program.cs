using BankLiaison.Data;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

var b = WebApplication.CreateBuilder(args);
b.WebHost.UseUrls($"http://0.0.0.0:{Environment.GetEnvironmentVariable("PORT") ?? "8080"}");

b.Services.AddControllersWithViews(o =>
{
    o.Filters.Add(new AutoValidateAntiforgeryTokenAttribute());
    o.MaxModelBindingCollectionSize = 3000;
});
b.Services.Configure<FormOptions>(o => o.ValueCountLimit = 20000);
b.Services.AddDbContext<AppDb>(o => o.UseNpgsql(ConnectionString()));
b.Services.AddAuthentication("Cookies").AddCookie("Cookies", o =>
{
    o.LoginPath = "/Account/Login";
    o.AccessDeniedPath = "/Account/Login";
    o.ExpireTimeSpan = TimeSpan.FromHours(10);
    o.SlidingExpiration = true;
});
b.Services.Configure<ForwardedHeadersOptions>(o =>
{
    o.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    o.KnownNetworks.Clear(); o.KnownProxies.Clear();
});

var app = b.Build();
app.UseForwardedHeaders();
using (var s = app.Services.CreateScope()) Seeder.Run(s.ServiceProvider.GetRequiredService<AppDb>());
app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllerRoute("default", "{controller=Home}/{action=Index}/{id?}");
app.Run();

static string ConnectionString()
{
    var url = Environment.GetEnvironmentVariable("DATABASE_URL");
    if (string.IsNullOrWhiteSpace(url)) return "Host=localhost;Database=liaison;Username=postgres;Password=postgres";
    var u = new Uri(url);
    var ui = u.UserInfo.Split(':', 2);
    return $"Host={u.Host};Port={(u.Port > 0 ? u.Port : 5432)};Database={u.AbsolutePath.TrimStart('/')};Username={Uri.UnescapeDataString(ui[0])};Password={Uri.UnescapeDataString(ui[1])};SSL Mode=Prefer;Trust Server Certificate=true";
}
