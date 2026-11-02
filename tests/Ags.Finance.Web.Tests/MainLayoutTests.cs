using Ags.Finance.Web.Components.Layout;
using Bunit;
using Microsoft.AspNetCore.Components;

namespace Ags.Finance.Web.Tests;

/// <summary>No celular o menu é uma gaveta: abre pelo botão e fecha ao navegar ou tocar na película.</summary>
public sealed class MainLayoutTests : IDisposable
{
    private readonly BunitContext _context = new();

    [Fact]
    public void DrawerStartsClosed()
    {
        var layout = RenderLayout();

        Assert.DoesNotContain("drawer-open", layout.Find(".app-shell").ClassList);
        Assert.Equal("false", layout.Find("button[aria-controls='sidebar']").GetAttribute("aria-expanded"));
    }

    [Fact]
    public void MenuButtonOpensTheDrawer()
    {
        var layout = RenderLayout();

        layout.Find("button[aria-controls='sidebar']").Click();

        Assert.Contains("drawer-open", layout.Find(".app-shell").ClassList);
        Assert.Equal("true", layout.Find("button[aria-controls='sidebar']").GetAttribute("aria-expanded"));
    }

    [Fact]
    public void MenuButtonClosesAnOpenDrawer()
    {
        var layout = RenderLayout();
        layout.Find("button[aria-controls='sidebar']").Click();

        layout.Find("button[aria-controls='sidebar']").Click();

        Assert.DoesNotContain("drawer-open", layout.Find(".app-shell").ClassList);
    }

    [Fact]
    public void TappingTheScrimClosesTheDrawer()
    {
        var layout = RenderLayout();
        layout.Find("button[aria-controls='sidebar']").Click();

        layout.Find(".scrim").Click();

        Assert.DoesNotContain("drawer-open", layout.Find(".app-shell").ClassList);
    }

    [Fact]
    public void ChoosingAMenuItemClosesTheDrawer()
    {
        var layout = RenderLayout();
        layout.Find("button[aria-controls='sidebar']").Click();

        layout.Find("a[href='faturas']").Click();

        Assert.DoesNotContain("drawer-open", layout.Find(".app-shell").ClassList);
    }

    [Fact]
    public void MenuHasTheSevenSectionsInOrder()
    {
        var layout = RenderLayout();

        var labels = layout.FindAll(".nav-link span").Select(span => span.TextContent).ToList();

        Assert.Equal(
            ["Lançamentos", "Faturas", "Cashback", "Reembolsos", "Dashboard", "Cadastros", "Configurações"],
            labels);
    }

    public void Dispose()
    {
        _context.Dispose();
    }

    private IRenderedComponent<MainLayout> RenderLayout()
    {
        return _context.Render<MainLayout>(parameters => parameters
            .Add(layout => layout.Body, (RenderFragment)(builder => builder.AddContent(0, "conteúdo"))));
    }
}
