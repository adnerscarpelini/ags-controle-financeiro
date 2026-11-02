namespace Ags.Finance.Web.Authentication;

/// <summary>Só deixa voltar para um caminho desta aplicação depois do login, nunca para outro site.</summary>
internal static class ReturnUrl
{
    public const string Home = "/";

    public static string Sanitize(string? returnUrl)
    {
        if (string.IsNullOrEmpty(returnUrl) || returnUrl[0] != '/')
        {
            return Home;
        }

        // "//host" e "/\host" são lidos pelo navegador como endereço de outro site.
        if (returnUrl.Length > 1 && (returnUrl[1] == '/' || returnUrl[1] == '\\'))
        {
            return Home;
        }

        return returnUrl.Any(char.IsControl) ? Home : returnUrl;
    }
}
