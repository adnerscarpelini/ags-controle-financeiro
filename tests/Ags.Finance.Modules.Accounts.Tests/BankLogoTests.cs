using System.Text;
using Ags.Finance.Modules.Accounts.Contracts;
using Ags.Finance.SharedKernel;
using Ags.Finance.Tests.Support;
using Microsoft.Extensions.DependencyInjection;
using SkiaSharp;

namespace Ags.Finance.Modules.Accounts.Tests;

public sealed class BankLogoTests : IAsyncLifetime
{
    private PostgresTestDatabase? _database;
    private ServiceProvider? _provider;
    private ServiceProvider Provider => _provider ?? throw new InvalidOperationException();
    private IBankLogoCatalog Logos => Provider.GetRequiredService<IBankLogoCatalog>();
    private IAccountCatalog Accounts => Provider.GetRequiredService<IAccountCatalog>();
    private Guid _bank;
    public async Task InitializeAsync()
    {
        _database = await PostgresTestDatabase.CreateAsync();
        _provider = new ServiceCollection().AddAccountsModule(_database.ConnectionString).BuildServiceProvider();
        await Provider.MigrateAccountsModuleAsync();
        _bank = (await Accounts.CreateBankAsync("Banco Exemplo")).ExternalId!.Value;
    }
    public async Task DisposeAsync()
    {
        if (_provider is not null) { await _provider.DisposeAsync(); }
        if (_database is not null) { await _database.DisposeAsync(); }
    }
    private static byte[] ImageContent(SKEncodedImageFormat format = SKEncodedImageFormat.Png, int width = 400, int height = 200)
    {
        using var bitmap = new SKBitmap(width, height);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.Red);
        using var paint = new SKPaint { Color = SKColors.Blue };
        canvas.DrawRect(new SKRect(width / 2f, 0, width, height), paint);
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(format, 100);
        return data.ToArray();
    }

    [Theory]
    [InlineData(SKEncodedImageFormat.Png)]
    [InlineData(SKEncodedImageFormat.Jpeg)]
    [InlineData(SKEncodedImageFormat.Webp)]
    public async Task FormatsProducePng256AndCircularTransparencyMatchingPreview(SKEncodedImageFormat format)
    {
        var source = ImageContent(format);
        var crop = new LogoCrop(1.5, 0.4, -0.2);
        var preview = Logos.CreatePreview(source, crop);
        Assert.True(preview.Succeeded, preview.Error);
        Assert.True((await Logos.SaveAsync(_bank, source, crop)).Succeeded);
        var saved = Assert.IsType<BankLogoDetails>(await Logos.GetAsync(_bank));
        Assert.Equal(preview.Png, saved.Png);
        using var decoded = SKBitmap.Decode(saved.Png);
        Assert.Equal(256, decoded.Width);
        Assert.Equal(256, decoded.Height);
        Assert.Equal(0, decoded.GetPixel(0, 0).Alpha);
        Assert.Equal(0, decoded.GetPixel(255, 255).Alpha);
        Assert.Equal(255, decoded.GetPixel(128, 128).Alpha);
        Assert.Equal(saved.Version, Assert.Single(await Accounts.GetBanksAsync()).LogoVersion);
    }

    [Fact]
    public void PositionSelectsOppositeSidesWithoutStretching()
    {
        var source = ImageContent();
        using var left = SKBitmap.Decode(Logos.CreatePreview(source, new(1, -1, 0)).Png);
        using var right = SKBitmap.Decode(Logos.CreatePreview(source, new(1, 1, 0)).Png);
        Assert.Equal(SKColors.Red, left.GetPixel(128, 128));
        Assert.Equal(SKColors.Blue, right.GetPixel(128, 128));
    }

    [Fact]
    public async Task InvalidUploadsAndCropNeverReplaceTheCurrentLogo()
    {
        var source = ImageContent();
        await Logos.SaveAsync(_bank, source, new());
        var before = await Logos.GetAsync(_bank);
        foreach (var invalid in new[] { Array.Empty<byte>(), Encoding.UTF8.GetBytes("<svg>arquivo inválido</svg>"), source[..^1], new byte[IBankLogoCatalog.MaxSourceBytes + 1], ImageContent(width: 4097, height: 1) })
        {
            Assert.False((await Logos.SaveAsync(_bank, invalid, new())).Succeeded);
        }
        Assert.False((await Logos.SaveAsync(_bank, source, new(double.NaN))).Succeeded);
        Assert.False((await Logos.SaveAsync(_bank, source, new(6))).Succeeded);
        Assert.False((await Logos.SaveAsync(_bank, source, new(1, 2))).Succeeded);
        var after = await Logos.GetAsync(_bank);
        Assert.Equal(before?.Version, after?.Version);
        Assert.Equal(before?.Png, after?.Png);
    }

    [Fact]
    public async Task ReplaceRemoveAndReloadPreserveBankAccountAndCardIdentity()
    {
        var account = await Accounts.SaveAccountAsync(new(null, _bank, "Conta Exemplo", AccountKind.Checking, "green"));
        await Accounts.SaveCreditCardAsync(new(null, account.ExternalId!.Value, "Cartão Exemplo", CardBrand.Visa, new Money(1000), 3, 10, true, "green"));
        var card = Assert.Single(await Accounts.GetCreditCardsAsync());
        await Logos.SaveAsync(_bank, ImageContent(), new());
        var first = await Logos.GetAsync(_bank);
        await Logos.SaveAsync(_bank, ImageContent(), new(2, 1));
        Assert.NotEqual(first?.Version, (await Logos.GetAsync(_bank))?.Version);
        Assert.Equal(card, Assert.Single(await Accounts.GetCreditCardsAsync()));
        Assert.True((await Logos.RemoveAsync(_bank)).Succeeded);
        Assert.Null(await Logos.GetAsync(_bank));
        Assert.Null(Assert.Single(await Accounts.GetBanksAsync()).LogoVersion);
        Assert.Equal(card, Assert.Single(await Accounts.GetCreditCardsAsync()));
        Assert.False((await Logos.SaveAsync(Guid.NewGuid(), ImageContent(), new())).Succeeded);
        Assert.False((await Logos.RemoveAsync(Guid.NewGuid())).Succeeded);
    }
}
