﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using Lens.Compiler.Entities;
using Lens.SyntaxTree;
using Lens.SyntaxTree.ControlFlow;
using Lens.SyntaxTree.Declarations.Functions;
using Lens.SyntaxTree.Declarations.Types;
using Lens.Translations;
using Lens.Utils;

namespace Lens.Compiler
{
    internal partial class Context
    {
        #region Compilation essentials

        /// <summary>
        /// The main compilation method.
        /// Processes the list of nodes, performs all compilation stages.
        /// </summary>
        /// <returns>
        /// The instance of generated assembly's entry point class.
        /// </returns>
        public IScript Compile(IEnumerable<NodeBase> nodes)
        {
            LoadTree(nodes);
            CreateInitialEntities();
            TransformTree();
            EmitCode();
            FinalizeAssembly();

            var inst = Activator.CreateInstance(ResolveType(EntityNames.MainTypeName));
            return inst as IScript;
        }

        /// <summary>
        /// Initializes the context from a stream of nodes.
        /// </summary>
        private void LoadTree(IEnumerable<NodeBase> nodes)
        {
            foreach (var currNode in nodes)
            {
                if (currNode is TypeDefinitionNode)
                    DeclareType(currNode as TypeDefinitionNode);
                else if (currNode is RecordDefinitionNode)
                    DeclareRecord(currNode as RecordDefinitionNode);
                else if (currNode is FunctionNode)
                    DeclareFunction(currNode as FunctionNode);
                else if (currNode is UseNode)
                    DeclareOpenNamespace(currNode as UseNode);
                else
                    DeclareScriptNode(currNode);
            }
        }

        /// <summary>
        /// Creates all autogenerated code for user-defined types.
        /// </summary>
        private void CreateInitialEntities()
        {
            if (Options.AllowSave && Options.SaveAsExe)
                CreateEntryPoint();

            PrepareEntities(false);

            if (UnpreparedTypes.Count > 0)
            {
                foreach (var curr in UnpreparedTypes)
                    curr.CreateEntities();

                PrepareEntities();
            }
        }

        /// <summary>
        /// Traverses all nodes, resolving and transforming them.
        /// </summary>
        private void TransformTree()
        {
            while (UnprocessedMethods.Count > 0)
            {
                var methods = UnprocessedMethods.OrderByDescending(x => x.IsImported).ToArray();
                UnprocessedMethods.Clear();

                foreach (var curr in methods)
                {
                    // closure method's body has been processed as contents of lambda function
                    // no need to process it twice
                    if (curr.Kind == TypeContentsKind.Closure)
                        continue;

                    curr.TransformBody();
                    curr.ProcessClosures();
                }

                PrepareEntities();
            }
        }

        /// <summary>
        /// Generates the source code for all the declared methods.
        /// </summary>
        private void EmitCode()
        {
            foreach (var curr in _definedTypes)
                curr.Value.Compile();
        }

        /// <summary>
        /// Finalizes the assembly.
        /// </summary>
        private void FinalizeAssembly()
        {
            foreach (var curr in _definedTypes)
                if (!curr.Value.IsImported)
                    curr.Value.TypeBuilder.CreateType();

            if (Options.AllowSave)
            {
                if (Options.SaveAsExe)
                {
                    var ep = ResolveMethod(ResolveType(EntityNames.MainTypeName), EntityNames.EntryPointMethodName);
                    MainAssembly.SetEntryPoint(ep.MethodInfo, PEFileKinds.ConsoleApplication);
                }

                MainAssembly.Save(Options.FileName);
            }
        }

        #endregion

        #region Scope manipulation

        /// <summary>
        /// Add a new scope to the lookup list.
        /// </summary>
        public void EnterScope(Scope scope)
        {
            if (Scope == null)
            {
                if (scope.OuterScope == null && scope.Kind != ScopeKind.FunctionRoot)
                    throw new InvalidOperationException($"Scope of kind '{scope.Kind}' must have a parent!");
            }
            else
            {
                scope.OuterScope = Scope;
            }

            _scopeStack.Push(scope);
        }

        /// <summary>
        /// Remove the topmost scope from the lookup list.
        /// </summary>
        public Scope ExitScope()
        {
            if (Scope == null)
                throw new InvalidOperationException("No scope to exit!");

            return _scopeStack.Pop();
        }

        #endregion

        #region Compilation helpers

