using System;
using Unity.Mathematics;
using UnityEditor.PackageManager;
using UnityEngine.TextCore.Text;
using Debug = UnityEngine.Debug;

namespace V6
{
    public static class PrecomputeMoves
    {
        // Help from https://www.chessprogramming.org/Magic_Bitboards
        // Help from https://analog-hors.github.io/site/magic-bitboards/
        public static ulong[] KnightMoves;
        public static ulong[] KingMoves;
        public static ulong[] OrthBSlide;
        public static ulong[] DiagBSlide;
        public static ulong[] OrthFSlide;
        public static ulong[] DiagFSlide;
        public static ulong[] WhiteEPSentries;
        public static ulong[] BlackEPSentries;
        static ulong[][] RookAttacks;
        static ulong[][] BishopAttacks;
        static ulong[][] BishopSubMasks;
        static ulong[][] RookSubMasks;
        static ulong[] RookMagicValues;
        static ulong[] BishopMagicValues;
        static byte[] RookShifts;
        static byte[] BishopShifts;
        static Magic[] RookMagics;
        static Magic[] BishopMagics;
        static readonly System.Random rng = new();
        readonly struct Magic
        {
            public Magic(ulong[] attacks, ulong mask, ulong magic, int shift)
            {
                this.attacks = attacks;
                this.mask = mask;
                this.magic = magic;
                this.shift = (byte)shift;
            }
            public readonly ulong[] attacks;
            public readonly ulong mask;
            public readonly ulong magic;
            public readonly byte shift;
            
        }
        public static void ComputeMoveTables() 
        {
            ComputeKingTable();
            ComputeKnightTable();
            ComputeEnPassantInfo();
            ComputeSlideTables();
            LoadMagics();
        }

        static void LoadMagics()
        {
            RookMagics = new Magic[64];
            BishopMagics = new Magic[64];
            for (int i = 0; i < 64; i++) {
                BishopMagics[i] = new(BishopAttacks[i], DiagBSlide[i], BishopMagicValues[i], BishopShifts[i]);
                RookMagics[i] = new(RookAttacks[i], OrthBSlide[i], RookMagicValues[i], RookShifts[i]);
            }
        }

        static void FindMagic(int piece, int square, byte maxLen)
        {
            ulong mask;
            if (piece == Piece.Bishop)
                mask = DiagBSlide[square];
            else
                mask = OrthBSlide[square];
            byte len = byte.MaxValue;
            byte[] buffer = new byte[8];
            while (len > maxLen) {
                rng.NextBytes(buffer);
                ulong magic = (ulong) BitConverter.ToInt64(buffer, 0);

            }
        }
        // Return tuple:
        // bool: magic is valid
        // ulong: table of attacks
        // byte: shift
        static Tuple<bool, ulong[], byte> TestMagic(ulong magic, bool isBishop, int pos, byte maxLen)
        {
            ulong[] masks = isBishop ? BishopSubMasks[pos] : RookSubMasks[pos];
            foreach (ulong mask in masks) {

            }
            return new(false, new ulong[masks.Length], 0);
        }

        static void GenerateSubMasks()
        {
            BishopSubMasks = new ulong[64][];
            RookSubMasks = new ulong[64][];
            for (int pos = 0; pos < 64; pos++) {
                ulong bishopMask = DiagBSlide[pos];
                int bits = math.countbits(bishopMask);
                ulong values = (ulong) (1 << bits);
                BishopSubMasks[pos] = new ulong[values];
                for (ulong i = 0; i < values; i++) {
                    ulong bishopMaskCopy = bishopMask;
                    ulong subMask = 0UL;
                    for (int bit = 0; bit < bits; bit++) {
                        subMask |= ((i >> bit) & 1) << math.tzcnt(bishopMaskCopy);
                        bishopMaskCopy &= bishopMaskCopy - 1;
                    }
                    BishopSubMasks[pos][i] = subMask;
                }

                ulong rookMask = OrthBSlide[pos];
                bits = math.countbits(rookMask);
                values = (ulong) (1 << bits);
                RookSubMasks[pos] = new ulong[values];
                for (ulong i = 0; i < values; i++) {
                    ulong rookMaskCopy = rookMask;
                    ulong subMask = 0UL;
                    for (int bit = 0; bit < bits; bit++) {
                        subMask |= ((i >> bit) & 1) << math.tzcnt(rookMaskCopy);
                        rookMaskCopy &= rookMaskCopy - 1;
                    }
                    RookSubMasks[pos][i] = subMask;
                }
            }
        }

        public static ulong BishopAttackMap(int pos, ulong blockers)
        {
            Magic m = BishopMagics[pos];
            blockers &= m.mask;
            blockers *= m.magic;
            blockers >>= m.shift;
            return m.attacks[blockers];
        }
        public static ulong RookAttackMap(int pos, ulong blockers)
        {
            Magic m = RookMagics[pos];
            blockers &= m.mask;
            blockers *= m.magic;
            blockers >>= m.shift;
            return m.attacks[blockers];
        }

