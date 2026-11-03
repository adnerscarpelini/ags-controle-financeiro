using Ags.Finance.Modules.Accounts.Contracts;

namespace Ags.Finance.Modules.Accounts.Application;

internal interface ILogoProcessor
{
    LogoPreviewResult CreatePreview(byte[] content, LogoCrop crop);
}