        /// <summary>
        /// Declares a new type.
        /// </summary>
        private void DeclareType(TypeDefinitionNode node)
        {
            if (node.Name == "_")
                Error(CompilerMessages.UnderscoreName);

            var mainType = CreateType(node.Name, prepare: false);
            mainType.Kind = TypeEntityKind.Type;

            foreach (var curr in node.Entries)
            {
                var tagName = curr.Name;
                var labelType = CreateType(tagName, node.Name, isSealed: true, defaultCtor: false, prepare: false);
                labelType.Kind = TypeEntityKind.TypeLabel;

                var ctor = labelType.CreateConstructor(prepare: false);
                ctor.Kind = TypeContentsKind.AutoGenerated;
                if (curr.IsTagged)
                {
                    var tagField = labelType.CreateField("Tag", curr.TagType, prepare: false);
                    tagField.Kind = TypeContentsKind.UserDefined;

                    var args = new HashList<FunctionArgument> {{"value", new FunctionArgument("value", curr.TagType)}};

                    var staticCtor = MainType.CreateMethod(tagName, tagName, new string[0], isStatic: true, prepare: false);
                    staticCtor.Kind = TypeContentsKind.AutoGenerated;

                    ctor.Arguments = staticCtor.Arguments = args;
                    ctor.Body.Add(
                        Expr.SetMember(Expr.This(), "Tag", Expr.Get("value"))
                    );

                    staticCtor.Body.Add(
                        Expr.New(tagName, Expr.Get("value"))
                    );
                }
            }
        }

        /// <summary>
        /// Declares a new record.
        /// </summary>
        private void DeclareRecord(RecordDefinitionNode node)
        {
            if (node.Name == "_")
                Error(CompilerMessages.UnderscoreName);

            var recType = CreateType(node.Name, isSealed: true, prepare: false);
            recType.Kind = TypeEntityKind.Record;

            var recCtor = recType.CreateConstructor(prepare: false);
            recCtor.Kind = TypeContentsKind.AutoGenerated;

            foreach (var curr in node.Entries)
            {
                var field = recType.CreateField(curr.Name, curr.Type, prepare: false);
                field.Kind = TypeContentsKind.UserDefined;
                var argName = "_" + field.Name.ToLowerInvariant();

                recCtor.Arguments.Add(argName, new FunctionArgument(argName, curr.Type));
                recCtor.Body.Add(
                    Expr.SetMember(Expr.This(), field.Name, Expr.Get(argName))
                );
            }
        }

        /// <summary>
        /// Declares a new function.
        /// </summary>
        private void DeclareFunction(FunctionNode node)
        {
            if (node.Name == "_")
                Error(CompilerMessages.UnderscoreName);

            var isVariadic = false;

            // validation
            if (node.Arguments.Count > 0)
            {
                for (var idx = 0; idx < node.Arguments.Count; idx++)
                {
                    var curr = node.Arguments[idx];
                    if (curr.Name == "_")
                        curr.Name = Unique.AnonymousArgName();

                    if (curr.Type == typeof(UnspecifiedType))
                        Error(CompilerMessages.LambdaArgTypeUnknown);

                    if (curr.IsVariadic)
                    {
                        if (idx < node.Arguments.Count - 1)
                            Error(CompilerMessages.VariadicArgumentNotLast);

                        isVariadic = true;
                    }
                }
            }
            else
            {
                if (node.Name == EntityNames.RunMethodName || node.Name == EntityNames.EntryPointMethodName)
                    Error(CompilerMessages.ReservedFunctionRedefinition, node.Name);
            }

            var method = MainType.CreateMethod(node.Name, node.ReturnTypeSignature, node.Arguments, true, prepare: false);
            method.Kind = TypeContentsKind.UserDefined;
            method.IsPure = node.IsPure;
            method.IsVariadic = isVariadic;
            method.Body = node.Body;
        }

        /// <summary>
        /// Opens a new namespace for current script.
        /// </summary>
        private void DeclareOpenNamespace(UseNode node)
        {
            if (!Namespaces.ContainsKey(node.Namespace))
                Namespaces.Add(node.Namespace, true);
        }

        /// <summary>
        /// Adds a new node to the main script's body.
        /// </summary>
        private void DeclareScriptNode(NodeBase node)
        {
            MainMethod.Body.Add(node);
        }

        /// <summary>
        /// Creates all assembly-level entities for types, fields, methods and constructors.
        /// </summary>
        private void PrepareEntities(bool clearTypes = true)
        {
            if (UnpreparedTypes.Count > 0)
            {
                foreach (var curr in UnpreparedTypes)
                    curr.PrepareSelf();

                if (clearTypes)
                    UnpreparedTypes.Clear();
            }

            if (UnpreparedTypeContents.Count > 0)
            {
                foreach (var curr in UnpreparedTypeContents)
                {
                    curr.PrepareSelf();

                    var me = curr as MethodEntity;
                    if (me != null)
                        me.ContainerType.CheckMethod(me);
                }

                UnpreparedTypeContents.Clear();
            }
        }

        /// <summary>
        /// Creates the entry point for an assembly if it is supposed to be saved.
        /// The entry point method basically calls the Run method and discards the result.
        /// </summary>
        private void CreateEntryPoint()
        {
            var ep = MainType.CreateMethod(EntityNames.EntryPointMethodName, "Void", args: null, isStatic: true);
            ep.Kind = TypeContentsKind.AutoGenerated;
            ep.Body = Expr.Block(
                Expr.Invoke(Expr.New(EntityNames.MainTypeName), "Run"),
                Expr.Unit()
            );
        }

        #endregion
    }
}