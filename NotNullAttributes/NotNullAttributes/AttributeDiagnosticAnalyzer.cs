// © 2015 Sitecore Corporation A/S. All rights reserved.

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
            var parameters = method.Parameters.Where(p => p.RefKind != RefKind.Out && !p.IsExtern && p.Type.IsReferenceType).ToList();

            // search for attributes in the inheritance heirarchy
            var currentMethod = method;
            while (currentMethod != null)
            {
                for (var index = parameters.Count - 1; index >= 0; index--)
                {
                    var parameter = parameters[index];

                    var p = currentMethod.Parameters.FirstOrDefault(n => n.Name == parameter.Name);
                    if (p == null)
                    {
                        continue;
                    }

                    if (HasAttribute(p.GetAttributes()))
                    {
                        parameters.Remove(parameter);
                    }
                }

                currentMethod = currentMethod.OverriddenMethod;
            }

            // search for attributes in interfaces
            var namedTypeSymbol = method.ContainingType;
            if (namedTypeSymbol != null)
            {
                foreach (var intf in namedTypeSymbol.AllInterfaces)
                {
                    // todo: find method with the right signature
                    foreach (var interfaceMethod in intf.GetMembers(method.Name).OfType<IMethodSymbol>())
                    {
                        for (var index = parameters.Count - 1; index >= 0; index--)
                        {
                            var parameter = parameters[index];

                            var p = interfaceMethod.Parameters.FirstOrDefault(n => n.Name == parameter.Name);
                            if (p == null)
                            {
                                continue;
                            }

                            if (HasAttribute(p.GetAttributes()))
                            {
                                parameters.Remove(parameter);
                            }
                        }
                    }
                }
            }

            foreach (var parameter in parameters)
            {
                var diagnostic = Diagnostic.Create(MethodParameterRule, parameter.Locations.First(), parameter.Name);
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
            }

            // search for attributes in interfaces
            var namedTypeSymbol = method.ContainingType;
            if (namedTypeSymbol != null)
            {
                foreach (var intf in namedTypeSymbol.AllInterfaces)
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

            // search for attributes in the inheritance heirarchy
            var currentProperty = property;
            while (currentProperty != null)
            {
                if (HasAttribute(currentProperty.GetAttributes()))
                {
                    return;
                }

                currentProperty = currentProperty.OverriddenProperty;
            }

            // search for attributes in interfaces
            var namedTypeSymbol = property.ContainingType;
            if (namedTypeSymbol != null)
            {
                foreach (var intf in namedTypeSymbol.AllInterfaces)
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
    }
}
