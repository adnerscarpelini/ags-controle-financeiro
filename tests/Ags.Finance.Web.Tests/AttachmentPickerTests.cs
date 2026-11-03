using Ags.Finance.Modules.Attachments.Contracts;
using Ags.Finance.Web.Components;
using Bunit;
using Microsoft.AspNetCore.Components.Forms;

namespace Ags.Finance.Web.Tests;

public sealed class AttachmentPickerTests
{
    [Fact]
    public async Task RetryOnlySendsFailedFilesAndDoesNotRepeatSuccessfulUploads()
    {
        await using var context = new BunitContext();
        var picker = context.Render<AttachmentPicker>();
        picker.FindComponent<InputFile>().UploadFiles(InputFileContent.CreateFromText("fictional first", "first.pdf"), InputFileContent.CreateFromText("fictional second", "second.pdf"));
        var calls = new List<string>();
        var failSecond = true;
        Task<AttachmentResult> Upload(AttachmentKind kind, string name, Stream stream)
        {
            calls.Add(name);
            return Task.FromResult(new AttachmentResult(null, name == "second.pdf" && failSecond ? "Falha fictícia." : null));
        }
        await picker.InvokeAsync(async () => Assert.False(await picker.Instance.SendAsync(Upload)));
        Assert.True(picker.Instance.HasFiles);
        failSecond = false;
        await picker.InvokeAsync(async () => Assert.True(await picker.Instance.SendAsync(Upload)));
        Assert.False(picker.Instance.HasFiles);
        Assert.Equal(["first.pdf", "second.pdf", "second.pdf"], calls);
    }
}
