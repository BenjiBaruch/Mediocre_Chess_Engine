using UnityEngine;
using System.Security.Cryptography;

namespace V4 
{
    public static class Zobrist 
    {
        public static long[,] whitePieceHash;
        public static long[,] blackPieceHash;
        public static long blackToMoveHash;
        public static long[] castleRightsHash;
        public static long[] pawnLeapFilesHash;
        static RNGCryptoServiceProvider RNG;

        public static void Initialize() 
        {
            // help from https://learn.microsoft.com/en-us/dotnet/api/system.security.cryptography.rngcryptoserviceprovider?view=net-8.0&redirectedfrom=MSDN
            // help from https://www.chessprogramming.org/Zobrist_Hashing
            RNG = new RNGCryptoServiceProvider();
            whitePieceHash = new long[8,64];
            blackPieceHash = new long[8,64];
            for (int i = 0; i < 7; i++) {
                for (int j = 0; j < 64; j++) {
                    whitePieceHash[i,j] = GetRandom();
                    blackPieceHash[i,j] = GetRandom();
                }
            }
            blackToMoveHash = GetRandom();
            castleRightsHash = new long[16];
            for (int i = 0; i < 16; i++) {
                castleRightsHash[i] = GetRandom();
            }
            pawnLeapFilesHash = new long[9];
            for (int i = 0; i < 9; i++) {
                pawnLeapFilesHash[i] = GetRandom();
            }
        }

        public static long GetRandom() 
        {
            var bytes = new byte[8];
            RNG.GetBytes(bytes);
            long hash = 0;
            foreach (byte b in bytes) {
                hash = (hash << 8) + b;
            }
            return hash;
        }

        public static long HashBoard(MoveGen board) 
        {
            long hash = board.WhiteToMove ? 0 : blackToMoveHash;
            int[] IntBoard = board.IntBoard;
            for (int i = 0; i < 64; i++) {
                if ((IntBoard[i] & 0b11000) == 0b01000) {
                    hash ^= whitePieceHash[IntBoard[i] & 0b111, i];
                }
                else if ((IntBoard[i] & 0b11000) == 0b10000) {
                    hash ^= blackPieceHash[IntBoard[i] & 0b111, i];
                }
            }
            hash ^= castleRightsHash[board.castlingRights];
            hash ^= pawnLeapFilesHash[board.pawnLeapFile];
            return hash;
        }
    }
}