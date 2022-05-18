﻿using MonoMod.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using static MonoMod.Core.Interop.CoreCLR;

namespace MonoMod.Core.Platforms.Runtimes {
    internal class Core60Runtime : Core50Runtime {
        // src/coreclr/inc/jiteeversionguid.h line 46
        // 5ed35c58-857b-48dd-a818-7c0136dc9f73
        private static readonly Guid JitVersionGuid = new Guid(
            0x5ed35c58,
            0x857b,
            0x48dd,
            0xa8, 0x18, 0x7c, 0x01, 0x36, 0xdc, 0x9f, 0x73
        );

        protected override Guid ExpectedJitVersion => JitVersionGuid;

        protected override InvokeCompileMethodPtr InvokeCompileMethodPtr => V60.InvokeCompileMethodPtr;

        protected override Delegate CastCompileHookToRealType(Delegate del)
            => del.CastDelegate<V60.CompileMethodDelegate>();
    }
}