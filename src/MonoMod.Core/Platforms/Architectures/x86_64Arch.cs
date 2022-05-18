﻿using MonoMod.Core.Utils;
using System;
using System.Diagnostics.CodeAnalysis;

namespace MonoMod.Core.Platforms.Architectures {
    internal class x86_64Arch : IArchitecture, INeedsPlatformTripleInit {
        public ArchitectureKind Target => ArchitectureKind.x86_64;

        public ArchitectureFeature Features => ArchitectureFeature.Immediate64;

        private BytePatternCollection? lazyKnownMethodThunks;
        public unsafe BytePatternCollection KnownMethodThunks => Helpers.GetOrInit(ref lazyKnownMethodThunks, &CreateKnownMethodThunks);
        
        private static BytePatternCollection CreateKnownMethodThunks()
        {
            const ushort An = BytePattern.SAnyValue;
            const ushort Ad = BytePattern.SAddressValue;
            const byte Bn = BytePattern.BAnyValue;
            const byte Bd = BytePattern.BAddressValue;

            if (PlatformDetection.Runtime is RuntimeKind.Framework or RuntimeKind.CoreCLR) {
                return new BytePatternCollection(
                    // .NET Framework
                    new(new(AddressKind.Abs64),
                        // test rcx, rcx
                        0x48, 0x85, 0xc9,
                        // je .... ???
                        0x74, An,
                        // mov rax, [rcx]
                        0x48, 0x8b, 0x01,
                        // mov ... (extra)
                        0x49, An, An, An, An, An, An, An, An, An,
                        // cmp rax, r10
                        0x49, 0x3b, 0xc2,
                        // je ...
                        0x74, An,
                        // mov {TARGET}
                        0x48, 0xb8, Ad, Ad, Ad, Ad, Ad, Ad, Ad, Ad),

                    // .NET Core
                    new(new(AddressKind.Rel32, 5),
                        // jmp {DELTA}
                        0xe9, Ad, Ad, Ad, Ad,
                        // pop rdi
                        0x5f),

                    // Wine wierdness and generic type handle thunks
                    new(new(AddressKind.Abs64),
                            // movabs rax, {PTR}
                            0x48, 0xb8, Ad, Ad, Ad, Ad, Ad, Ad, Ad, Ad,
                            // jmp rax
                            0xff, 0xe0),

                    // Autoscan funkyness
                    new(new(AddressKind.Rel32, 19),
                        new byte[] { // mask
                            0xf0, 0xff, 00, 00, 00, 00, 00, 00, 00, 00, 
                            0xff, 0xff, 0xf0, 
                            0xff, 0xff, 00, 00, 00, 00
                        },
                        new byte[] { // pattern
                            // movabs ??1, ???
                            0x40, 0xb8, Bn, Bn, Bn, Bn, Bn, Bn, Bn, Bn,
                            // dec WORD PTR [??1]
                            0x66, 0xff, 0x00, 
                            // jne {DELTA}
                            0x0f, 0x85, Bd, Bd, Bd, Bd
                            // TODO: somehow encode a check that the ??1s are the same
                            // I somehow doubt that that's necessary, but hey
                        }),

                    // PrecodeFixupThunk (CLR 4+)
                    new(new(AddressKind.PrecodeFixupThunkRel32, 5),
                        // call {PRECODE FIXUP THUNK}
                        0xe8, Ad, Ad, Ad, Ad,
                        // pop rsi(?) (is this even consistent?)
                        0x5e),

                    // PrecodeFixupThunk (CLR 2)
                    new(new(AddressKind.PrecodeFixupThunkRel32, 5),
                        // call {PRECODE FIXUP THUNK}
                        0xe8, Ad, Ad, Ad, Ad,
                        // int 3
                        0xcc),

                    null
                );
            } else {
                // TODO: Mono
                return new();
            }
        }

        private sealed class Abs64Kind : DetourKindBase {
            public static readonly Abs64Kind Instance = new();

            public override int Size => 1 + 1 + 4 + 8;

            public override int GetBytes(IntPtr from, IntPtr to, Span<byte> buffer, object? data, out IDisposable? allocHandle) {
                buffer[0] = 0xff;
                buffer[1] = 0x25;
                Unsafe.WriteUnaligned(ref buffer[2], (int) 0);
                Unsafe.WriteUnaligned(ref buffer[6], (long) to);
                allocHandle = null;
                return Size;
            }
        }

        private sealed class Abs64SplitKind : DetourKindBase {
            public static readonly Abs64SplitKind Instance = new();

            public override int Size => 1 + 1 + 4;

            public override int GetBytes(IntPtr from, IntPtr to, Span<byte> buffer, object? data, out IDisposable? allocHandle) {
                Helpers.ThrowIfNull(data);
                var alloc = (IAllocatedMemory) data;

                buffer[0] = 0xff;
                buffer[1] = 0x25;
                Unsafe.WriteUnaligned(ref buffer[2], (int) (alloc.BaseAddress - ((nint) from + 6)));

                Unsafe.WriteUnaligned(ref alloc.Memory[0], to);

                allocHandle = alloc;
                return Size;
            }
        }

        private PlatformTriple platformTriple = null!;

        void INeedsPlatformTripleInit.Initialize(PlatformTriple triple) {
            platformTriple = triple;
        }

        void INeedsPlatformTripleInit.PostInit() {
            // no-op
        }

        [SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope",
            Justification = "Ownership of the allocation is transferred correctly.")]
        public NativeDetourInfo ComputeDetourInfo(nint from, nint to, int sizeHint) {
            x86Shared.FixSizeHint(ref sizeHint);

            if (x86Shared.TryRel32Detour(from, to, sizeHint, out var rel32Info))
                return rel32Info;

            var target = from + 6;
            var memRequest = new AllocationRequest(target, target + int.MinValue, target + int.MaxValue, IntPtr.Size);
            if (sizeHint >= Abs64SplitKind.Instance.Size && platformTriple.System.MemoryAllocator.TryAllocateInRange(memRequest, out var allocated)) {
                return new(from, to, Abs64SplitKind.Instance, allocated);
            }

            // TODO: more, smaller detours

            if (sizeHint < Abs64Kind.Instance.Size) {
                MMDbgLog.Log($"Size too small for all known detour kinds; defaulting to Abs64. provided size: {sizeHint}");
            }
            return new(from, to, Abs64Kind.Instance, null);
        }

        public int GetDetourBytes(NativeDetourInfo info, Span<byte> buffer, out IDisposable? allocHandle) {
            return DetourKindBase.GetDetourBytes(info, buffer, out allocHandle);
        }
    }
}