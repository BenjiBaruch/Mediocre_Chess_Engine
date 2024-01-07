using UnityEditor.PackageManager;
using Debug = UnityEngine.Debug;

namespace V6
{
    public static class PrecomputeMoves
    {
        public static ulong[] KnightMoves;
        public static ulong[] KingMoves;
        public static ulong[] OrthBSlide;
        public static ulong[] DiagBSlide;
        public static ulong[] OrthFSlide;
        public static ulong[] DiagFSlide;
        public static ulong[] WhiteEPSentries;
        public static ulong[] BlackEPSentries;
        public static void ComputeMoveTables() 
        {
            ComputeKingTable();
            ComputeKnightTable();
            ComputeEnPassantInfo();
            ComputeSlideTables();
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
                pos += offset;
                int x = pos & 0b111;
                int y = pos >> 3;
                if (x >= ((safeFile == 0) ? 0 : 1) &&
                    x <= ((safeFile == 7) ? 7 : 6) &&
                    y >= ((safeRank == 0) ? 0 : 1) &&
                    y <= ((safeRank == 7) ? 7 : 6)) {
                    ray |= 1UL << pos;
                }
            }
            return ray;
        }

        static ulong ShootFullRay(int pos, int offset)
        {
            ulong ray = 0UL;
            
            int safeFile = pos & 0b111;
            int safeRank = pos >> 3;
            for (int i = 0; i < 7; i++) {
                pos += offset;
                int x = pos & 0b111;
                int y = pos >> 3;
                if (x >= 0 && x <= 7 && y >= 0 && y <= 7)
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