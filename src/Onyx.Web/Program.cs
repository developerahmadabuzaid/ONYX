using Onyx.Web.Configuration;

var builder = WebApplication.CreateBuilder(args);

builder.WebHost.ConfigureKestrel(kestrel => kestrel.AddServerHeader = false);
builder.Services.AddOnyxWeb(builder.Configuration, builder.Environment);

var app = builder.Build();
app.UseOnyxWeb();
app.Run();
