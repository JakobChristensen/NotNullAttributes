// © 2015 Sitecore Corporation A/S. All rights reserved.

using System;
using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace NotNullAttributes
{
    [ExportCodeFixProvider("PropertyNotNullAttributeCodeFixProvider", LanguageNames.CSharp)]
    [Shared]
    public class PropertyAttributeCodeFixProvider : CodeFixProvider
    {
        public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArray.Create(AttributeDiagnosticAnalyzer.PropertyDiagnosticId);

        public override sealed FixAllProvider GetFixAllProvider()
        {
            return WellKnownFixAllProviders.BatchFixer;
        }

        public override async Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);

            var diagnostic = context.Diagnostics.FirstOrDefault();
            if (diagnostic == null)
            {
                return;
            }

            var propertyDeclaration = root.FindToken(diagnostic.Location.SourceSpan.Start).Parent.AncestorsAndSelf().OfType<PropertyDeclarationSyntax>().FirstOrDefault();
            if (propertyDeclaration == null)
            {
                return;
            }

            var notNullTitle = "Mark '" + propertyDeclaration.Identifier.Text + "' with [NotNull]";
            context.RegisterCodeFix(CodeAction.Create(notNullTitle, cancellationToken => ApplyAttribute(context.Document, propertyDeclaration, "NotNull", cancellationToken), notNullTitle), diagnostic);

            var canBeNullTitle = "Mark '" + propertyDeclaration.Identifier.Text + "' with [CanBeNull]";
            context.RegisterCodeFix(CodeAction.Create(canBeNullTitle, cancellationToken => ApplyAttribute(context.Document, propertyDeclaration, "CanBeNull", cancellationToken), canBeNullTitle), diagnostic);
        }

        private async Task<Document> ApplyAttribute(Document document, PropertyDeclarationSyntax propertyDeclaration, string attributeName, CancellationToken cancellationToken)
        {
            var attribute = SyntaxFactory.Attribute(SyntaxFactory.IdentifierName(attributeName));
            var syntaxes = new[]
            {
                attribute
            };

            var attributeList = propertyDeclaration.AttributeLists.Add(SyntaxFactory.AttributeList().WithAttributes(SyntaxFactory.SeparatedList(syntaxes)).WithTrailingTrivia(SyntaxFactory.SyntaxTrivia(SyntaxKind.EndOfLineTrivia, Environment.NewLine)));

            var syntaxNode = (await document.GetSyntaxRootAsync(cancellationToken)).ReplaceNode(propertyDeclaration, propertyDeclaration.WithAttributeLists(attributeList));

            return document.WithSyntaxRoot(syntaxNode);
        }
    }
}
