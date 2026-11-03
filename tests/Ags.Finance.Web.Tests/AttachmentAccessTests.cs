using System.Net;
using Ags.Finance.Web.Attachments;
using Microsoft.Extensions.Configuration;

namespace Ags.Finance.Web.Tests;

public sealed class AttachmentAccessTests
{
    [Fact]
    public async Task AnonymousDocumentAccessRequiresLogin()
    {
        using var factory = new AppFactory();
        using var browser = factory.CreateBrowser();
        using var response = await browser.GetAsync($"/attachments/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
    }

    [Theory]
    [InlineData("data", null)]
    [InlineData("data/documents", null)]
    [InlineData("documents", "documents/backups")]
    [InlineData("backups/documents", "backups")]
    public void OverlappingDirectoriesDisableAttachments(string documents, string? backups)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        { ["Attachments:Directory"] = documents, ["Backup:Directory"] = backups }).Build();
        Assert.NotNull(AttachmentSetup.ReadOptions(configuration, "data").Error);
    }
}
