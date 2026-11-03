using System.Runtime.CompilerServices;
using Bunit;

namespace Ags.Finance.Web.Tests;

internal static class BunitDefaults
{
    /// <summary>O padrão de 1 segundo é curto para telas que usam o banco em máquina carregada, como no CI. A espera termina assim que a condição é atendida.</summary>
    [ModuleInitializer]
    internal static void Configure() => BunitContext.DefaultWaitTimeout = TimeSpan.FromSeconds(15);
}
