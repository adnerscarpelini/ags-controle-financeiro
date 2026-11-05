using System.Reflection;
using Ags.Finance.Web.Components.Layout;
using Ags.Finance.Web.Hosting;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace Ags.Finance.Web.Tests;

/// <summary>A janela Sobre mostra a versão em execução, para conferir se a instalação está atualizada.</summary>
public sealed class AboutDialogTests
{
    [Fact]
    public async Task TheMenuOpensTheWindowWithTheVersionTheDeveloperAndTheRepository()
    {
        await using var context = new BunitContext().UseAppUi().UseDefaultPreferences();
        var layout = context.Render<MainLayout>(parameters => parameters.Add(x => x.Body, (RenderFragment)(builder => builder.AddContent(0, "conteúdo"))));

        await layout.Find("[data-open-about]").ClickAsync(new MouseEventArgs());

        layout.WaitForAssertion(() => Assert.Equal(AppVersion.Current, layout.Find("[data-about-version]").TextContent));
        Assert.Contains("Adner Scarpelini", layout.Find(".about-details").TextContent, StringComparison.Ordinal);
        var repository = layout.Find("[data-about-repository]");
        Assert.Equal("https://github.com/adnerscarpelini/ags-controle-financeiro", repository.GetAttribute("href"));
        Assert.Equal("_blank", repository.GetAttribute("target"));

        await layout.Find("[data-about-close]").ClickAsync(new MouseEventArgs());
        layout.WaitForAssertion(() => Assert.Empty(layout.FindAll("[data-about-version]")));
    }

    [Fact]
    public void TheVersionIsTheOneOfTheAssemblyWithoutTheBuildSuffix()
    {
        var informational = typeof(AppVersion).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()!.InformationalVersion;

        Assert.StartsWith(AppVersion.Current, informational, StringComparison.Ordinal);
        Assert.DoesNotContain('+', AppVersion.Current);
    }
}
