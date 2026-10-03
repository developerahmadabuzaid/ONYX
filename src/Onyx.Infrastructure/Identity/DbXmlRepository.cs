using System.Xml.Linq;
using Microsoft.AspNetCore.DataProtection.Repositories;
using Microsoft.Extensions.DependencyInjection;
using Onyx.Infrastructure.Persistence;

namespace Onyx.Infrastructure.Identity;

/// <summary>
/// Stores the Data Protection key ring in PostgreSQL so every API instance shares it and it is covered by database backups
/// (implemented in-house; no extra package). Keys protect reset/invitation tokens and 2FA secrets at rest.
/// </summary>
public sealed class DbXmlRepository(IServiceScopeFactory scopeFactory) : IXmlRepository
{
    public IReadOnlyCollection<XElement> GetAllElements()
    {
        using var scope = scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return dbContext.DataProtectionKeys
            .Select(key => key.Xml)
            .AsEnumerable()
            .Select(XElement.Parse)
            .ToList()
            .AsReadOnly();
    }

    public void StoreElement(XElement element, string friendlyName)
    {
        ArgumentNullException.ThrowIfNull(element);

        using var scope = scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        dbContext.DataProtectionKeys.Add(new DataProtectionKeyRecord
        {
            FriendlyName = friendlyName,
            Xml = element.ToString(SaveOptions.DisableFormatting),
        });
        dbContext.SaveChanges();
    }
}
