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
    [ExportCodeFixProvider("MethodReturnAttributeCodeFixProvider", LanguageNames.CSharp)]
    [Shared]
    public class MethodReturnAttributeCodeFixProvider : CodeFixProvider
    {
        public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArray.Create(AttributeDiagnosticAnalyzer.MethodReturnDiagnosticId);

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

            var methodDeclaration = root.FindToken(diagnostic.Location.SourceSpan.Start).Parent.AncestorsAndSelf().OfType<MethodDeclarationSyntax>().FirstOrDefault();
            if (methodDeclaration == null)
            {
                return;
            }

            var notNullTitle = "Mark '" + methodDeclaration.Identifier.Text + "' with [NotNull]";
            context.RegisterCodeFix(CodeAction.Create(notNullTitle, cancellationToken => ApplyAttribute(context.Document, methodDeclaration, "NotNull", cancellationToken), notNullTitle), diagnostic);

            var canBeNullTitle = "Mark '" + methodDeclaration.Identifier.Text + "' with [CanBeNull]";
            context.RegisterCodeFix(CodeAction.Create(canBeNullTitle, cancellationToken => ApplyAttribute(context.Document, methodDeclaration, "CanBeNull", cancellationToken), canBeNullTitle), diagnostic);
        }

        private async Task<Document> ApplyAttribute(Document document, MethodDeclarationSyntax methodDeclaration, string attributeName, CancellationToken cancellationToken)
        {
            var attribute = SyntaxFactory.Attribute(SyntaxFactory.IdentifierName(attributeName));
            var syntaxes = new[]
            {
                attribute
            };

            var attributeList = methodDeclaration.AttributeLists.Add(SyntaxFactory.AttributeList().WithAttributes(SyntaxFactory.SeparatedList(syntaxes)).WithTrailingTrivia(SyntaxFactory.SyntaxTrivia(SyntaxKind.EndOfLineTrivia, Environment.NewLine)));

            var syntaxNode = (await document.GetSyntaxRootAsync(cancellationToken)).ReplaceNode(methodDeclaration, methodDeclaration.WithAttributeLists(attributeList));

            return document.WithSyntaxRoot(syntaxNode);
        }
    }
}
