using Ags.Finance.SharedKernel;
using ArchUnitNET.Domain;
using ArchUnitNET.Loader;
using Assembly = System.Reflection.Assembly;

namespace Ags.Finance.ArchitectureTests;

internal static class ArchitectureModel
{
    public const string ModulesNamespace = "Ags.Finance.Modules";

    /// <summary>
    /// Dependências permitidas entre módulos. Espelha a tabela de docs/architecture/module-structure.md:
    /// uma dependência nova precisa ser registrada lá e aqui.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, string[]> AllowedDependencies = new Dictionary<string, string[]>
    {
        ["Identity"] = [],
        ["Accounts"] = [],
        ["Transactions"] = ["Accounts"],
        ["Invoices"] = ["Accounts", "Transactions"],
        ["Reimbursements"] = ["Accounts", "Transactions"],
        ["Reports"] = ["Identity", "Accounts", "Transactions", "Invoices", "Reimbursements"],
        ["Imports"] = ["Identity", "Accounts", "Transactions", "Invoices", "Reimbursements"],
    };

    private static readonly Dictionary<string, Assembly> s_moduleAssemblies = new()
    {
        ["Identity"] = typeof(Modules.Identity.DependencyInjection).Assembly,
        ["Accounts"] = typeof(Modules.Accounts.DependencyInjection).Assembly,
        ["Transactions"] = typeof(Modules.Transactions.DependencyInjection).Assembly,
        ["Invoices"] = typeof(Modules.Invoices.DependencyInjection).Assembly,
        ["Reimbursements"] = typeof(Modules.Reimbursements.DependencyInjection).Assembly,
        ["Reports"] = typeof(Modules.Reports.DependencyInjection).Assembly,
        ["Imports"] = typeof(Modules.Imports.DependencyInjection).Assembly,
    };

    public static Assembly SharedKernelAssembly { get; } = typeof(Money).Assembly;

    public static Assembly WebAssembly { get; } = typeof(Web.Components.App).Assembly;

    public static Architecture Architecture { get; } = new ArchLoader()
        .LoadAssemblies([SharedKernelAssembly, WebAssembly, .. s_moduleAssemblies.Values])
        .Build();

    public static IEnumerable<object[]> ModuleNames()
    {
        return AllowedDependencies.Keys.Select(name => new object[] { name });
    }

    public static Assembly AssemblyOf(string module)
    {
        return s_moduleAssemblies[module];
    }

    public static string NamespaceOf(string module)
    {
        return $"{ModulesNamespace}.{module}";
    }
}
