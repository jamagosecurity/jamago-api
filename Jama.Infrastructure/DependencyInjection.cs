using Jama.Application.Auth;
using Jama.Application.Common.Interfaces;
using Jama.Application.Options;
using Jama.Infrastructure.Data;
using Jama.Infrastructure.Documents;
using Jama.Infrastructure.Notifications;
using Jama.Infrastructure.Security;
using Jama.Infrastructure.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using QuestPDF.Infrastructure;

namespace Jama.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration config)
    {
        // QuestPDF Community license: free for organisations with < $1M USD annual gross revenue. See https://www.questpdf.com/license/.
        QuestPDF.Settings.License = LicenseType.Community;

        services.Configure<JwtSettings>(config.GetSection(JwtSettings.SectionName));
        services.Configure<AdminSeedSettings>(config.GetSection(AdminSeedSettings.SectionName));
        services.Configure<FileStorageSettings>(config.GetSection(FileStorageSettings.SectionName));
        services.Configure<TwilioSettings>(config.GetSection(TwilioSettings.SectionName));
        services.Configure<MailSettings>(config.GetSection(MailSettings.SectionName));

        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseNpgsql(config.GetConnectionString("DefaultConnection")));
        services.AddScoped<IApplicationDbContext>(sp => sp.GetRequiredService<ApplicationDbContext>());
        services.AddScoped<IDiaInspectionRepository, DiaInspectionRepository>();
        services.AddScoped<ITechnicianInspectionRepository, TechnicianInspectionRepository>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<ApplicationDbContextInitialiser>();

        services.AddSingleton<IPasswordHasher, PasswordHasherService>();
        services.AddSingleton<ITokenGenerator, JwtTokenGenerator>();
        services.AddSingleton<IInvoicePdfGenerator, InvoicePdfGenerator>();
        services.AddSingleton<IQuotationPdfGenerator, QuotationPdfGenerator>();
        services.AddSingleton<IBoqPdfGenerator, BoqPdfGenerator>();
        services.AddSingleton<IContractPdfGenerator, ContractPdfGenerator>();
        services.AddSingleton<IContractTextTemplate, ContractTextTemplate>();
        services.AddSingleton<IMoiStoragePdfGenerator, MoiStoragePdfGenerator>();
        services.AddSingleton<IPdfWatermarker, PdfWatermarker>();
        services.AddSingleton<IFileStorage, LocalFileStorage>();
        services.AddSingleton<IWhatsAppSender, TwilioWhatsAppSender>();
        // Live aircraft over Qatar for the sign-in map; one shared cache for every visitor.
        services.AddSingleton<Jama.Application.Flights.IFlightFeed>(sp => new Jama.Infrastructure.Flights.OpenSkyFlightFeed(
            new HttpClient { Timeout = TimeSpan.FromSeconds(10) }, sp.GetRequiredService<TimeProvider>()));
        // Opens a short Google Maps share link to find the pin; never follows a redirect on its own (see MapLinkResolver).
        services.AddSingleton<IMapLinkResolver>(_ => new Jama.Infrastructure.Geo.MapLinkResolver(
            new HttpClient(new SocketsHttpHandler { AllowAutoRedirect = false, ConnectTimeout = TimeSpan.FromSeconds(4) })
            { Timeout = TimeSpan.FromSeconds(8) }));
        services.AddHostedService<Jama.Infrastructure.Mail.MailSyncService>();
        services.AddSingleton<IMailSender, Jama.Infrastructure.Mail.MailSender>();
        services.AddSingleton<Jama.Infrastructure.Background.BackgroundWorkQueue>();
        services.AddSingleton<IBackgroundWork>(sp => sp.GetRequiredService<Jama.Infrastructure.Background.BackgroundWorkQueue>());
        services.AddHostedService(sp => sp.GetRequiredService<Jama.Infrastructure.Background.BackgroundWorkQueue>());
        services.AddScoped<IJobBillPdfGenerator, Jama.Infrastructure.Documents.NonMoiJobBillPdfGenerator>();

        return services;
    }
}
