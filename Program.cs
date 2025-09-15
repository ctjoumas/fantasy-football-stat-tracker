using Azure.AI.OpenAI;
using FantasyFootballStatTracker.Plugins;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;

var builder = WebApplication.CreateBuilder(args);

// Add configuration from appsettings.json and appsettings.Development.json
builder.Configuration.AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
                     .AddJsonFile("appsettings.Development.json", optional: true, reloadOnChange: true)
                     .AddEnvironmentVariables(); // Optional, for environment variable overrides

// Add services to the container.
builder.Services.AddControllersWithViews();
builder.Services.AddControllers(); // For API controllers

// Add CORS for React development
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowReactApp",
        builder => builder
            .WithOrigins("http://localhost:3000", "https://localhost:3001")
            .AllowAnyMethod()
            .AllowAnyHeader()
            .AllowCredentials());
});

//builder.Services.AddHttpContextAccessor();

builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(1);
});

builder.Services.AddTransient<Kernel>(s =>
{
    var config = s.GetRequiredService<IConfiguration>();

    var apiDeploymentName = config["AppConfiguration:ApiDeploymentName"];
    var openAiEndpoint = config["AppConfiguration:OpenAiEndpoint"];
    var openAiApiKey = config["AppConfiguration:OpenAiApiKey"];

    var builder = Kernel.CreateBuilder();
    builder.AddAzureOpenAIChatCompletion(
        apiDeploymentName,
        openAiEndpoint,
        openAiApiKey
    );

    builder.Plugins.AddFromType<DBQueryPlugin>();

    return builder.Build();
});

builder.Services.AddSingleton<IChatCompletionService>(sp =>
            sp.GetRequiredService<Kernel>().GetRequiredService<IChatCompletionService>());

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    //app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseSession();

app.UseCors("AllowReactApp"); // Enable CORS for React

app.UseRouting();

app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.MapControllers(); // For API routes

app.Run();