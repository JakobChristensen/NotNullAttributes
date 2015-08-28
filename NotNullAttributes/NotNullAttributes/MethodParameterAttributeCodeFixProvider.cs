// © 2015 Sitecore Corporation A/S. All rights reserved.

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
    [ExportCodeFixProvider("MethodParameterAttributeCodeFixProvider", LanguageNames.CSharp)]
    [Shared]
    public class MethodParameterAttributeCodeFixProvider : CodeFixProvider
    {
        public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArray.Create(AttributeDiagnosticAnalyzer.MethodParameterDiagnosticId);

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

            var parameterSyntax = root.FindToken(diagnostic.Location.SourceSpan.Start).Parent.AncestorsAndSelf().OfType<ParameterSyntax>().FirstOrDefault();
            if (parameterSyntax == null)
            {
                return;
            }

            var notNullTitle = "Mark '" + parameterSyntax.Identifier.Text + "' with [NotNull]";
            context.RegisterCodeFix(CodeAction.Create(notNullTitle, cancellationToken => ApplyAttribute(context.Document, parameterSyntax, "NotNull", cancellationToken), notNullTitle), diagnostic);

            var canBeNullTitle = "Mark '" + parameterSyntax.Identifier.Text + "' with [CanBeNull]";
            context.RegisterCodeFix(CodeAction.Create(canBeNullTitle, cancellationToken => ApplyAttribute(context.Document, parameterSyntax, "CanBeNull", cancellationToken), canBeNullTitle), diagnostic);
        }

        private async Task<Document> ApplyAttribute(Document document, ParameterSyntax parameterSyntax, string attributeName, CancellationToken cancellationToken)
        {
            var attribute = SyntaxFactory.Attribute(SyntaxFactory.IdentifierName(attributeName));
            var syntaxes = new[]
            {
                attribute
            };

            var attributeList = parameterSyntax.AttributeLists.Add(SyntaxFactory.AttributeList().WithAttributes(SyntaxFactory.SeparatedList(syntaxes)).WithTrailingTrivia(SyntaxFactory.SyntaxTrivia(SyntaxKind.WhitespaceTrivia, " ")));

            var syntaxNode = (await document.GetSyntaxRootAsync(cancellationToken)).ReplaceNode(parameterSyntax, parameterSyntax.WithAttributeLists(attributeList));

            return document.WithSyntaxRoot(syntaxNode);
        }
    }
}
