﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using Microsoft.CodeAnalysis.Testing;

namespace Microsoft.CodeAnalysis.CSharp.Testing
{
    public class CSharpSourceGeneratorLangVersionTest<TSourceGenerator, TVerifier> : SourceGeneratorTest<TVerifier>
        where TSourceGenerator : new()
        where TVerifier : IVerifier, new()
    {
        private static readonly LanguageVersion DefaultLanguageVersion =
            Enum.TryParse("Default", out LanguageVersion version) ? version : CSharp.LanguageVersion.CSharp7_3;

        protected override IEnumerable<Type> GetSourceGenerators()
            => new Type[] { typeof(TSourceGenerator) };

        protected override string DefaultFileExt => "cs";

        public override string Language => LanguageNames.CSharp;

        public LanguageVersion? LanguageVersion { get; set; }

        protected override CompilationOptions CreateCompilationOptions()
            => new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, allowUnsafe: true);

        protected override ParseOptions CreateParseOptions()
            => new CSharpParseOptions(LanguageVersion ?? DefaultLanguageVersion, DocumentationMode.Diagnose);
    }
}
