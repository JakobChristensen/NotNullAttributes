// © 2015 Sitecore Corporation A/S. All rights reserved.

using System;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace NotNullAttributes
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class AttributeDiagnosticAnalyzer : DiagnosticAnalyzer
    {
        public const string Category = "CodeStyle";

        public const string MethodParameterDiagnosticId = "MethodParameterAttribute";

        public const string MethodReturnDiagnosticId = "MethodReturnAttribute";

        public const string PropertyDiagnosticId = "PropertyAttribute";

        public static readonly DiagnosticDescriptor MethodParameterRule = new DiagnosticDescriptor(MethodParameterDiagnosticId, "The parameter should be marked with either [NotNull] or [CanBeNull]", "'{0}' should be marked with either [NotNull] or [CanBeNull]", Category, DiagnosticSeverity.Warning, true);

        public static readonly DiagnosticDescriptor MethodReturnRule = new DiagnosticDescriptor(MethodReturnDiagnosticId, "A methods that returns a reference value should be marked with either [NotNull] or [CanBeNull]", "'{0}' should be marked with either [NotNull] or [CanBeNull]", Category, DiagnosticSeverity.Warning, true);

        public static readonly DiagnosticDescriptor PropertyRule = new DiagnosticDescriptor(PropertyDiagnosticId, "The property should be marked with either [NotNull] or [CanBeNull]", "'{0}' should be marked with either [NotNull] or [CanBeNull]", Category, DiagnosticSeverity.Warning, true);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(MethodReturnRule, MethodParameterRule, PropertyRule);

        public override void Initialize(AnalysisContext context)
        {
            context.RegisterSymbolAction(AnalyzeMethod, SymbolKind.Method);
            context.RegisterSymbolAction(AnalyzeProperty, SymbolKind.Property);
        }

        private void AnalyzeMethod(SymbolAnalysisContext context)
        {
            var method = context.Symbol as IMethodSymbol;
            if (method == null)
            {
                return;
            }

            if (IsDesignerFile(context))
            {
                return;
            }

            if (method.MethodKind == MethodKind.PropertyGet)
            {
                return;
            }

            if (method.MethodKind == MethodKind.PropertySet)
            {
                return;
            }

            AnalyzeMethodParameters(context, method);
            AnalyzeMethodReturn(context, method);
        }

        private void AnalyzeMethodParameters(SymbolAnalysisContext context, IMethodSymbol method)
        {
            var parameters = method.Parameters.Select(p => new ParameterDescriptor(p, p.RefKind == RefKind.Out || p.IsExtern || !p.Type.IsReferenceType)).ToArray();

            // search for attributes in the inheritance hierarchy
            var currentMethod = method;
            while (currentMethod != null)
            {
                for (var index = 0; index < parameters.Length; index++)
                {
                    var parameter = parameters[index];
                    if (parameter.HasAttribute)
                    {
                        continue;
                    }

                    var parm = currentMethod.Parameters.ElementAt(index);
                    if (parm == null || parm.Type != parameter.Parameter.Type)
                    {
                        continue;
                    }

                    parameter.HasAttribute = HasAttribute(parm.GetAttributes());
                }

                currentMethod = currentMethod.OverriddenMethod;
                if (IsFrameworkSymbol(currentMethod))
                {
                    break;
                }
            }

            // search for attributes in interfaces
            var namedTypeSymbol = method.ContainingType;
            if (namedTypeSymbol != null)
            {
                foreach (var intf in namedTypeSymbol.AllInterfaces.Where(i => !IsFrameworkSymbol(i)))
                {
                    // todo: find method with the right signature
                    foreach (var interfaceMethod in intf.GetMembers(method.Name).OfType<IMethodSymbol>())
                    {
                        for (var index = parameters.Length - 1; index >= 0; index--)
                        {
                            var parameter = parameters[index];
                            if (parameter.HasAttribute)
                            {
                                continue;
                            }

                            var parm = interfaceMethod.Parameters.ElementAt(index);
                            if (parm == null || parm.Type != parameter.Parameter.Type)
                            {
                                continue;
                            }

                            parameter.HasAttribute = HasAttribute(parm.GetAttributes());
                        }
                    }
                }
            }

            foreach (var parameter in parameters.Where(p => !p.HasAttribute))
            {
                var diagnostic = Diagnostic.Create(MethodParameterRule, parameter.Parameter.Locations.First(), parameter.Parameter.Name);
                context.ReportDiagnostic(diagnostic);
            }
        }

        private void AnalyzeMethodReturn(SymbolAnalysisContext context, IMethodSymbol method)
        {
            if (method.ReturnsVoid)
            {
                return;
            }

            if (!method.ReturnType.IsReferenceType)
            {
                return;
            }

            // search for attributes in the inheritance heirarchy
            var currentMethod = method;
            while (currentMethod != null)
            {
                if (HasAttribute(currentMethod.GetAttributes()))
                {
                    return;
                }

                currentMethod = currentMethod.OverriddenMethod;
                if (IsFrameworkSymbol(currentMethod))
                {
                    break;
                }
            }

            // search for attributes in interfaces
            var namedTypeSymbol = method.ContainingType;
            if (namedTypeSymbol != null)
            {
                foreach (var intf in namedTypeSymbol.AllInterfaces.Where(i => !IsFrameworkSymbol(i)))
                {
                    // todo: find method with the right signature
                    foreach (var interfaceMethod in intf.GetMembers(method.Name).OfType<IMethodSymbol>())
                    {
                        if (HasAttribute(interfaceMethod.GetAttributes()))
                        {
                            return;
                        }
                    }
                }
            }

            var diagnostic = Diagnostic.Create(MethodReturnRule, method.Locations.First(), method.Name);
            context.ReportDiagnostic(diagnostic);
        }

        private void AnalyzeProperty(SymbolAnalysisContext context)
        {
            var property = context.Symbol as IPropertySymbol;
            if (property == null)
            {
                return;
            }

            if (!property.Type.IsReferenceType)
            {
                return;
            }

            if (IsDesignerFile(context))
            {
                return;
            }

            // search for attributes in the inheritance hierarchy
            var currentProperty = property;
            while (currentProperty != null)
            {
                if (HasAttribute(currentProperty.GetAttributes()))
                {
                    return;
                }

                currentProperty = currentProperty.OverriddenProperty;
                if (IsFrameworkSymbol(currentProperty))
                {
                    break;
                }
            }

            // search for attributes in interfaces
            var namedTypeSymbol = property.ContainingType;
            if (namedTypeSymbol != null)
            {
                foreach (var intf in namedTypeSymbol.AllInterfaces.Where(i => !IsFrameworkSymbol(i)))
                {
                    // todo: find method with the right signature
                    foreach (var interfaceProperty in intf.GetMembers(property.Name).OfType<IPropertySymbol>())
                    {
                        if (HasAttribute(interfaceProperty.GetAttributes()))
                        {
                            return;
                        }
                    }
                }
            }

            var diagnostic = Diagnostic.Create(PropertyRule, property.Locations.First(), property.Name);
            context.ReportDiagnostic(diagnostic);
        }

        private bool HasAttribute(ImmutableArray<AttributeData> attributes)
        {
            return attributes.Any(a => a.AttributeClass.Name == "NotNullAttribute" || a.AttributeClass.Name == "CanBeNullAttribute");
        }

        private static bool IsDesignerFile(SymbolAnalysisContext context)
        {
            var compilation = context.Compilation;
            if (compilation != null)
            {
                foreach (var syntaxTree in compilation.SyntaxTrees)
                {
                    var filePath = syntaxTree.FilePath;
                    if (string.IsNullOrEmpty(filePath))
                    {
                        continue;
                    }

                    if (filePath.EndsWith(".Designer.cs", StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }

                    if (filePath.EndsWith(".Generated.cs", StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }
            }

            foreach (var location in context.Symbol.Locations)
            {
                var sourceTree = location.SourceTree;
                if (sourceTree == null)
                {
                    continue;
                }

                var filePath = sourceTree.FilePath;
                if (string.IsNullOrEmpty(filePath))
                {
                    continue;
                }

                if (filePath.EndsWith(".Designer.cs", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }

                if (filePath.EndsWith(".Generated.cs", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private bool IsFrameworkSymbol(ISymbol symbol)
        {
            if (symbol == null)
            {
                return true;
            }

            var assemblyName = symbol.ContainingAssembly.Name;
            if (assemblyName.StartsWith("System.", StringComparison.OrdinalIgnoreCase) || assemblyName.IndexOf("mscorlib", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }

            return false;
        }

        private class ParameterDescriptor
        {
            public ParameterDescriptor(IParameterSymbol parameter, bool hasAttribute)
            {
                Parameter = parameter;
                HasAttribute = hasAttribute;
            }

            public bool HasAttribute { get; set; }

            public IParameterSymbol Parameter { get; }
        }
    }
}
