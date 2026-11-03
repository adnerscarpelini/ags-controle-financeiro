using Microsoft.AspNetCore.Components;

namespace Ags.Finance.Web.Components.Common;

/// <summary>
/// Uma ação de linha de tabela. A chave identifica a ação; o rótulo é o texto mostrado e o nome acessível.
/// É um EventCallback criado pela tela, para ela redesenhar depois da ação, e não só a lista de botões.
/// </summary>
public sealed record RowAction(string Label, string Icon, string Key, EventCallback Run, string? Tooltip = null);
