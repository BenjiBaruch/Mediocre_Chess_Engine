using UnityEngine;
using System.Security.Cryptography;

namespace V5 
{
    public class Zobrist 
    {
        public static long[,] whitePieceHash;
        public static long[,] blackPieceHash;
        public static long blackToMoveHash;
        public static long[] castleRightsHash;
        public static long[] pawnLeapFilesHash;
        public readonly int[,] Transposition;
        public readonly int[][] Transposition2;
        public int keySize;
        readonly long keyMask;
        readonly int tableSize;
        static RNGCryptoServiceProvider RNG;

        public Zobrist(int keySize) {
            this.keySize = keySize;
            keyMask = 0;
            for (int i = 0; i < keySize; i++) {
                keyMask |= 1L << i;
            }
            tableSize = 1 << keySize;
            Transposition = new int[tableSize, 3];
            Transposition2 = new int[tableSize][];
            for (int i = 0; i < tableSize; i++) {
                Transposition2[i] = new int[3];
            }
        }

        int SearchEntry(int key, int check) {
            int offset = 0;
            while (true) {
                int value = Transposition[key + offset, 0];
                if (value == 0)
                    return -1;
                if (value == check)
                    return key + offset;
                if (key + offset < tableSize) {
                    offset++;
                } 
                else if (offset == -1) {
                    return -1;
                } 
                else {
                    offset = key - tableSize;
                }
            }
        }

        int SearchBlank(int key) {
            int offset = 0;
            while (Transposition[key + offset, 0] != 0) {
                if (key + offset < tableSize) {
                    offset++;
                } 
                else if (offset == -1) {
                    return -1;
                } 
                else {
                    offset = key - tableSize;
                }
            }
            return key + offset;
        }

        int SearchEntryOrBlank(int key, int check) {
            int offset = 0;
            while (true) {
                int value = Transposition[key + offset, 0];
                if (value == 0 || value == check)
                    return key + offset;
                if (key + offset < tableSize) {
                    offset++;
                } 
                else if (offset == -1) {
                    return -1;
                } 
                else {
                    offset = key - tableSize;
                }
            }
        }

        public int? Read2(long hash, int minDepth) {
            int key = (int)(hash & keyMask);
            int index = key;
            int check = (int)(hash >> 32);
            int[] entry;
            while (true) {
                entry = Transposition2[index];
                if (entry[0] == 0)
                    return null;
                if (entry[0] == check)
                    break;
                if (key - index == 1)
                    return null;
                if (index < tableSize-1)
                    index++;
                else
                    index = 0;
            } 
            if (entry[2] < minDepth)
                return null;
            return entry[1];
        }

        public int? Read(long hash, int minDepth) {
            int key = (int)(hash & keyMask);
            int check = (int)(hash >> 32);
            int index = SearchEntry(key, check);
            if (index == -1 || Transposition[index, 2] < minDepth)
                return null;
            return Transposition[index, 1];
        }

        public bool Write2(long hash, int score, int depth) {
            int key = (int)(hash & keyMask);
            int check = (int)(hash >> 32);
            int index = key;
            int[] entry;
            while (true) {
                entry = Transposition2[index];
                if (entry[0] == 0 || entry[0] == check)
                    break;
                if (key - index == 1)
                    return false;
                if (index < tableSize-1)
                    index++;
                else
                    index = 0;
            }
            entry[0] = check;
            entry[1] = score;
            entry[2] = depth;
            return true;
        }

        public bool Write(long hash, int score, int depth) {
            int key = (int)(hash & keyMask);
            int check = (int)(hash >> 32);
            int index = SearchEntryOrBlank(key, check);
            if (index == -1) 
                return false;
            Transposition[index, 0] = check;
            Transposition[index, 1] = score;
            Transposition[index, 2] = depth;
            return true;
        }

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