        static void ComputeSlideTables() 
        {
            OrthBSlide = new ulong[64];
            DiagBSlide = new ulong[64];
            OrthFSlide = new ulong[64];
            DiagFSlide = new ulong[64];
            
            for (int pos = 0; pos < 64; pos++) {
                OrthBSlide[pos] = ShootBlockedRay(pos, -1)
                                | ShootBlockedRay(pos, 1)
                                | ShootBlockedRay(pos, -8)
                                | ShootBlockedRay(pos, 8);
                DiagBSlide[pos] = ShootBlockedRay(pos, -9)
                                | ShootBlockedRay(pos, -7)
                                | ShootBlockedRay(pos, 7)
                                | ShootBlockedRay(pos, 9);
                OrthFSlide[pos] = ShootFullRay(pos, -1)
                                | ShootFullRay(pos, 1)
                                | ShootFullRay(pos, -8)
                                | ShootFullRay(pos, 8);
                DiagFSlide[pos] = ShootFullRay(pos, -9)
                                | ShootFullRay(pos, -7)
                                | ShootFullRay(pos, 7)
                                | ShootFullRay(pos, 9);
            }
        }

        static ulong ShootBlockedRay(int pos, int offset)
        {
            ulong ray = 0UL;

            int safeFile = pos & 0b111;
            int safeRank = pos >> 3;

            for (int i = 0; i < 7; i++) {
                int x = (pos + offset) & 0b111;
                int y = (pos + offset) >> 3;
                if (((pos & 0b111) == 0 && ((offset + 16) % 8) == 7) ||
                    ((pos & 0b111) == 7 && ((offset + 16) % 8) == 1) ||
                    ((pos >> 3) == 0 && (offset / 4) < 0) ||
                    ((pos >> 3) == 7 && (offset / 4) > 0) ||
                    (x == 0 && safeFile > 0) ||
                    (x == 7 && safeFile < 7) ||
                    (y == 0 && safeRank > 0) ||
                    (y == 7 && safeRank < 7)) {
                    break;
                }
                pos += offset;
                ray |= 1UL << pos;
            }
            return ray;
        }

        static ulong ShootFullRay(int pos, int offset)
        {
            ulong ray = 0UL;
            
            for (int i = 0; i < 7; i++) {
                if (((pos & 0b111) == 0 && ((offset + 16) % 8) == 7) ||
                    ((pos & 0b111) == 7 && ((offset + 16) % 8) == 1) ||
                    ((pos >> 3) == 0 && (offset / 4) < 0) ||
                    ((pos >> 3) == 7 && (offset / 4) > 0)) {
                    break;
                }
                pos += offset;
                ray |= 1UL << pos;
            }
            return ray;
        }

        static void ComputeKnightTable() 
        {
            KnightMoves = new ulong[64];
            for (int pos = 0; pos < 64; pos++) {
                ulong board = 0UL;
                int x = pos & 0b111;
                int y = pos >> 3;
                if (x > 0)
                {
                    if (y > 1) board |= 1UL << pos - 17;
                    if (y < 6) board |= 1UL << pos + 15;
                    if (x > 1) 
                    { 
                        if (y > 0) board |= 1UL << pos - 10;
                        if (y < 7) board |= 1UL << pos + 6;
                    }
                }
                if (x < 7)
                {
                    if (y > 1) board |= 1UL << pos - 15;
                    if (y < 6) board |= 1UL << pos + 17;
                    if (x < 6)
                    {
                        if (y > 0) board |= 1UL << pos - 6;
                        if (y < 7) board |= 1UL << pos + 10;
                    }
                }
                KnightMoves[pos] = board;
            }
        }

        static void ComputeKingTable()
        {
            KingMoves = new ulong[64];
            int[] offsets = {-9, -8, -7, -1, 1, 7, 8, 9};
            for (int pos = 0; pos < 64; pos++) {
                ulong board = 0UL;
                foreach (int offset in offsets)
                    if (0 <= pos + offset && pos + offset < 64)
                        board |= 1UL << (pos + offset);
                KingMoves[pos] = board;
            }
        }

        static void ComputeEnPassantInfo()
        {
            WhiteEPSentries = new ulong[9];
            BlackEPSentries = new ulong[9];

            for (int i = 0; i < 8; i++) {
                if (i > 0) {
                    WhiteEPSentries[i] |= 1UL << i + 23;
                    BlackEPSentries[i] |= 1UL << i + 39;
                }
                if (i < 7) {
                    WhiteEPSentries[i] |= 1UL << i + 25;
                    BlackEPSentries[i] |= 1UL << i + 41;
                }
            }
        }
    }
}