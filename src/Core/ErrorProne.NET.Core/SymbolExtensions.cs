﻿using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ErrorProne.NET.Core
{
    /// <nodoc />
    public static class SymbolExtensions
    {
        public static IEnumerable<ISymbol> GetAllUsedSymbols(Compilation compilation, SyntaxNode root)
        {
            var noDuplicates = new HashSet<ISymbol>();

            var model = compilation.GetSemanticModel(root.SyntaxTree);

            foreach (var node in root.DescendantNodesAndSelf())
            {
                switch (node.Kind())
                {
                    case SyntaxKind.ExpressionStatement:
                    case SyntaxKind.InvocationExpression:
                        break;
                    default:
                        ISymbol symbol = model.GetSymbolInfo(node).Symbol;

                        if (symbol != null)
                        {
                            if (noDuplicates.Add(symbol))
                                yield return symbol;
                        }
                        break;
                }
            }
        }

        /// <summary>
        /// Returns true if a given <paramref name="method"/> has iterator block inside of it.
        /// </summary>
        public static bool IsIteratorBlock(this IMethodSymbol method)
        {
            Debug.Assert(method.DeclaringSyntaxReferences.Length != 0);

            return method.DeclaringSyntaxReferences
                .Select(sr => sr.GetSyntax())
                .OfType<MethodDeclarationSyntax>()
                .Any(md => md.IsIteratorBlock()) == true;
        }

        /// <summary>
        /// Returns true if the given <paramref name="method"/> is async or return task-like type.
        /// </summary>
        public static bool IsAsyncOrTaskBased(this IMethodSymbol method, Compilation compilation)
        {
            // Currently method detects only Task<T> or ValueTask<T>
            if (method.IsAsync)
            {
                return true;
            }

            return method.ReturnType.IsTaskLike(compilation);
        }

        /// <summary>
        /// Returns true if a given <paramref name="method"/> is an implementation of an interface member.
        /// </summary>
        public static bool IsInterfaceImplementation(this IMethodSymbol method)
            => IsInterfaceImplementation(method, out _);
        
        /// <summary>
        /// Returns true if a given <paramref name="method"/> is an implementation of an interface member.
        /// </summary>
        public static bool IsInterfaceImplementation(this IMethodSymbol method, out ISymbol implementedMethod)
        {
            if (method.MethodKind == MethodKind.ExplicitInterfaceImplementation)
            {
                implementedMethod = method;
                return true;
            }

            implementedMethod = null;
            if (method.DeclaredAccessibility != Accessibility.Public)
            {
                return false;
            }

            var containingType = method.ContainingType;
            var implementedInterfaces = containingType.AllInterfaces;

            foreach (var implementedInterface in implementedInterfaces)
            {
                var implementedInterfaceMembersWithSameName = implementedInterface.GetMembers(method.Name);
                foreach (var implementedInterfaceMember in implementedInterfaceMembersWithSameName)
                {
                    if (method.Equals(containingType.FindImplementationForInterfaceMember(implementedInterfaceMember)))
                    {
                        implementedMethod = implementedInterfaceMember;
                        return true;
                    }
                }
            }

            return false;
        }

        public static VariableDeclarationSyntax TryGetDeclarationSyntax(this IFieldSymbol symbol)
        {
            if (symbol.DeclaringSyntaxReferences.Length == 0)
            {
                return null;
            }

            var syntaxReference = symbol.DeclaringSyntaxReferences[0];
            return syntaxReference.GetSyntax().FirstAncestorOrSelf<VariableDeclarationSyntax>();
        }

        public static PropertyDeclarationSyntax TryGetDeclarationSyntax(this IPropertySymbol symbol)
        {
            if (symbol.DeclaringSyntaxReferences.Length == 0)
            {
                return null;
            }

            var syntaxReference = symbol.DeclaringSyntaxReferences[0];
            return syntaxReference.GetSyntax().FirstAncestorOrSelf<PropertyDeclarationSyntax>();
        }

        public static MethodDeclarationSyntax TryGetDeclarationSyntax(this IMethodSymbol symbol)
        {
            if (symbol.DeclaringSyntaxReferences.Length == 0)
            {
                return null;
            }

            var syntaxReference = symbol.DeclaringSyntaxReferences[0];
            return syntaxReference.GetSyntax().FirstAncestorOrSelf<MethodDeclarationSyntax>();
        }

        public static bool ExceptionFromCatchBlock(this ISymbol symbol)
        {
            return
                (symbol.DeclaringSyntaxReferences.FirstOrDefault()?.GetSyntax()) is CatchDeclarationSyntax;

            // There is additional interface, called ILocalSymbolInternal
            // that has IsCatch property, but, unfortunately, that interface is internal.
            // Use following code if the trick with DeclaredSyntaxReferences would not work properly!
            // return (bool?)(symbol.GetType().GetRuntimeProperty("IsCatch")?.GetValue(symbol)) == true;
        }
    }
}